namespace ProjectVTT.Api.History;

/// <summary>
/// Entrada del historial con el nombre del usuario que hizo el cambio
/// (resuelto al leer).
/// </summary>
public sealed record HistoryEntryDto(
    Guid Id,
    Guid CampaignId,
    Guid UserId,
    string UserName,
    string Kind,
    string Summary,
    DateTime CreatedAt);

/// <summary>
/// Entrada de actividad reciente multi-campaña (PMB-2.2, Inicio): incluye
/// el nombre de la campaña además del autor.
/// </summary>
public sealed record ActivityEntryDto(
    Guid CampaignId,
    string CampaignName,
    string UserName,
    string Kind,
    string Summary,
    DateTime CreatedAt);
