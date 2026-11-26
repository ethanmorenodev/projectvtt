namespace ProjectVTT.Api.Domain;

/// <summary>
/// Imagen compartida de la biblioteca (ADR-035 §10): el archivo vive en
/// <c>IAssetStore</c> y aquí solo referencias (clave de almacenamiento +
/// variantes cuadradas derivadas 64/128/256). Los items la referencian por
/// <c>ImageId</c> sin duplicar archivos. La procedencia (ADR-036) se
/// completa al importar paquetes (PMB-P.8).
/// </summary>
public class ImageAsset
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>
    /// Papel visual (ADR-035 §3.5 / PMB-4.7): `icono`, `miniatura` o
    /// `ilustracion`. Determina el tratamiento en rejillas y lightbox.
    /// </summary>
    public string Role { get; set; } = "miniatura";

    /// <summary>Clave de almacenamiento del original (IAssetStore, ADR-028).</summary>
    public required string StorageKey { get; set; }

    /// <summary>
    /// Identidad importada (ADR-036): <c>(PackKey, PackItemKey)</c> para
    /// reimportaciones idempotentes.
    /// </summary>
    public string? PackItemKey { get; set; }

    /// <summary>Punto focal para el recorte "cover" (p. ej. "50,30").</summary>
    public string? Focal { get; set; }

    /// <summary>Etiquetas JSON (string[]).</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>Álbumes JSON (string[]).</summary>
    public string AlbumsJson { get; set; } = "[]";

    /// <summary>
    /// Variantes derivadas JSON ({ "thumb-sm"|"thumb-md"|"thumb-lg": clave
    /// de almacenamiento }), generadas en cliente y validadas en servidor.
    /// </summary>
    public required string VariantsJson { get; set; }

    // --- Procedencia (ADR-036; se completa al importar paquetes, PMB-P.8) ---
    public string? PackKey { get; set; }
    public string? License { get; set; }
    public string? LicenseUrl { get; set; }

    /// <summary>Atribuciones JSON (string[]).</summary>
    public string? AttributionJson { get; set; }

    public string? Origin { get; set; }
    public string? Author { get; set; }

    public DateTime CreatedAt { get; set; }
}
