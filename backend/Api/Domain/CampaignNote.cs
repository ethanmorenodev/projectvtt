namespace ProjectVTT.Api.Domain;

/// <summary>
/// Nota privada del GM de una campaña (Épica F, ADR-019): título y
/// cuerpo que solo leen y editan GM u Owner. Tabla relacional con FK a
/// la campaña (cascade), según ARCHITECTURE §8. Sin evento en vivo: la
/// UI vive en la página de campaña y se actualiza por petición.
/// </summary>
public class CampaignNote
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public required string Title { get; set; }
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
