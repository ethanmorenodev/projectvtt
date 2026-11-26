namespace ProjectVTT.Api.Domain;

/// <summary>
/// Mensaje privado 1:1 entre dos miembros de una campaña (Épica F,
/// ADR-017): whisper dentro de la campaña, no una mensajería global.
/// Tabla relacional con FK a campaña y a los dos usuarios (cascade) e
/// índice por campaña + participantes + fecha, según ARCHITECTURE §8.
/// </summary>
public class DirectMessage
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid FromUserId { get; set; }
    public User FromUser { get; set; } = null!;
    public Guid ToUserId { get; set; }
    public User ToUser { get; set; } = null!;
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; }
}
