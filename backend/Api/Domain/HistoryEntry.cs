namespace ProjectVTT.Api.Domain;

/// <summary>
/// Entrada del historial de cambios de una campaña (Épica F, ADR-021):
/// registro append-only de acciones sobre el estado compartido (escenas,
/// tokens, fichas, inventario, efectos, tiradas y combate). Sin notas ni
/// mensajes privados (no deben filtrarse). El texto del resumen ya está
/// redactado para mostrar.
/// </summary>
public class HistoryEntry
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Kind { get; set; }
    public required string Summary { get; set; }
    public DateTime CreatedAt { get; set; }
}
