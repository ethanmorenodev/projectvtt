namespace ProjectVTT.Api.Domain;

/// <summary>
/// Ítem del inventario de un Actor: nombre y cantidad, genéricos para
/// cualquier sistema de rol (PROJECT_SPEC §3, multisistema). Es una tabla
/// relacional, no JSON, según ARCHITECTURE §8. <see cref="Version"/> es el
/// testigo de concurrencia optimista, igual que en <see cref="Actor"/> y
/// <see cref="Token"/>.
/// </summary>
public class ActorItem
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Actor Actor { get; set; } = null!;
    public required string Name { get; set; }
    public int Quantity { get; set; }

    /// <summary>Hueco de equipamiento (ADR-041): null = mochila.</summary>
    public string? SlotId { get; set; }

    /// <summary>Referencia opcional al objeto del catálogo (ContentItem).</summary>
    public Guid? ContentItemId { get; set; }

    /// <summary>Peso/volumen heredados del objeto de contenido (opcionales).</summary>
    public double? Weight { get; set; }
    public double? Volume { get; set; }

    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
