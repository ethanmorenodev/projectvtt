namespace ProjectVTT.Api.Domain;

/// <summary>
/// Relación tipada entre items (ADR-031 §8): `kind` lo define el sistema
/// (grants, requires, excludes, memberOf, locatedIn, leadsTo, owns…). Alimenta
/// requisitos del creador, el grafo del Character Hub y la navegación de lore.
/// </summary>
public class ContentRelation
{
    public Guid Id { get; set; }
    public Guid FromItemId { get; set; }
    public ContentItem FromItem { get; set; } = null!;

    /// <summary>Tipo de relación, definido por el sistema.</summary>
    public required string Kind { get; set; }

    public Guid ToItemId { get; set; }
    public ContentItem ToItem { get; set; } = null!;
    public string? Label { get; set; }

    /// <summary>Metadatos JSON opcionales.</summary>
    public string? MetadataJson { get; set; }
}
