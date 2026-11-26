using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.History;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("History")
            .RequireAuthorization();

        group.MapGet("/campaigns/{campaignId:guid}/history", ListHistoryAsync);
        group.MapGet("/activity", ListActivityAsync);

        return endpoints;
    }

    private static async Task<IResult> ListHistoryAsync(
        HttpContext http, Guid campaignId, HistoryService history,
        CancellationToken cancellationToken) =>
        Results.Ok(await history.ListAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ListActivityAsync(
        HttpContext http, HistoryService history, CancellationToken cancellationToken)
    {
        var limitRaw = http.Request.Query["limit"].FirstOrDefault();
        var limit = int.TryParse(limitRaw, out var parsed) ? parsed : 10;
        return Results.Ok(await history.ListActivityAsync(GetUserId(http), limit, cancellationToken));
    }

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
