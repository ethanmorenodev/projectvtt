using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Paquetes de contenido (PMB-P.8, ADR-036): registro idempotente por
/// clave, identidad de elementos por <c>(paquete, key)</c> y reimportación
/// con resolución explícita de conflictos (Keep/Update/Copy — nunca
/// silenciosa: `Update` publica una revisión nueva). La procedencia
/// (licencia, atribución, origen) vive en el paquete y se hereda. Es la
/// vía autorizada para contenido `global` (el API de usuarios no crea ese
/// ámbito); el pipeline de importación (PMB-A.2) y el paquete SRD
/// (PMB-P.4) construyen sobre este servicio.
/// </summary>
public sealed class ContentPackService(AppDbContext db)
{
    /// <summary>Registra o actualiza un paquete por clave (idempotente).</summary>
    public async Task<ContentPackDto> RegisterAsync(
        CreateContentPackRequest request, CancellationToken cancellationToken = default)
    {
        var key = InputGuard.RequiredText(request.Key, "key", maxLength: 100);
        var name = InputGuard.RequiredText(request.Name, "name", maxLength: 200);
        var version = InputGuard.RequiredText(request.Version, "version", maxLength: 50);
        var system = InputGuard.RequiredText(request.System, "system", maxLength: 50);
        var license = InputGuard.RequiredText(request.License, "license", maxLength: 100);

        var pack = await db.ContentPacks
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (pack is null)
        {
            pack = new ContentPack
            {
                Id = Guid.NewGuid(),
                Key = key,
                Name = name,
                Version = version,
                System = system,
                SourceUrl = Truncate(request.SourceUrl, 500),
                License = license,
                LicenseUrl = Truncate(request.LicenseUrl, 500),
                AttributionJson = ToJsonArray(request.Attribution),
                ImportedAt = DateTime.UtcNow,
            };
            db.ContentPacks.Add(pack);
        }
        else
        {
            // Reimportación: los metadatos siguen al paquete (idempotente).
            pack.Name = name;
            pack.Version = version;
            pack.System = system;
            pack.SourceUrl = Truncate(request.SourceUrl, 500);
            pack.License = license;
            pack.LicenseUrl = Truncate(request.LicenseUrl, 500);
            pack.AttributionJson = ToJsonArray(request.Attribution);
            pack.ImportedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(pack);
    }

    public async Task<List<ContentPackDto>> ListAsync(
        CancellationToken cancellationToken = default) =>
        (await db.ContentPacks.AsNoTracking()
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken))
        .Select(ToDto)
        .ToList();

    /// <summary>
    /// Busca o crea un tipo de contenido (utilidad del importador: los
    /// tipos del paquete se registran junto a su contenido).
    /// </summary>
    public async Task<ContentType> EnsureTypeAsync(
        string system, string key, string displayName, JsonNode? schema,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.ContentTypes
            .FirstOrDefaultAsync(
                x => x.System == system && x.Key == key, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var type = new ContentType
        {
            Id = Guid.NewGuid(),
            System = system,
            Key = key,
            DisplayName = displayName,
            SchemaJson = (schema ?? new JsonObject()).ToJsonString(),
            CreatedAt = DateTime.UtcNow,
        };
        db.ContentTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        return type;
    }

    /// <summary>
    /// Importa un item con identidad <c>(packKey, itemKey)</c>. Si ya
    /// existe, <paramref name="strategy"/> decide explícitamente.
    /// </summary>
    public async Task<ContentItem> ImportItemAsync(
        string packKey, ImportContentItemRequest request,
        ImportConflictStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var pack = await db.ContentPacks
            .FirstOrDefaultAsync(x => x.Key == packKey, cancellationToken)
            ?? throw ApiException.BadRequest(
                $"El paquete '{packKey}' debe registrarse antes de importar su contenido.");

        var itemKey = InputGuard.RequiredText(request.ItemKey, "itemKey", maxLength: 100);
        var name = InputGuard.RequiredText(request.Name, "name", maxLength: 200);
        var typeId = request.TypeId
            ?? throw ApiException.BadRequest("Falta el campo 'typeId'.");
        var type = await db.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == typeId, cancellationToken)
            ?? throw ApiException.NotFound("Tipo de contenido no encontrado.");

        var properties = request.Properties as JsonObject
            ?? throw ApiException.BadRequest("El campo 'properties' debe ser un objeto JSON.");
        ContentSchema.Validate(
            JsonNode.Parse(type.SchemaJson), properties, field: "properties");
        if (type.Key == "ability")
        {
            AbilitySchema.Validate(properties);
        }
        if (properties["choiceGroups"] is JsonArray choiceGroups)
        {
            ChoiceGroupSchema.Validate(choiceGroups);
        }
        var inheritance = NormalizeInheritance(request.Inheritance);

        var existing = await db.ContentItems
            .FirstOrDefaultAsync(
                x => x.PackKey == packKey && x.PackItemKey == itemKey, cancellationToken);

        if (existing is not null)
        {
            return strategy switch
            {
                ImportConflictStrategy.Keep => existing,
                ImportConflictStrategy.Update => await UpdateImportedAsync(
                    existing, name, request, properties, inheritance, cancellationToken),
                ImportConflictStrategy.Copy => await CopyImportedAsync(
                    packKey, itemKey, type, name, request, properties, inheritance,
                    cancellationToken),
                _ => throw ApiException.BadRequest("Estrategia de conflicto no válida."),
            };
        }

        return await CreateImportedAsync(
            pack, itemKey, type, name, request, properties, inheritance,
            cancellationToken);
    }

    // --- Internos -------------------------------------------------------

    private async Task<ContentItem> CreateImportedAsync(
        ContentPack pack, string itemKey, ContentType type, string name,
        ImportContentItemRequest request, JsonObject properties,
        Dictionary<string, string> inheritance, CancellationToken cancellationToken)
    {
        // Jerarquía del paquete (variantes/subrazas): el padre debe existir
        // y ser del mismo tipo o una categoría de ese sistema. Como solo se asigna al crear, el árbol
        // importado no puede formar ciclos.
        Guid? parentId = null;
        if (!string.IsNullOrWhiteSpace(request.ParentItemKey))
        {
            var parent = await db.ContentItems.AsNoTracking().Include(x => x.Type)
                .FirstOrDefaultAsync(
                    x => x.PackKey == pack.Key && x.PackItemKey == request.ParentItemKey,
                    cancellationToken)
                ?? throw ApiException.BadRequest(
                    $"El padre '{request.ParentItemKey}' no existe en el paquete '{pack.Key}'.");
            if (!ContentResolver.CanParent(type, parent.Type))
            {
                throw ApiException.BadRequest("El padre debe ser del mismo tipo o una categoría del mismo sistema.");
            }
            parentId = parent.Id;
        }

        var now = DateTime.UtcNow;
        var item = new ContentItem
        {
            Id = Guid.NewGuid(),
            System = type.System,
            TypeId = type.Id,
            Name = name,
            Summary = Truncate(request.Summary, 500),
            Description = request.Description,
            ParentId = parentId,
            TagsJson = ToJsonArray(request.Tags),
            PropertiesJson = properties.ToJsonString(),
            InheritanceJson = ToJsonObject(inheritance),
            Scope = ContentScope.Global,
            IsPrivate = false,
            PrivateFieldsJson = "[]",
            WorkflowState = ContentWorkflowState.Published,
            CurrentRevision = 1,
            PackKey = pack.Key,
            PackItemKey = itemKey,
            CreatedByUserId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ContentItems.Add(item);
        db.ContentRevisions.Add(new ContentRevision
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            Number = 1,
            PayloadJson = PayloadOf(item),
            Message = FirstImportMessage(request.Message),
            CreatedByUserId = null,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private async Task<ContentItem> UpdateImportedAsync(
        ContentItem item, string name, ImportContentItemRequest request,
        JsonObject properties, Dictionary<string, string> inheritance,
        CancellationToken cancellationToken)
    {
        item.Name = name;
        item.Summary = Truncate(request.Summary, 500);
        item.Description = request.Description;
        item.TagsJson = ToJsonArray(request.Tags);
        item.PropertiesJson = properties.ToJsonString();
        item.InheritanceJson = ToJsonObject(inheritance);
        item.CurrentRevision += 1;
        item.UpdatedAt = DateTime.UtcNow;

        // Nunca silenciosa: la actualización publica una revisión (ADR-036).
        db.ContentRevisions.Add(new ContentRevision
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            Number = item.CurrentRevision,
            PayloadJson = PayloadOf(item),
            Message = Truncate(request.Message, 500) ?? "Actualización por reimportación",
            CreatedByUserId = null,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private async Task<ContentItem> CopyImportedAsync(
        string packKey, string itemKey, ContentType type, string name,
        ImportContentItemRequest request, JsonObject properties,
        Dictionary<string, string> inheritance, CancellationToken cancellationToken)
    {
        // Copia explícita: el anterior se conserva y el nuevo gana una
        // identidad derivada (ambos trazables al paquete).
        var suffix = 2;
        string derivedKey;
        do
        {
            derivedKey = $"{itemKey}#copia{suffix}";
            suffix++;
        }
        while (await db.ContentItems.AnyAsync(
            x => x.PackKey == packKey && x.PackItemKey == derivedKey,
            cancellationToken));

        return await CreateImportedAsync(
            await db.ContentPacks.FirstAsync(x => x.Key == packKey, cancellationToken),
            derivedKey, type, name, request, properties, inheritance,
            cancellationToken);
    }

    // --- Utilidades -----------------------------------------------------

    /// <summary>Snapshot de publicación del item (payload de revisión).</summary>
    private static string PayloadOf(ContentItem item) => new JsonObject
    {
        ["name"] = item.Name,
        ["summary"] = item.Summary,
        ["description"] = item.Description,
        ["tags"] = JsonNode.Parse(item.TagsJson),
        ["properties"] = JsonNode.Parse(item.PropertiesJson),
        ["inheritance"] = JsonNode.Parse(item.InheritanceJson),
        ["packKey"] = item.PackKey,
        ["packItemKey"] = item.PackItemKey,
    }.ToJsonString();

    private static string FirstImportMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) ? "Importación inicial" : message.Trim();

    private static Dictionary<string, string> NormalizeInheritance(
        Dictionary<string, string>? inheritance)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, mode) in inheritance ?? [])
        {
            normalized[key] = mode switch
            {
                "inherit" or "override" or "extend" => mode,
                _ => throw ApiException.BadRequest(
                    $"El modo de herencia '{mode}' no es válido (inherit|override|extend)."),
            };
        }
        return normalized;
    }

    private static string ToJsonArray(string[]? values) =>
        new JsonArray((values ?? []).Select(v => (JsonNode?)JsonValue.Create(v)).ToArray())
            .ToJsonString();

    private static string ToJsonObject(Dictionary<string, string> values)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values)
        {
            obj[key] = value;
        }
        return obj.ToJsonString();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, maxLength)];
    }

    private static ContentPackDto ToDto(ContentPack pack) => new(
        pack.Id, pack.Key, pack.Name, pack.Version, pack.System,
        pack.SourceUrl, pack.License, pack.LicenseUrl,
        ParseArray(pack.AttributionJson), pack.ImportedAt);

    private static string[] ParseArray(string json)
    {
        if (JsonNode.Parse(json) is not JsonArray array)
        {
            return [];
        }
        return array
            .Select(v => v?.GetValue<string>() ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToArray();
    }
}
