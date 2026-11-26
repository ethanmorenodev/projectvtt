namespace ProjectVTT.Api.Shared;

/// <summary>
/// Validación compartida de imágenes subidas (mapas, portadas…): tipo,
/// tamaño y firma binaria. Extraída de `MapService` para reutilizarla sin
/// duplicar reglas (las mismas que valida la web).
/// </summary>
public static class ImageGuard
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>Tipos admitidos (los mismos que valida la web).</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/svg+xml",
    };

    /// <summary>Límite de tamaño por defecto (configurable con `Assets:MaxBytes`).</summary>
    public const int DefaultMaxBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Valida tipo, tamaño y firma; rebobina el flujo si puede para que el
    /// llamador guarde el archivo completo.
    /// </summary>
    public static async Task ValidateAsync(
        string contentType, long sizeBytes, Stream content, long maxBytes,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw ApiException.BadRequest(
                $"Tipo no permitido: '{contentType}'. Usa PNG, JPEG, WebP o SVG.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }

        if (sizeBytes <= 0 || sizeBytes > maxBytes)
        {
            throw ApiException.BadRequest(
                $"El archivo debe pesar entre 1 byte y {maxBytes} bytes.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }

        // Firma binaria: un archivo que dice ser PNG/JPEG/WebP debe serlo.
        var header = new byte[12];
        var read = await content.ReadAsync(header, cancellationToken);
        if (!HasExpectedSignature(contentType, header.AsSpan(0, read)))
        {
            throw ApiException.BadRequest(
                "El contenido del archivo no corresponde con su tipo declarado.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }

        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }
    }

    private static bool HasExpectedSignature(string contentType, ReadOnlySpan<byte> header) =>
        contentType switch
        {
            "image/png" => header.StartsWith(PngSignature),
            "image/jpeg" => header.StartsWith(JpegSignature),
            "image/webp" => header.Length >= 12
                && header[..4].SequenceEqual("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8),
            // SVG: texto; basta con que aparezca la raíz <svg en la cabecera.
            "image/svg+xml" => System.Text.Encoding.UTF8.GetString(header)
                .Contains("<svg", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
}
