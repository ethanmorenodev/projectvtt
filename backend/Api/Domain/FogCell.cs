namespace ProjectVTT.Api.Domain;

/// <summary>
/// Celda REVELADA de la niebla de guerra de una Escena (Épica F,
/// ADR-020): lo que no está en esta tabla queda oculto para los
/// jugadores. Clave compuesta (SceneId, X, Y) con las coordenadas de
/// celda respecto a `GridCellSize` de la escena; en cascade con la
/// escena.
/// </summary>
public class FogCell
{
    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public int X { get; set; }
    public int Y { get; set; }
}
