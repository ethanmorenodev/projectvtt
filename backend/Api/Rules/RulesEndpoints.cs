using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Rules;

public static class RulesEndpoints
{
    public static IEndpointRouteBuilder MapRulesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Rules")
            .RequireAuthorization();

        group.MapPost("/campaigns/{campaignId:guid}/rolls", RollAsync);
        group.MapGet("/campaigns/{campaignId:guid}/rolls", ListRollsAsync);
        group.MapGet("/rulesets", ListRulesetsAsync);

        return endpoints;
    }

    private static async Task<IResult> RollAsync(
        HttpContext http, Guid campaignId, CreateRollRequest request, RollService rolls,
        CancellationToken cancellationToken) =>
        Results.Ok(await rolls.RollAsync(campaignId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> ListRollsAsync(
        HttpContext http, Guid campaignId, RollService rolls, CancellationToken cancellationToken) =>
        Results.Ok(await rolls.ListAsync(campaignId, GetUserId(http), cancellationToken));

    private static Task<IResult> ListRulesetsAsync(IRulesetRegistry rulesets)
    {
        var list = rulesets.All
            .Select(r => new RulesetDto(r.Key, r.DisplayName, r.AttributeKeys, r.ResourceKeys))
            .ToList();
        return Task.FromResult<IResult>(Results.Ok(list));
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
