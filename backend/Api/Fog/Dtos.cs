namespace ProjectVTT.Api.Fog;

/// <summary>Celda revelada de la niebla (coordenadas de celda).</summary>
public sealed record FogCellDto(int X, int Y);

/// <summary>
/// Estado de la niebla de una Escena: las celdas reveladas (lo demás
/// está oculto). El tamaño de celda es el `GridCellSize` de la escena.
/// </summary>
public sealed record FogStateDto(
    Guid SceneId,
    int GridCellSize,
    IReadOnlyList<FogCellDto> Revealed);

/// <summary>Conmutar una celda: revelar u ocultar.</summary>
public sealed record SetFogCellRequest(int? X, int? Y);
