namespace ProjectVTT.Api.Domain;

/// <summary>Pertenencia de un usuario a una campaña, con su rol.</summary>
public class CampaignMember
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public CampaignRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}
