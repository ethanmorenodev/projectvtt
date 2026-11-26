using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Campaigns;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Campaigns")
            .RequireAuthorization();

        group.MapPost("/campaigns", CreateAsync);
        group.MapGet("/campaigns", ListAsync);
        group.MapGet("/campaigns/{campaignId:guid}", GetAsync);
        group.MapPatch("/campaigns/{campaignId:guid}", UpdateDetailsAsync);
        group.MapPost("/campaigns/{campaignId:guid}/archive", ArchiveAsync);

        group.MapPut("/campaigns/{campaignId:guid}/cover", UploadCoverAsync);
        group.MapGet("/campaigns/{campaignId:guid}/cover", GetCoverAsync);
        group.MapDelete("/campaigns/{campaignId:guid}/cover", DeleteCoverAsync);

        group.MapGet("/campaigns/{campaignId:guid}/members", ListMembersAsync);
        group.MapPatch("/campaigns/{campaignId:guid}/members/{userId:guid}", UpdateMemberRoleAsync);
        group.MapDelete("/campaigns/{campaignId:guid}/members/{userId:guid}", RemoveMemberAsync);

        group.MapGet("/campaigns/{campaignId:guid}/invites", ListInvitesAsync);
        group.MapPost("/campaigns/{campaignId:guid}/invites", CreateInviteAsync);
        group.MapDelete("/campaigns/{campaignId:guid}/invites/{inviteId:guid}", RevokeInviteAsync);
        group.MapPost("/invites/{code}/redeem", RedeemInviteAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext http, CreateCampaignRequest request, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        var result = await campaigns.CreateAsync(
            GetUserId(http), request.Name, request.Description, cancellationToken);
        return Results.Created($"/api/v1/campaigns/{result.Id}", result);
    }

    private static async Task<IResult> ListAsync(
        HttpContext http, CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.ListAsync(GetUserId(http), cancellationToken));

    private static async Task<IResult> GetAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.GetAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> UpdateDetailsAsync(
        HttpContext http, Guid campaignId, UpdateCampaignRequest request, CampaignService campaigns,
        CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.UpdateDetailsAsync(
            campaignId, GetUserId(http), request.Name, request.Description, cancellationToken));

    private static async Task<IResult> UploadCoverAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file")
            ?? throw ApiException.BadRequest(
                "Falta el archivo 'file'.",
                new Dictionary<string, object?> { ["field"] = "file" });

        await using var content = file.OpenReadStream();
        await campaigns.UploadCoverAsync(
            campaignId, GetUserId(http), file.ContentType ?? string.Empty,
            content, file.Length, cancellationToken);
        return Results.Ok(new { hasCover = true });
    }

    private static async Task<IResult> GetCoverAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        var opened = await campaigns.OpenCoverAsync(campaignId, GetUserId(http), cancellationToken);
        if (opened is null)
        {
            return Results.NotFound();
        }

        var (contentType, content) = opened.Value;
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers["Cache-Control"] = "no-store";
        return Results.File(content, contentType);
    }

    private static async Task<IResult> DeleteCoverAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        await campaigns.DeleteCoverAsync(campaignId, GetUserId(http), cancellationToken);
        return Results.Ok(new { deleted = true });
    }

    private static async Task<IResult> ArchiveAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.ArchiveAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ListMembersAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.ListMembersAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> UpdateMemberRoleAsync(
        HttpContext http, Guid campaignId, Guid userId, UpdateMemberRoleRequest request,
        CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.UpdateMemberRoleAsync(
            campaignId, GetUserId(http), userId, request.Role, cancellationToken));

    private static async Task<IResult> RemoveMemberAsync(
        HttpContext http, Guid campaignId, Guid userId, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        await campaigns.RemoveMemberAsync(campaignId, GetUserId(http), userId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListInvitesAsync(
        HttpContext http, Guid campaignId, CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.ListInvitesAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> CreateInviteAsync(
        HttpContext http, Guid campaignId, CreateInviteRequest request, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        var invite = await campaigns.CreateInviteAsync(
            campaignId, GetUserId(http), request.Role, request.ExpiresInDays, cancellationToken);
        return Results.Created($"/api/v1/campaigns/{campaignId}/invites", invite);
    }

    private static async Task<IResult> RevokeInviteAsync(
        HttpContext http, Guid campaignId, Guid inviteId, CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        await campaigns.RevokeInviteAsync(campaignId, GetUserId(http), inviteId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RedeemInviteAsync(
        HttpContext http, string code, CampaignService campaigns, CancellationToken cancellationToken) =>
        Results.Ok(await campaigns.RedeemInviteAsync(code, GetUserId(http), cancellationToken));

    private static Guid GetUserId(HttpContext http)
    {
        var value = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            throw ApiException.Unauthorized();
        }

        return userId;
    }
}
