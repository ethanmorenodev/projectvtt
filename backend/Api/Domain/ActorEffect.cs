namespace ProjectVTT.Api.Domain;

/// <summary>
/// Efecto o condición genérica aplicada a un Actor (Épica E): solo nombre,
/// sin duración (decisión del propietario 2026-11-06: los turnos que la
/// decrementarían llegan en la Épica F, ver ADR-014). Es una tabla
/// relacional, no JSON, según ARCHITECTURE §8; la interpretación del
/// efecto (a qué modifica, cuánto dura) es trabajo del Ruleset.
/// </summary>
public class ActorEffect
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Actor Actor { get; set; } = null!;
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
