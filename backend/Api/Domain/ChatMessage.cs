namespace ProjectVTT.Api.Domain;

/// <summary>
/// Mensaje del chat de campaña (Épica F): texto libre enviado por un
/// miembro. Tabla relacional con FK a la campaña (cascade) e índice por
/// campaña+fecha, según ARCHITECTURE §8. No lleva escena: el chat es de
/// campaña, no de mapa.
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; }
}
