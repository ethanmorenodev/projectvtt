using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Combats;

public static class CombatEndpoints
{
    public static IEndpointRouteBuilder MapCombatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Combats")
            .RequireAuthorization();

        group.MapGet("/scenes/{sceneId:guid}/combat", GetAsync);
        group.MapPost("/scenes/{sceneId:guid}/combat/start", StartAsync);
        group.MapPost("/scenes/{sceneId:guid}/combat/end", EndAsync);
        group.MapPost("/scenes/{sceneId:guid}/combat/advance", AdvanceAsync);
        group.MapPost("/scenes/{sceneId:guid}/combat/combatants", AddCombatantAsync);
        group.MapPatch(
            "/scenes/{sceneId:guid}/combat/combatants/{combatantId:guid}", UpdateCombatantAsync);
        group.MapDelete(
            "/scenes/{sceneId:guid}/combat/combatants/{combatantId:guid}", RemoveCombatantAsync);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        HttpContext http, Guid sceneId, CombatService combat, CancellationToken cancellationToken) =>
        Results.Ok(await combat.GetAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> StartAsync(
        HttpContext http, Guid sceneId, CombatService combat, CancellationToken cancellationToken) =>
        Results.Created(
            $"/api/v1/scenes/{sceneId}/combat",
            await combat.StartAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> EndAsync(
        HttpContext http, Guid sceneId, CombatService combat, CancellationToken cancellationToken) =>
        Results.Ok(await combat.EndAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> AdvanceAsync(
        HttpContext http, Guid sceneId, CombatService combat, CancellationToken cancellationToken) =>
        Results.Ok(await combat.AdvanceAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> AddCombatantAsync(
        HttpContext http, Guid sceneId, CreateCombatantRequest request, CombatService combat,
        CancellationToken cancellationToken) =>
        Results.Created(
            $"/api/v1/scenes/{sceneId}/combat",
            await combat.AddCombatantAsync(sceneId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> UpdateCombatantAsync(
        HttpContext http, Guid sceneId, Guid combatantId, UpdateCombatantRequest request,
        CombatService combat, CancellationToken cancellationToken) =>
        Results.Ok(await combat.UpdateCombatantAsync(
            sceneId, combatantId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> RemoveCombatantAsync(
        HttpContext http, Guid sceneId, Guid combatantId, CombatService combat,
        CancellationToken cancellationToken) =>
        Results.Ok(await combat.RemoveCombatantAsync(
            sceneId, combatantId, GetUserId(http), cancellationToken));

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
