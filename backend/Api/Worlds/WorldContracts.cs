using System.Text.Json.Nodes;

namespace ProjectVTT.Api.Worlds;

public sealed record WorldDto(
    Guid Id, string System, string Name, string? Description,
    Guid OwnerUserId, DateTime CreatedAt);

public sealed record CreateWorldRequest(
    string? System, string? Name, string? Description);

public sealed record LinkWorldCampaignRequest(Guid? CampaignId);

/// <summary>Usar contenido del mundo en una campaña con revisión fijada.</summary>
public sealed record UseWorldItemRequest(Guid? CampaignId, long? Revision);

/// <summary>Override local de una campaña: solo claves cambiadas.</summary>
public sealed record WorldOverrideRequest(Guid? CampaignId, JsonNode? Patch);

public sealed record WorldCampaignRequest(Guid? CampaignId);

/// <summary>
/// Actualización explícita del mundo en una campaña: `apply` adopta la
/// revisión indicada; `keep` conserva la fijada (nunca silenciosa).
/// </summary>
public sealed record UpdateWorldItemRequest(
    Guid? CampaignId, long? Revision, string? Mode);

public sealed record WorldEffectiveDto(
    Guid ItemId, long Revision, JsonNode Properties, JsonNode? Override);
