using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Lore (PMB-7.3): facciones, localizaciones y eventos como contenido,
/// unidos por `ContentRelation` (ADR-031 §8). Los secretos del DM se
/// aplican en servidor: ningún nodo ni arista se filtra en el cliente.
/// </summary>
public sealed class LoreService(AppDbContext db, ContentService content)
{
    private static readonly string[] LoreTypes = ["faction", "location", "event"];

    public async Task<LoreGraphDto> GraphAsync(
        Guid? campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = db.ContentItems.AsNoTracking()
            .Include(item => item.Type)
            .Where(item => LoreTypes.Contains(item.Type.Key));
        if (campaignId.HasValue)
        {
            query = query.Where(item => item.CampaignId == campaignId.Value
                || item.Scope == ContentScope.Global || item.Scope == ContentScope.World);
        }
        var candidates = await query.ToListAsync(cancellationToken);
        var visible = new List<ContentItem>();
        foreach (var item in candidates)
        {
            if (await content.IsVisibleAsync(item.Id, userId, cancellationToken))
            {
                visible.Add(item);
            }
        }
        var visibleIds = visible.Select(item => item.Id).ToHashSet();
        var relations = await db.ContentRelations.AsNoTracking()
            .Where(relation => visibleIds.Contains(relation.FromItemId)
                && visibleIds.Contains(relation.ToItemId))
            .OrderBy(relation => relation.Kind)
            .ToListAsync(cancellationToken);

        return new LoreGraphDto(
            visible.Select(item => new LoreNodeDto(
                item.Id, item.Name, item.Type.Key,
                item.Summary, item.IsPrivate)).ToList(),
            relations.Select(relation => new LoreEdgeDto(
                relation.Id, relation.FromItemId, relation.Kind,
                relation.ToItemId, relation.Label)).ToList());
    }

    public async Task<List<LoreEventDto>> TimelineAsync(
        Guid? campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = db.ContentItems.AsNoTracking()
            .Include(item => item.Type)
            .Where(item => item.Type.Key == "event");
        if (campaignId.HasValue)
        {
            query = query.Where(item => item.CampaignId == campaignId.Value
                || item.Scope == ContentScope.Global || item.Scope == ContentScope.World);
        }
        var candidates = await query.ToListAsync(cancellationToken);
        var events = new List<(ContentItem Item, JsonObject Properties)>();
        foreach (var item in candidates)
        {
            if (!await content.IsVisibleAsync(item.Id, userId, cancellationToken))
            {
                continue;
            }
            events.Add((item, JsonNode.Parse(item.PropertiesJson) as JsonObject ?? []));
        }
        return events
            .OrderBy(entry => entry.Properties["order"]?.GetValue<long>() ?? 0)
            .ThenBy(entry => entry.Properties["happenedAt"]?.GetValue<string>() ?? string.Empty)
            .Select(entry => new LoreEventDto(
                entry.Item.Id, entry.Item.Name, entry.Item.Summary,
                entry.Properties["happenedAt"]?.GetValue<string>(),
                entry.Properties["order"]?.GetValue<long>() ?? 0))
            .ToList();
    }
}
