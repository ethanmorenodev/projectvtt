namespace ProjectVTT.Api.Domain;

/// <summary>
/// Mundo reutilizable (ADR-032/042): colección de contenido de ámbito
/// `world` que puede usarse en varias campañas sin contaminarlas.
/// </summary>
public class World
{
    public Guid Id { get; set; }
    public required string System { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Modificación local de un contenido del mundo en una campaña (ADR-042):
/// solo las claves modificadas, sobre una revisión base fijada.
/// </summary>
public class WorldContentOverride
{
    public Guid Id { get; set; }
    public Guid WorldId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ContentItemId { get; set; }
    public long BaseRevision { get; set; }
    public required string PatchJson { get; set; }
    public DateTime UpdatedAt { get; set; }
}
