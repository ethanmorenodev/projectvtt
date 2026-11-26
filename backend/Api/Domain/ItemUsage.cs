namespace ProjectVTT.Api.Domain;

/// <summary>
/// Uso de contenido por un personaje (ADR-031 §7): fija la revisión elegida
/// y materializa las propiedades efectivas (herencia resuelta) en un
/// snapshot. Así, editar el contenido nunca rompe personajes existentes:
/// cada uno decide si actualizar o conservar.
/// </summary>
public class ItemUsage
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Actor Actor { get; set; } = null!;

    /// <summary>
    /// Item usado. Sin navegación ni borrado en cascada a propósito: el
    /// historial de un personaje se conserva aunque el contenido cambie.
    /// </summary>
    public Guid ItemId { get; set; }

    public long RevisionNumber { get; set; }

    /// <summary>Propiedades efectivas del momento (JSON).</summary>
    public required string SnapshotJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
