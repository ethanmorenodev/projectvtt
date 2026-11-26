using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.Campaigns;

public sealed record CampaignSummaryDto(
    Guid Id,
    string Name,
    CampaignRole Role,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    string? Description,
    bool HasCover,
    DateTime? LastActivityAt,
    int MemberCount);

public sealed record CampaignDetailDto(
    Guid Id,
    string Name,
    CampaignRole Role,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount,
    string? Description,
    bool HasCover);

public sealed record MemberDto(Guid UserId, string UserName, CampaignRole Role, DateTime JoinedAt);

public sealed record InviteDto(
    Guid Id, string Code, CampaignRole Role, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt);

public sealed record RedeemInviteDto(Guid CampaignId, string CampaignName, CampaignRole Role);

public sealed record UpdateCampaignRequest(string? Name, string? Description);

public sealed record UpdateMemberRoleRequest(CampaignRole? Role);

public sealed record CreateInviteRequest(CampaignRole? Role, int? ExpiresInDays);

public sealed record CreateCampaignRequest(string? Name, string? Description);
