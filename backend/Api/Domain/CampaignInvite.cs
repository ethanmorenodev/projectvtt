namespace ProjectVTT.Api.Domain;

/// <summary>
/// Invitación a una campaña: código de un solo uso por usuario (un mismo
/// código puede usarse varias veces hasta que caduque o se revoque).
/// </summary>
public class CampaignInvite
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public required string Code { get; set; }
    public CampaignRole Role { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
