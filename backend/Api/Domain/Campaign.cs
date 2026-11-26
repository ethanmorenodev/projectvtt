namespace ProjectVTT.Api.Domain;

/// <summary>Campaña de juego compartida entre usuarios.</summary>
public class Campaign
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Descripción opcional (PMB-2.4; aditiva, nullable).</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tipo de contenido de la portada (PMB-2.4; aditivo, nullable). El
    /// archivo vive en `IAssetStore` bajo `campaign-covers/{id}`; sin
    /// portada la UI muestra la imagen alternativa (inicial).
    /// </summary>
    public string? CoverContentType { get; set; }

    /// <summary>Mundo reutilizable al que pertenece la campaña (ADR-042).</summary>
    public Guid? WorldId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public List<CampaignMember> Members { get; set; } = [];
    public List<CampaignInvite> Invites { get; set; } = [];
}
