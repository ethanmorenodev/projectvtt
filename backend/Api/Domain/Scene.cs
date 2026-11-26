namespace ProjectVTT.Api.Domain;

/// <summary>Formas del grid de una escena (ADR-023).</summary>
public static class GridShapes
{
    public const string Square = "square";
    public const string Hex = "hex";
}

/// <summary>Escena de juego dentro de una campaña (mapa base sobre el que hay tokens).</summary>
public class Scene
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public required string Name { get; set; }
    public int GridCellSize { get; set; } = 32;

    /// <summary>Forma del grid: "square" (por defecto) o "hex" (flat-top).</summary>
    public string GridShape { get; set; } = GridShapes.Square;

    /// <summary>
    /// Tipo MIME de la portada opcional; el archivo vive en `IAssetStore`
    /// bajo `scene-covers/{id}`. Aditivo y portable entre proveedores.
    /// </summary>
    public string? CoverContentType { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Token> Tokens { get; set; } = [];
}
