using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Campaigns;

/// <summary>
/// Campañas, membresías e invitaciones. Toda operación comprueba el acceso
/// en el servidor: un no miembro recibe 404 (sin revelar que la campaña
/// existe) y un miembro sin permiso suficiente recibe 403.
/// </summary>
public sealed class CampaignService(AppDbContext db, IAssetStore assets)
{
    private const int DefaultInviteDays = 7;
    private const int MaxInviteDays = 30;

    // --- Campañas -------------------------------------------------------

    public async Task<CampaignDetailDto> CreateAsync(Guid userId, string? name, string? description = null,
        CancellationToken cancellationToken = default)
    {
        var cleanName = InputGuard.RequiredText(name, "name", maxLength: 100);
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = cleanName,
            Description = CleanDescription(description),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };

        db.Campaigns.Add(campaign);
        db.CampaignMembers.Add(new CampaignMember
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            UserId = userId,
            Role = CampaignRole.Owner,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new CampaignDetailDto(
            campaign.Id, campaign.Name, CampaignRole.Owner, campaign.CreatedAt, null,
            MemberCount: 1, campaign.Description, HasCover: false);
    }

    public async Task<IReadOnlyList<CampaignSummaryDto>> ListAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await db.CampaignMembers
            .Where(m => m.UserId == userId)
            .Select(m => new { m.Role, m.Campaign })
            .OrderByDescending(x => x.Campaign.CreatedAt)
            .ToListAsync(cancellationToken);

        // Última actividad real por campaña (historial); sin actividad → null.
        var ids = memberships.Select(m => m.Campaign.Id).ToList();
        var lastByCampaign = await db.HistoryEntries
            .Where(h => ids.Contains(h.CampaignId))
            .GroupBy(h => h.CampaignId)
            .Select(g => new { g.Key, Last = g.Max(h => h.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => (DateTime?)x.Last, cancellationToken);
        var membersByCampaign = await db.CampaignMembers
            .Where(m => ids.Contains(m.CampaignId))
            .GroupBy(m => m.CampaignId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return memberships
            .Select(m => new CampaignSummaryDto(
                m.Campaign.Id, m.Campaign.Name, m.Role, m.Campaign.CreatedAt, m.Campaign.ArchivedAt,
                m.Campaign.Description, m.Campaign.CoverContentType is not null,
                lastByCampaign.GetValueOrDefault(m.Campaign.Id),
                membersByCampaign.GetValueOrDefault(m.Campaign.Id)))
            .ToList();
    }

    public async Task<CampaignDetailDto> GetAsync(Guid campaignId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        var campaign = await FindCampaignAsync(campaignId, cancellationToken);
        return await ToDetailAsync(campaign, membership.Role, cancellationToken);
    }

    /// <summary>Renombra y/o actualiza la descripción (solo Owner).</summary>
    public async Task<CampaignDetailDto> UpdateDetailsAsync(
        Guid campaignId, Guid userId, string? name, string? description,
        CancellationToken cancellationToken = default)
    {
        var membership = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(membership, CampaignRole.Owner);

        var campaign = await FindCampaignAsync(campaignId, cancellationToken);
        campaign.Name = InputGuard.RequiredText(name, "name", maxLength: 100);
        campaign.Description = CleanDescription(description);
        await db.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(campaign, membership.Role, cancellationToken);
    }

    /// <summary>Sube o reemplaza la portada (solo Owner; validación de imagen).</summary>
    public async Task UploadCoverAsync(
        Guid campaignId, Guid userId, string contentType, Stream content, long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var membership = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(membership, CampaignRole.Owner);
        var campaign = await FindCampaignAsync(campaignId, cancellationToken);

        await ImageGuard.ValidateAsync(contentType, sizeBytes, content, MaxCoverBytes, cancellationToken);
        await assets.SaveAsync(CoverKey(campaign.Id), content, cancellationToken);
        campaign.CoverContentType = contentType;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Abre la portada; null si la campaña no tiene.</summary>
    public async Task<(string ContentType, Stream Content)?> OpenCoverAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        _ = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        var campaign = await FindCampaignAsync(campaignId, cancellationToken);
        if (campaign.CoverContentType is null)
        {
            return null;
        }

        var content = await assets.OpenReadAsync(CoverKey(campaign.Id), cancellationToken);
        return content is null ? null : (campaign.CoverContentType, content);
    }

    /// <summary>Elimina la portada (solo Owner; idempotente).</summary>
    public async Task DeleteCoverAsync(Guid campaignId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(membership, CampaignRole.Owner);
        var campaign = await FindCampaignAsync(campaignId, cancellationToken);

        await assets.DeleteAsync(CoverKey(campaign.Id), cancellationToken);
        campaign.CoverContentType = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Límite de portada (más pequeño que el de mapas).</summary>
    public const long MaxCoverBytes = 5 * 1024 * 1024;

    private static string CoverKey(Guid campaignId) => $"campaign-covers/{campaignId}";

    private static string? CleanDescription(string? description)
    {
        var clean = description?.Trim();
        return string.IsNullOrEmpty(clean) ? null : InputGuard.RequiredText(clean, "description", maxLength: 500);
    }

    public async Task<CampaignDetailDto> ArchiveAsync(Guid campaignId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(membership, CampaignRole.Owner);

        var campaign = await FindCampaignAsync(campaignId, cancellationToken);
        campaign.ArchivedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(campaign, membership.Role, cancellationToken);
    }

    // --- Membresías -----------------------------------------------------

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid campaignId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireMembershipAsync(campaignId, userId, cancellationToken);

        var members = await db.CampaignMembers
            .Where(m => m.CampaignId == campaignId)
            .Include(m => m.User)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

        return members
            .Select(m => new MemberDto(m.UserId, m.User.UserName, m.Role, m.JoinedAt))
            .ToList();
    }

    public async Task<MemberDto> UpdateMemberRoleAsync(
        Guid campaignId, Guid actorUserId, Guid targetUserId, CampaignRole? role,
        CancellationToken cancellationToken = default)
    {
        if (role is null)
        {
            throw ApiException.BadRequest("El campo 'role' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "role" });
        }

        var actor = await RequireMembershipAsync(campaignId, actorUserId, cancellationToken);
        RequireRole(actor, CampaignRole.Owner);

        var target = await FindMemberAsync(campaignId, targetUserId, cancellationToken);
        if (target.Role == CampaignRole.Owner && role.Value != CampaignRole.Owner
            && await CountOwnersAsync(campaignId, cancellationToken) <= 1)
        {
            throw ApiException.Conflict("La campaña debe mantener al menos un propietario.");
        }

        if (target.Role != role.Value)
        {
            target.Role = role.Value;
            await db.SaveChangesAsync(cancellationToken);
        }

        return new MemberDto(target.UserId, target.User.UserName, target.Role, target.JoinedAt);
    }

    public async Task RemoveMemberAsync(Guid campaignId, Guid actorUserId, Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireMembershipAsync(campaignId, actorUserId, cancellationToken);
        RequireRole(actor, CampaignRole.Owner);

        var target = await FindMemberAsync(campaignId, targetUserId, cancellationToken);
        if (target.Role == CampaignRole.Owner && await CountOwnersAsync(campaignId, cancellationToken) <= 1)
        {
            throw ApiException.Conflict("La campaña debe mantener al menos un propietario.");
        }

        db.CampaignMembers.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
    }

    // --- Invitaciones ---------------------------------------------------

    public async Task<IReadOnlyList<InviteDto>> ListInvitesAsync(Guid campaignId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(actor, CampaignRole.Owner);

        var invites = await db.CampaignInvites
            .Where(i => i.CampaignId == campaignId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(ToDto).ToList();
    }

    public async Task<InviteDto> CreateInviteAsync(
        Guid campaignId, Guid userId, CampaignRole? role, int? expiresInDays,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(actor, CampaignRole.Owner);

        var inviteRole = role ?? CampaignRole.Player;
        if (inviteRole == CampaignRole.Owner)
        {
            throw ApiException.BadRequest(
                "Una invitación no puede otorgar el rol de propietario; crea al miembro como jugador o GM y promuévelo después.",
                new Dictionary<string, object?> { ["field"] = "role" });
        }

        var days = expiresInDays ?? DefaultInviteDays;
        if (days is < 1 or > MaxInviteDays)
        {
            throw ApiException.BadRequest(
                $"La caducidad debe estar entre 1 y {MaxInviteDays} días.",
                new Dictionary<string, object?> { ["field"] = "expiresInDays" });
        }

        var invite = new CampaignInvite
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Code = Guid.NewGuid().ToString("N"),
            Role = inviteRole,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
        };
        db.CampaignInvites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(invite);
    }

    public async Task RevokeInviteAsync(Guid campaignId, Guid userId, Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireMembershipAsync(campaignId, userId, cancellationToken);
        RequireRole(actor, CampaignRole.Owner);

        var invite = await db.CampaignInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.CampaignId == campaignId, cancellationToken)
            ?? throw ApiException.NotFound("La invitación no existe.");

        invite.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RedeemInviteDto> RedeemInviteAsync(string? code, Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw ApiException.BadRequest("El código de invitación es obligatorio.");
        }

        var invite = await db.CampaignInvites
            .Include(i => i.Campaign)
            .FirstOrDefaultAsync(i => i.Code == code.Trim().ToLowerInvariant(), cancellationToken);

        if (invite is null)
        {
            throw ApiException.NotFound("La invitación no existe.");
        }

        if (invite.RevokedAt is not null || invite.ExpiresAt <= DateTime.UtcNow)
        {
            throw ApiException.Gone("La invitación ha caducado o ha sido revocada.");
        }

        var existing = await db.CampaignMembers
            .FirstOrDefaultAsync(
                m => m.CampaignId == invite.CampaignId && m.UserId == userId, cancellationToken);

        if (existing is null)
        {
            db.CampaignMembers.Add(new CampaignMember
            {
                Id = Guid.NewGuid(),
                CampaignId = invite.CampaignId,
                UserId = userId,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        var role = existing?.Role ?? invite.Role;
        return new RedeemInviteDto(invite.CampaignId, invite.Campaign.Name, role);
    }

    // --- Acceso y utilidades --------------------------------------------

    /// <summary>Garantiza que el usuario es miembro de la campaña (si no, 404).</summary>
    public Task<CampaignMember> RequireMemberAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
        RequireMembershipAsync(campaignId, userId, cancellationToken);

    /// <summary>Garantiza que el rol del miembro alcanza el mínimo exigido (si no, 403).</summary>
    public static void RequireRole(CampaignMember membership, CampaignRole minimum)
    {
        if (membership.Role < minimum)
        {
            throw ApiException.Forbidden("Tu rol en la campaña no permite realizar esta operación.");
        }
    }

    private async Task<CampaignMember> RequireMembershipAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await db.CampaignMembers
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.UserId == userId, cancellationToken);

        return membership ?? throw ApiException.NotFound(
            "La campaña no existe o no tienes acceso a ella.");
    }

    private Task<Campaign> FindCampaignAsync(Guid campaignId, CancellationToken cancellationToken) =>
        db.Campaigns.FirstAsync(c => c.Id == campaignId, cancellationToken);

    private async Task<CampaignMember> FindMemberAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken) =>
        await db.CampaignMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.UserId == userId, cancellationToken)
        ?? throw ApiException.NotFound("El miembro no existe en esta campaña.");

    private Task<int> CountOwnersAsync(Guid campaignId, CancellationToken cancellationToken) =>
        db.CampaignMembers.CountAsync(
            m => m.CampaignId == campaignId && m.Role == CampaignRole.Owner, cancellationToken);

    private async Task<CampaignDetailDto> ToDetailAsync(
        Campaign campaign, CampaignRole role, CancellationToken cancellationToken)
    {
        var memberCount = await db.CampaignMembers
            .CountAsync(m => m.CampaignId == campaign.Id, cancellationToken);
        return new CampaignDetailDto(
            campaign.Id, campaign.Name, role, campaign.CreatedAt, campaign.ArchivedAt, memberCount,
            campaign.Description, campaign.CoverContentType is not null);
    }

    private static InviteDto ToDto(CampaignInvite invite) =>
        new(invite.Id, invite.Code, invite.Role, invite.CreatedAt, invite.ExpiresAt, invite.RevokedAt);
}
