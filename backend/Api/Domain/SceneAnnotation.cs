namespace ProjectVTT.Api.Domain;

/// <summary>
/// Anotación compartida de una Escena (ADR-043): trazo ('draw') o nota con
/// texto ('note'). Los puntos se guardan como JSON portable. La versión es
/// la de la anotación dentro de la escena; la versión del estado de la
/// escena es la máxima de sus anotaciones + 1 (0 si no hay ninguna).
/// Al borrar la escena desaparecen sus anotaciones.
/// </summary>
public class SceneAnnotation
{
    public Guid Id { get; set; }
    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public required string Kind { get; set; }
    public required string PointsJson { get; set; }
    public string? Text { get; set; }
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
