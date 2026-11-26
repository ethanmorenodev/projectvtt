namespace ProjectVTT.Api.Domain;

/// <summary>
/// Revisión inmutable de un <see cref="ContentItem"/> (ADR-031 §7): cada
/// publicación crea una con el payload completo del item. Los personajes
/// fijan la revisión usada vía <see cref="ItemUsage"/>; actualizar nunca es
/// silencioso (diff + decisión explícita).
/// </summary>
public class ContentRevision
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public ContentItem Item { get; set; } = null!;
    public long Number { get; set; }

    /// <summary>Snapshot completo del item al publicar (JSON, inmutable).</summary>
    public required string PayloadJson { get; set; }

    /// <summary>Mensaje de cambio opcional.</summary>
    public string? Message { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
