using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Fog;

public static class FogEndpoints
{
    public static IEndpointRouteBuilder MapFogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Fog")
            .RequireAuthorization();

        group.MapGet("/scenes/{sceneId:guid}/fog", GetAsync);
        group.MapPost("/scenes/{sceneId:guid}/fog/reveal", RevealAsync);
        group.MapPost("/scenes/{sceneId:guid}/fog/hide", HideAsync);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        HttpContext http, Guid sceneId, FogService fog, CancellationToken cancellationToken) =>
        Results.Ok(await fog.GetAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> RevealAsync(
        HttpContext http, Guid sceneId, SetFogCellRequest request, FogService fog,
        CancellationToken cancellationToken) =>
        Results.Ok(await fog.RevealAsync(sceneId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> HideAsync(
        HttpContext http, Guid sceneId, SetFogCellRequest request, FogService fog,
        CancellationToken cancellationToken) =>
        Results.Ok(await fog.HideAsync(sceneId, GetUserId(http), request, cancellationToken));

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
