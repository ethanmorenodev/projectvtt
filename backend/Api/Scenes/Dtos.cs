namespace ProjectVTT.Api.Scenes;

public sealed record SceneSummaryDto(
    Guid Id, Guid CampaignId, string Name, int GridCellSize, string GridShape, DateTime CreatedAt,
    int TokenCount, bool HasCover);

public sealed record SceneDetailDto(
    Guid Id, Guid CampaignId, string Name, int GridCellSize, string GridShape, DateTime CreatedAt,
    bool HasCover);

public sealed record TokenDto(
    Guid Id, Guid SceneId, string Name, double X, double Y,
    long Version, Guid? UpdatedByUserId, DateTime UpdatedAt, Guid? ActorId);

public sealed record CreateSceneRequest(string? Name);

/// <summary>
/// Configuración del grid: el tamaño de celda sigue siendo obligatorio;
/// la forma (`square`/`hex`) es opcional y solo cambia si se envía.
/// </summary>
public sealed record UpdateSceneRequest(int? GridCellSize, string? GridShape);

public sealed record CreateTokenRequest(string? Name, double? X, double? Y, Guid? ActorId);
