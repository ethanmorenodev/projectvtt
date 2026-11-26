using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Worlds;

/// <summary>
/// Mundos reutilizables (ADR-032/042): las campañas usan contenido del
/// mundo con revisión fijada, pueden overridearlo localmente o
/// independizarlo como copia; las actualizaciones nunca son silenciosas.
/// </summary>
public sealed class WorldService(AppDbContext db)
{
    public async Task<WorldDto> CreateAsync(
        Guid userId, CreateWorldRequest request, CancellationToken cancellationToken = default)
    {
        var system = InputGuard.RequiredText(request.System, "system", maxLength: 50);
        var name = InputGuard.RequiredText(request.Name, "name", maxLength: 100);
        var world = new World
        {
            Id = Guid.NewGuid(), System = system, Name = name,
            Description = request.Description, OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Worlds.Add(world);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(world);
    }

    public async Task<List<WorldDto>> ListAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await db.Worlds.AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new WorldDto(
                x.Id, x.System, x.Name, x.Description, x.OwnerUserId, x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task LinkCampaignAsync(
        Guid worldId, Guid userId, LinkWorldCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        var world = await OwnedWorldAsync(worldId, userId, cancellationToken);
        var campaignId = request.CampaignId
            ?? throw ApiException.BadRequest("Falta el campo 'campaignId'.");
        var campaign = await db.Campaigns.FirstOrDefaultAsync(
            x => x.Id == campaignId, cancellationToken)
            ?? throw ApiException.NotFound("La campaña no existe.");
        if (campaign.CreatedByUserId != userId
            && !await db.CampaignMembers.AnyAsync(
                x => x.CampaignId == campaignId && x.UserId == userId
                    && x.Role == CampaignRole.GM, cancellationToken))
        {
            throw ApiException.Forbidden("Solo el DM o el creador enlaza la campaña al mundo.");
        }
        campaign.WorldId = world.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Fija una revisión del mundo para la campaña (ADR-042).</summary>
    public async Task<ContentItemDto> UseInCampaignAsync(
        Guid itemId, Guid userId, UseWorldItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await WorldItemAsync(itemId, cancellationToken);
        var link = await LinkAsync(item, request.CampaignId, userId, cancellationToken);
        var revision = request.Revision ?? item.CurrentRevision;
        if (revision < 1 || revision > item.CurrentRevision)
        {
            throw ApiException.BadRequest("La revisión no existe en el mundo.");
        }
        _ = link;
        var id = link.Id;
        // La revisión fijada queda registrada como override base de la
        // campaña; las creaciones de usos de personaje siguen fijando su
        // propia revisión en ItemUsage (ADR-031 §7).
        var existing = await db.WorldContentOverrides.FirstOrDefaultAsync(
            x => x.WorldId == item.WorldId && x.CampaignId == id
                && x.ContentItemId == itemId, cancellationToken);
        if (existing is null)
        {
            db.WorldContentOverrides.Add(new WorldContentOverride
            {
                Id = Guid.NewGuid(), WorldId = item.WorldId!.Value,
                CampaignId = id, ContentItemId = itemId,
                BaseRevision = revision, PatchJson = "{}", UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        return new ContentItemDto(
            item.Id, item.System, item.TypeId, string.Empty, item.Name,
            item.Summary, item.Description, item.ParentId, [], item.ImageId, item.Focal,
            JsonNode.Parse(item.PropertiesJson) as JsonObject ?? [],
            ParseInheritance(item.InheritanceJson), "world", item.CampaignId,
            item.WorkflowState.ToString().ToLowerInvariant(), item.CurrentRevision,
            item.CreatedByUserId, item.CreatedAt, item.UpdatedAt,
            null, null, false, [], 1);
    }

    /// <summary>Override local: solo las claves enviadas por el DM.</summary>
    public async Task<WorldEffectiveDto> SetOverrideAsync(
        Guid itemId, Guid userId, WorldOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await WorldItemAsync(itemId, cancellationToken);
        await LinkAsync(item, request.CampaignId, userId, cancellationToken);
        var patch = request.Patch as JsonObject
            ?? throw ApiException.BadRequest("El campo 'patch' debe ser un objeto.");
        var allowed = ContentSchema.ParseFields(
            JsonNode.Parse((await db.ContentTypes.FindAsync([item.TypeId], cancellationToken))!
                .SchemaJson));
        if (patch.Any(entry => !allowed.ContainsKey(entry.Key)))
        {
            throw ApiException.BadRequest("El override incluye claves desconocidas para el tipo.");
        }

        var campaignId = request.CampaignId!.Value;
        var existing = await db.WorldContentOverrides.FirstOrDefaultAsync(
            x => x.WorldId == item.WorldId && x.CampaignId == campaignId
                && x.ContentItemId == itemId, cancellationToken);
        if (existing is null)
        {
            existing = new WorldContentOverride
            {
                Id = Guid.NewGuid(), WorldId = item.WorldId!.Value,
                CampaignId = campaignId, ContentItemId = itemId,
                PatchJson = patch.ToJsonString(), UpdatedAt = DateTime.UtcNow,
            };
            db.WorldContentOverrides.Add(existing);
        }
        else
        {
            existing.PatchJson = patch.ToJsonString();
            existing.BaseRevision = item.CurrentRevision;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Effective(item, existing);
    }

    /// <summary>Propiedades efectivas para la campaña: base + override.</summary>
    public async Task<WorldEffectiveDto> GetEffectiveAsync(
        Guid itemId, Guid? campaignId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var item = await WorldItemAsync(itemId, cancellationToken);
        await LinkAsync(item, campaignId, userId, cancellationToken);
        var effective = JsonNode.Parse(item.PropertiesJson) as JsonObject ?? [];
        var overrideEntity = await db.WorldContentOverrides.AsNoTracking().FirstOrDefaultAsync(
            x => x.CampaignId == campaignId && x.ContentItemId == itemId, cancellationToken);
        if (overrideEntity is not null
            && JsonNode.Parse(overrideEntity.PatchJson) is JsonObject patch)
        {
            foreach (var (key, value) in patch)
            {
                effective[key] = value?.DeepClone();
            }
        }
        return new WorldEffectiveDto(
            item.Id, overrideEntity?.BaseRevision ?? item.CurrentRevision,
            effective, overrideEntity is null ? null : JsonNode.Parse(overrideEntity.PatchJson));
    }

    /// <summary>Independizar: copia del mundo al ámbito de la campaña.</summary>
    public async Task<ContentItemDto> IndependentizeAsync(
        Guid itemId, Guid userId, WorldCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await WorldItemAsync(itemId, cancellationToken);
        await LinkAsync(item, request.CampaignId, userId, cancellationToken);
        var copy = new ContentItem
        {
            Id = Guid.NewGuid(), System = item.System, TypeId = item.TypeId,
            Name = item.Name, Summary = item.Summary, Description = item.Description,
            ParentId = null, TagsJson = item.TagsJson, ImageId = item.ImageId,
            Focal = item.Focal, PropertiesJson = item.PropertiesJson,
            InheritanceJson = item.InheritanceJson, Scope = ContentScope.Campaign,
            OwnerUserId = null, CampaignId = request.CampaignId, WorldId = null,
            SourceWorldItemId = item.Id, SourceRevision = item.CurrentRevision,
            IsPrivate = false, PrivateFieldsJson = "[]",
            WorkflowState = ContentWorkflowState.Draft, CurrentRevision = 0,
            CreatedByUserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.ContentItems.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return new ContentItemDto(
            copy.Id, copy.System, copy.TypeId, string.Empty, copy.Name,
            copy.Summary, copy.Description, copy.ParentId, [], copy.ImageId, copy.Focal,
            JsonNode.Parse(copy.PropertiesJson) as JsonObject ?? [], [], "campaign",
            copy.CampaignId, "draft", 0, userId, copy.CreatedAt, copy.UpdatedAt,
            null, null, false, [], 0);
    }

    /// <summary>Actualización explícita: adoptar una revisión o conservar la actual.</summary>
    public async Task<WorldEffectiveDto> UpdateInCampaignAsync(
        Guid itemId, Guid userId, UpdateWorldItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await WorldItemAsync(itemId, cancellationToken);
        await LinkAsync(item, request.CampaignId, userId, cancellationToken);
        var mode = request.Mode?.Trim().ToLowerInvariant();
        if (mode is not ("apply" or "keep"))
        {
            throw ApiException.BadRequest("'mode' debe ser 'apply' o 'keep'.");
        }
        if (mode == "keep")
        {
            return await GetEffectiveAsync(itemId, request.CampaignId, userId, cancellationToken);
        }
        var revision = request.Revision
            ?? throw ApiException.BadRequest("Falta la revisión a aplicar.");
        if (revision < 1 || revision > item.CurrentRevision)
        {
            throw ApiException.BadRequest("La revisión no existe en el mundo.");
        }
        var existing = await db.WorldContentOverrides.FirstOrDefaultAsync(
            x => x.CampaignId == request.CampaignId && x.ContentItemId == itemId,
            cancellationToken);
        if (existing is not null)
        {
            existing.BaseRevision = revision;
            existing.PatchJson = "{}";
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return new WorldEffectiveDto(
            item.Id, revision,
            JsonNode.Parse(item.PropertiesJson) as JsonObject ?? [],
            existing is null ? null : JsonNode.Parse(existing.PatchJson));
    }

    // --- Utilidades ------------------------------------------------------

    private async Task<World> OwnedWorldAsync(
        Guid worldId, Guid userId, CancellationToken cancellationToken) =>
        await db.Worlds.FirstOrDefaultAsync(
            x => x.Id == worldId && x.OwnerUserId == userId, cancellationToken)
            ?? throw ApiException.NotFound("El mundo no existe.");

    private async Task<ContentItem> WorldItemAsync(
        Guid itemId, CancellationToken cancellationToken) =>
        await db.ContentItems.FirstOrDefaultAsync(
            x => x.Id == itemId && x.Scope == ContentScope.World && x.WorldId != null,
            cancellationToken)
            ?? throw ApiException.NotFound("El contenido del mundo no existe.");

    private async Task<Campaign> LinkAsync(
        ContentItem item, Guid? campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var id = campaignId ?? throw ApiException.BadRequest("Falta el campo 'campaignId'.");
        var world = await db.Worlds.FirstOrDefaultAsync(
            x => x.Id == item.WorldId, cancellationToken)
            ?? throw ApiException.NotFound("El mundo no existe.");
        if (world.OwnerUserId != userId)
        {
            throw ApiException.Forbidden("Solo el dueño del mundo gestiona su contenido.");
        }
        var campaign = await db.Campaigns.FirstOrDefaultAsync(
            x => x.Id == id && x.WorldId == world.Id, cancellationToken)
            ?? throw ApiException.NotFound("La campaña no está enlazada a este mundo.");
        return campaign;
    }

    private static WorldEffectiveDto Effective(ContentItem item, WorldContentOverride ov)
    {
        var properties = JsonNode.Parse(item.PropertiesJson) as JsonObject ?? [];
        var patch = JsonNode.Parse(ov.PatchJson);
        if (patch is JsonObject overrides)
        {
            foreach (var (key, value) in overrides)
            {
                properties[key] = value?.DeepClone();
            }
        }
        return new WorldEffectiveDto(item.Id, ov.BaseRevision, properties, patch);
    }

    private static Dictionary<string, string> ParseInheritance(string json)
    {
        if (string.IsNullOrWhiteSpace(json)
            || JsonNode.Parse(json) is not JsonObject node)
        {
            return [];
        }
        return node.ToDictionary(
            entry => entry.Key,
            entry => entry.Value?.GetValue<string>() ?? "inherit");
    }

    private static WorldDto ToDto(World world) => new(
        world.Id, world.System, world.Name, world.Description,
        world.OwnerUserId, world.CreatedAt);
}
