using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Importación/exportación de paquetes `.pvttpkg` (ADR-036, PMB-7.4):
/// zip con `manifest.json`, `content.json` y `LICENSES/`. Sin ejecución de
/// código, sin descargas, tamaños acotados y conflictos resueltos siempre
/// de forma explícita (Keep/Update/Copy).
/// </summary>
public sealed class PackageService(AppDbContext db, ContentPackService packs)
{
    private const long MaxArchiveBytes = 10 * 1024 * 1024;
    private const long MaxUncompressedBytes = 20 * 1024 * 1024;
    private const int MaxEntries = 2000;

    public async Task<byte[]> ExportAsync(
        string packKey, CancellationToken cancellationToken = default)
    {
        var pack = await db.ContentPacks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == packKey, cancellationToken)
            ?? throw ApiException.NotFound("El paquete no existe.");
        var types = await db.ContentTypes.AsNoTracking()
            .Where(x => x.System == pack.System)
            .ToListAsync(cancellationToken);
        var items = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == packKey)
            .ToListAsync(cancellationToken);

        var manifest = new JsonObject
        {
            ["key"] = pack.Key, ["name"] = pack.Name, ["version"] = pack.Version,
            ["system"] = pack.System, ["sourceUrl"] = pack.SourceUrl,
            ["license"] = pack.License, ["licenseUrl"] = pack.LicenseUrl,
            ["attribution"] = new JsonArray(ParseArray(pack.AttributionJson)
                .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["exportedAt"] = DateTime.UtcNow,
        };
        var content = new JsonObject
        {
            ["types"] = new JsonArray(types.Select(type => (JsonNode?)new JsonObject
            {
                ["key"] = type.Key, ["displayName"] = type.DisplayName,
                ["schema"] = JsonNode.Parse(type.SchemaJson),
            }).ToArray()),
            ["items"] = new JsonArray(items.Select(item => (JsonNode?)new JsonObject
            {
                ["itemKey"] = item.PackItemKey, ["typeKey"] = types
                    .FirstOrDefault(type => type.Id == item.TypeId)?.Key,
                ["name"] = item.Name, ["summary"] = item.Summary,
                ["description"] = item.Description,
                ["tags"] = new JsonArray(ParseArray(item.TagsJson)
                    .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                ["properties"] = JsonNode.Parse(item.PropertiesJson),
                ["inheritance"] = JsonNode.Parse(item.InheritanceJson),
            }).ToArray()),
        };

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "manifest.json", manifest.ToJsonString());
            Write(zip, "content.json", content.ToJsonString());
            Write(zip, "LICENSES/NOTICE.txt", string.Join('\n', ParseArray(pack.AttributionJson)));
        }
        return stream.ToArray();
    }

    public async Task<PackageImportReportDto> ImportAsync(
        Stream archive, ImportConflictStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await archive.CopyToAsync(memory, cancellationToken);
        if (memory.Length > MaxArchiveBytes)
        {
            throw ApiException.BadRequest("El paquete supera los 10 MB.");
        }
        memory.Position = 0;

        JsonObject manifest, content;
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (zip.Entries.Count > MaxEntries)
            {
                throw ApiException.BadRequest("Demasiadas entradas en el paquete.");
            }
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.EndsWith('/'))
                {
                    total += entry.Length;
                }
                if (entry.FullName.Contains("..") || Path.IsPathRooted(entry.FullName)
                    || !IsAllowed(entry.FullName))
                {
                    throw ApiException.BadRequest(
                        $"Entrada no permitida en el paquete: '{entry.FullName}'.");
                }
            }
            if (total > MaxUncompressedBytes)
            {
                throw ApiException.BadRequest("El contenido descomprimido supera los 20 MB.");
            }
            manifest = ReadJson(zip, "manifest.json");
            content = ReadJson(zip, "content.json");
        }

        var key = Text(manifest, "key", "manifest.key", maxLength: 100);
        await packs.RegisterAsync(new CreateContentPackRequest(
            key,
            Text(manifest, "name", "manifest.name", maxLength: 200),
            Text(manifest, "version", "manifest.version", maxLength: 50),
            Text(manifest, "system", "manifest.system", maxLength: 50),
            manifest["sourceUrl"]?.GetValue<string>(),
            Text(manifest, "license", "manifest.license", maxLength: 100),
            manifest["licenseUrl"]?.GetValue<string>(),
            (manifest["attribution"] as JsonArray)?
                .Select(value => value?.GetValue<string>() ?? string.Empty)
                .Where(value => value.Length > 0).ToArray()), cancellationToken);

        var typeIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var typeCount = 0;
        foreach (var entry in (content["types"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var typeKey = Text(entry, "key", "content.types.key", maxLength: 50);
            var type = await packs.EnsureTypeAsync(
                manifest["system"]!.GetValue<string>(), typeKey,
                Text(entry, "displayName", "content.types.displayName", maxLength: 100),
                entry["schema"], cancellationToken);
            typeIds[typeKey] = type.Id;
            typeCount++;
        }

        var conflicts = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;
        foreach (var entry in (content["items"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var itemKey = Text(entry, "itemKey", "content.items.itemKey", maxLength: 100);
            var typeKey = Text(entry, "typeKey", "content.items.typeKey", maxLength: 50);
            if (!typeIds.TryGetValue(typeKey, out var typeId))
            {
                throw ApiException.BadRequest($"Tipo desconocido '{typeKey}' en '{itemKey}'.");
            }
            var exists = await db.ContentItems.AnyAsync(
                x => x.PackKey == key && x.PackItemKey == itemKey, cancellationToken);
            if (exists)
            {
                conflicts.Add(itemKey);
                if (strategy == ImportConflictStrategy.Keep)
                {
                    skipped++;
                    continue;
                }
                if (strategy == ImportConflictStrategy.Update)
                {
                    updated++;
                }
                else
                {
                    created++;
                }
            }
            else
            {
                created++;
            }

            await packs.ImportItemAsync(key, new ImportContentItemRequest(
                typeId, itemKey,
                Text(entry, "name", "content.items.name", maxLength: 200),
                entry["summary"]?.GetValue<string>(),
                entry["description"]?.GetValue<string>(),
                (entry["tags"] as JsonArray)?
                    .Select(value => value?.GetValue<string>() ?? string.Empty)
                    .Where(value => value.Length > 0).ToArray(),
                entry["properties"] as JsonObject,
                entry["inheritance"] as JsonObject is { } inheritance
                    ? inheritance.ToDictionary(
                        node => node.Key, node => node.Value?.GetValue<string>() ?? "inherit")
                    : null,
                "Importación desde .pvttpkg"), strategy, cancellationToken);
        }

        return new PackageImportReportDto(
            key, strategy.ToString(), typeCount, created, updated, skipped,
            conflicts.ToArray());
    }

    // --- Utilidades ------------------------------------------------------

    private static bool IsAllowed(string name) =>
        name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith('/');

    private static void Write(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static JsonObject ReadJson(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name)
            ?? throw ApiException.BadRequest($"Falta '{name}' en el paquete.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd()) as JsonObject
            ?? throw ApiException.BadRequest($"'{name}' no es un objeto JSON.");
    }

    private static string Text(JsonObject node, string key, string field, int maxLength) =>
        node[key]?.GetValue<string>() is { Length: > 0 } value && value.Length <= maxLength
            ? value
            : throw ApiException.BadRequest($"Falta o no es válido '{field}'.",
                new Dictionary<string, object?> { ["field"] = field });

    private static string[] ParseArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)
            || JsonNode.Parse(json) is not JsonArray array)
        {
            return [];
        }
        return array.Select(value => value?.GetValue<string>() ?? string.Empty)
            .Where(value => value.Length > 0).ToArray();
    }
}
