namespace ProjectVTT.Api.Assets;

/// <summary>Metadatos del mapa persistido de una escena.</summary>
public sealed record MapAssetDto(
    Guid SceneId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UpdatedAt);
