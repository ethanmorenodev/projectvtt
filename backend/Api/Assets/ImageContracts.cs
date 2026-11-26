namespace ProjectVTT.Api.Assets;

/// <summary>
/// Imagen de la biblioteca (PMB-4.7, ADR-035/036). El archivo vive en
/// <c>IAssetStore</c>; aquí solo metadatos y procedencia.
/// </summary>
public sealed record ImageAssetDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Role,
    string[] Albums,
    string[] Tags,
    string License,
    string? LicenseUrl,
    string Author,
    string Origin,
    string[] Attribution,
    string? Focal,
    string? PackKey,
    DateTime CreatedAt,

    /// <summary>Referencias desde items (`ContentItem.ImageId`), PMB-4.10.</summary>
    int ReferenceCount,

    /// <summary>Claves de variantes derivadas (thumb-sm/md/lg, PMB-4.10).</summary>
    string[] Variants);

public sealed record UpdateImageAssetRequest(
    string? Role,
    string[]? Albums,
    string[]? Tags,
    string? License,
    string? LicenseUrl,
    string? Author,
    string? Origin,
    string[]? Attribution,
    string? Focal);

/// <summary>Datos opcionales que acompañan al archivo en la subida.</summary>
public sealed record UploadImageAssetRequest(
    string? Role,
    string[]? Albums,
    string[]? Tags,
    string? License,
    string? LicenseUrl,
    string? Author,
    string? Origin,
    string[]? Attribution,
    string? Focal);
