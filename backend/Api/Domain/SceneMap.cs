namespace ProjectVTT.Api.Domain;

/// <summary>
/// Mapa persistido de una Escena (Épica D/Épica H, ADR-028): metadatos
/// del archivo subido (un mapa por escena; subir otro lo reemplaza). El
/// contenido vive en el <c>IAssetStore</c> bajo la clave
/// "scene-maps/{SceneId}".
/// </summary>
public class SceneMap
{
    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
