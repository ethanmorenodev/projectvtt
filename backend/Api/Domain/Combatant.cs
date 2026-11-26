namespace ProjectVTT.Api.Domain;

/// <summary>
/// Combatiente del encuentro de una Escena (Épica F, ADR-018): nombre,
/// iniciativa manual y vinculación opcional a un token del mapa. El
/// orden de turno es por iniciativa descendente; el servidor lo
/// recalcula en cada consulta (sin columna de orden).
/// </summary>
public class Combatant
{
    public Guid Id { get; set; }
    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public required string Name { get; set; }
    public int Initiative { get; set; }
    public Guid? TokenId { get; set; }
    public Token? Token { get; set; }
    public DateTime CreatedAt { get; set; }
}
