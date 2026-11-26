using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Worlds;

/// <summary>Endpoints de mundos reutilizables (ADR-042 / PMB-7.2).</summary>
public static class WorldEndpoints
{
    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Worlds")
            .RequireAuthorization();

        group.MapPost("/worlds", CreateWorldAsync);
        group.MapGet("/worlds", ListWorldsAsync);
        group.MapPost("/worlds/{worldId:guid}/campaigns/link", LinkCampaignAsync);
        group.MapPost("/content/items/{itemId:guid}/world/use", UseItemAsync);
        group.MapPost("/content/items/{itemId:guid}/world/override", SetOverrideAsync);
        group.MapGet("/content/items/{itemId:guid}/world/effective", GetEffectiveAsync);
        group.MapPost("/content/items/{itemId:guid}/world/independentize", IndependentizeAsync);
        group.MapPost("/content/items/{itemId:guid}/world/update", UpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateWorldAsync(
        HttpContext http, CreateWorldRequest request, WorldService worlds,
        CancellationToken cancellationToken) =>
        Results.Created("/api/v1/worlds",
            await worlds.CreateAsync(GetUserId(http), request, cancellationToken));

    private static async Task<IResult> ListWorldsAsync(
        HttpContext http, WorldService worlds, CancellationToken cancellationToken) =>
        Results.Ok(await worlds.ListAsync(GetUserId(http), cancellationToken));

    private static async Task<IResult> LinkCampaignAsync(
        HttpContext http, Guid worldId, LinkWorldCampaignRequest request,
        WorldService worlds, CancellationToken cancellationToken)
    {
        await worlds.LinkCampaignAsync(worldId, GetUserId(http), request, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UseItemAsync(
        HttpContext http, Guid itemId, UseWorldItemRequest request,
        WorldService worlds, CancellationToken cancellationToken) =>
        Results.Ok(await worlds.UseInCampaignAsync(
            itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> SetOverrideAsync(
        HttpContext http, Guid itemId, WorldOverrideRequest request,
        WorldService worlds, CancellationToken cancellationToken) =>
        Results.Ok(await worlds.SetOverrideAsync(
            itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> GetEffectiveAsync(
        HttpContext http, Guid itemId, Guid? campaignId,
        WorldService worlds, CancellationToken cancellationToken) =>
        Results.Ok(await worlds.GetEffectiveAsync(
            itemId, campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> IndependentizeAsync(
        HttpContext http, Guid itemId, WorldCampaignRequest request,
        WorldService worlds, CancellationToken cancellationToken) =>
        Results.Created($"/api/v1/content/items/{itemId}/world/independentize",
            await worlds.IndependentizeAsync(itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> UpdateAsync(
        HttpContext http, Guid itemId, UpdateWorldItemRequest request,
        WorldService worlds, CancellationToken cancellationToken) =>
        Results.Ok(await worlds.UpdateInCampaignAsync(
            itemId, GetUserId(http), request, cancellationToken));

    private static Guid GetUserId(HttpContext http)
    {
        var raw = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(raw, out var userId))
        {
            throw ApiException.Unauthorized();
        }
        return userId;
    }
}
