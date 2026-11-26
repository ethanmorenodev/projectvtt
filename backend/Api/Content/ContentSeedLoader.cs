using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Importación del contenido real empaquetado en `Content/Seed/*.json`
/// (PMB-P.4, ADR-037): paquetes con procedencia que se cargan al arrancar
/// de forma **idempotente** por `(paquete, key)`. Si el contenido difiere
/// respecto al almacenado, la actualización publica una revisión nueva
/// (nunca silenciosa, ADR-036). El pipeline de importación general
/// (PMB-A.2) crecerá sobre este formato.
/// </summary>
public static class ContentSeedLoader
{
    /// <summary>Importa todos los paquetes seed (seguro repetirlo).</summary>
    public static async Task ImportAsync(
        IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var seedDirectory = Path.Combine(AppContext.BaseDirectory, "Content", "Seed");
        if (!Directory.Exists(seedDirectory))
        {
            return;
        }

        using var scope = services.CreateScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var file in Directory.EnumerateFiles(seedDirectory, "*.json").OrderBy(x => x))
        {
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(file, cancellationToken))
                as JsonObject ?? throw new InvalidOperationException(
                    $"El paquete seed '{Path.GetFileName(file)}' no es un objeto JSON.");
            await ImportPackAsync(packs, db, manifest, cancellationToken);
        }
    }

    private static async Task ImportPackAsync(
        ContentPackService packs, AppDbContext db, JsonObject manifest,
        CancellationToken cancellationToken)
    {
        var packNode = manifest["pack"] as JsonObject
            ?? throw new InvalidOperationException("El paquete seed no incluye 'pack'.");
        var packKey = packNode["key"]?.GetValue<string>()
            ?? throw new InvalidOperationException("El paquete seed no incluye 'pack.key'.");

        await packs.RegisterAsync(new CreateContentPackRequest(
            Key: packKey,
            Name: packNode["name"]?.GetValue<string>(),
            Version: packNode["version"]?.GetValue<string>(),
            System: packNode["system"]?.GetValue<string>(),
            SourceUrl: packNode["sourceUrl"]?.GetValue<string>(),
            License: packNode["license"]?.GetValue<string>(),
            LicenseUrl: packNode["licenseUrl"]?.GetValue<string>(),
            Attribution: (manifest["pack"]?["attribution"] as JsonArray)?
                .Select(v => v?.GetValue<string>() ?? string.Empty)
                .Where(v => v.Length > 0)
                .ToArray()), cancellationToken);

        // Tipos del paquete (find-or-create por (system, key)).
        var typeIdByKey = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (manifest["types"] is JsonArray types)
        {
            foreach (var entry in types)
            {
                if (entry is not JsonObject typeNode)
                {
                    continue;
                }
                var key = typeNode["key"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("Tipo seed sin 'key'.");
                var type = await packs.EnsureTypeAsync(
                    packNode["system"]?.GetValue<string>() ?? "dnd5e",
                    key,
                    typeNode["displayName"]?.GetValue<string>() ?? key,
                    typeNode["schema"],
                    cancellationToken);
                typeIdByKey[key] = type.Id;
            }
        }

        if (manifest["items"] is not JsonArray items)
        {
            return;
        }

        foreach (var entry in items)
        {
            if (entry is not JsonObject itemNode)
            {
                continue;
            }
            var itemKey = itemNode["itemKey"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Item seed sin 'itemKey'.");
            var typeKey = itemNode["typeKey"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Item seed '{itemKey}' sin 'typeKey'.");
            if (!typeIdByKey.TryGetValue(typeKey, out var typeId))
            {
                throw new InvalidOperationException(
                    $"Item seed '{itemKey}' apunta al tipo desconocido '{typeKey}'.");
            }

            var properties = itemNode["properties"] as JsonObject
                ?? throw new InvalidOperationException($"Item seed '{itemKey}' sin 'properties'.");
            var name = itemNode["name"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Item seed '{itemKey}' sin 'name'.");

            var request = new ImportContentItemRequest(
                TypeId: typeId,
                ItemKey: itemKey,
                Name: name,
                Summary: itemNode["summary"]?.GetValue<string>(),
                Description: itemNode["description"]?.GetValue<string>(),
                Tags: (itemNode["tags"] as JsonArray)?
                    .Select(v => v?.GetValue<string>() ?? string.Empty)
                    .Where(v => v.Length > 0)
                    .ToArray(),
                Properties: properties,
                Inheritance: ParseInheritance(itemNode["inheritance"] as JsonObject),
                Message: "Importación desde el paquete seed",
                ParentItemKey: itemNode["parentItemKey"]?.GetValue<string>());

            var existing = await db.ContentItems
                .FirstOrDefaultAsync(
                    x => x.PackKey == packKey && x.PackItemKey == itemKey,
                    cancellationToken);

            if (existing is null)
            {
                await packs.ImportItemAsync(
                    packKey, request, ImportConflictStrategy.Keep, cancellationToken);
                continue;
            }

            // Idempotencia real: solo se actualiza si el contenido difiere,
            // y la actualización publica una revisión (nunca silenciosa).
            var changed =
                existing.Name != name
                || existing.Summary != (request.Summary?.Trim() is { Length: > 0 } s ? s : null)
                || existing.PropertiesJson != properties.ToJsonString()
                || existing.InheritanceJson != ToJsonObject(request.Inheritance);
            if (changed)
            {
                await packs.ImportItemAsync(
                    packKey, request, ImportConflictStrategy.Update, cancellationToken);
            }
        }
    }

    private static Dictionary<string, string>? ParseInheritance(JsonObject? node)
    {
        if (node is null)
        {
            return null;
        }
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in node)
        {
            if (value is JsonValue v && v.TryGetValue<string>(out var mode))
            {
                result[key] = mode;
            }
        }
        return result;
    }

    private static string ToJsonObject(Dictionary<string, string>? values)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values ?? [])
        {
            obj[key] = value;
        }
        return obj.ToJsonString();
    }
}
