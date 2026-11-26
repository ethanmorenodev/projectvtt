using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Annotations;

public static class SceneAnnotationEndpoints
{
    public static IEndpointRouteBuilder MapSceneAnnotationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("SceneAnnotations")
            .RequireAuthorization();

        group.MapGet("/scenes/{sceneId:guid}/annotations", GetAsync);
        group.MapPut("/scenes/{sceneId:guid}/annotations", SetAsync);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        HttpContext http, Guid sceneId, AppDbContext db, CampaignService campaigns,
        SceneService scenes, IHubContext<CampaignHub> hub,
        CancellationToken cancellationToken) =>
        Results.Ok(await CreateService(db, campaigns, scenes, hub)
            .GetAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> SetAsync(
        HttpContext http, Guid sceneId, SetSceneAnnotationRequest request,
        AppDbContext db, CampaignService campaigns, SceneService scenes,
        IHubContext<CampaignHub> hub, CancellationToken cancellationToken) =>
        Results.Ok(await CreateService(db, campaigns, scenes, hub)
            .SetAsync(sceneId, GetUserId(http), request, cancellationToken));

    // El servicio se compone aquí con dependencias ya registradas para no
    // tocar el arranque más allá del mapeo del grupo de endpoints.
    private static SceneAnnotationService CreateService(
        AppDbContext db, CampaignService campaigns, SceneService scenes,
        IHubContext<CampaignHub> hub) => new(db, campaigns, scenes, hub);

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
