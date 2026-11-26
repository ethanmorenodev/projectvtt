using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Scenes;

public static class SceneEndpoints
{
    public static IEndpointRouteBuilder MapSceneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Scenes")
            .RequireAuthorization();

        group.MapPost("/campaigns/{campaignId:guid}/scenes", CreateSceneAsync);
        group.MapGet("/campaigns/{campaignId:guid}/scenes", ListScenesAsync);
        group.MapGet("/scenes/{sceneId:guid}", GetSceneAsync);
        group.MapPatch("/scenes/{sceneId:guid}", UpdateSceneAsync);
        group.MapDelete("/scenes/{sceneId:guid}", DeleteSceneAsync);
        group.MapPut("/scenes/{sceneId:guid}/cover", UploadCoverAsync);
        group.MapGet("/scenes/{sceneId:guid}/cover", GetCoverAsync);
        group.MapDelete("/scenes/{sceneId:guid}/cover", DeleteCoverAsync);
        group.MapPost("/scenes/{sceneId:guid}/tokens", CreateTokenAsync);
        group.MapGet("/scenes/{sceneId:guid}/tokens", ListTokensAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateSceneAsync(
        HttpContext http, Guid campaignId, CreateSceneRequest request, SceneService scenes,
        CancellationToken cancellationToken)
    {
        var result = await scenes.CreateSceneAsync(
            campaignId, GetUserId(http), request.Name, cancellationToken);
        return Results.Created($"/api/v1/scenes/{result.Id}", result);
    }

    private static async Task<IResult> ListScenesAsync(
        HttpContext http, Guid campaignId, SceneService scenes, CancellationToken cancellationToken) =>
        Results.Ok(await scenes.ListScenesAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> GetSceneAsync(
        HttpContext http, Guid sceneId, SceneService scenes, CancellationToken cancellationToken) =>
        Results.Ok(await scenes.GetSceneAsync(sceneId, GetUserId(http), cancellationToken));

    private static async Task<IResult> UpdateSceneAsync(
        HttpContext http, Guid sceneId, UpdateSceneRequest request, SceneService scenes,
        CancellationToken cancellationToken) =>
        Results.Ok(await scenes.UpdateSceneAsync(
            sceneId, GetUserId(http), request.GridCellSize, request.GridShape, cancellationToken));

    private static async Task<IResult> DeleteSceneAsync(
        HttpContext http, Guid sceneId, SceneService scenes, CancellationToken cancellationToken)
    {
        await scenes.DeleteSceneAsync(sceneId, GetUserId(http), cancellationToken);
        return Results.Ok(new { deleted = true });
    }

    private static async Task<IResult> UploadCoverAsync(
        HttpContext http, Guid sceneId, SceneService scenes, CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file")
            ?? throw ApiException.BadRequest(
                "Falta el archivo 'file'.",
                new Dictionary<string, object?> { ["field"] = "file" });
        await using var content = file.OpenReadStream();
        await scenes.UploadCoverAsync(sceneId, GetUserId(http), file.ContentType ?? string.Empty,
            content, file.Length, cancellationToken);
        return Results.Ok(new { hasCover = true });
    }

    private static async Task<IResult> GetCoverAsync(
        HttpContext http, Guid sceneId, SceneService scenes, CancellationToken cancellationToken)
    {
        var opened = await scenes.OpenCoverAsync(sceneId, GetUserId(http), cancellationToken);
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
        HttpContext http, Guid sceneId, SceneService scenes, CancellationToken cancellationToken)
    {
        await scenes.DeleteCoverAsync(sceneId, GetUserId(http), cancellationToken);
        return Results.Ok(new { deleted = true });
    }

    private static async Task<IResult> CreateTokenAsync(
        HttpContext http, Guid sceneId, CreateTokenRequest request, SceneService scenes,
        CancellationToken cancellationToken)
    {
        var token = await scenes.CreateTokenAsync(
            sceneId, GetUserId(http), request.Name, request.X, request.Y, request.ActorId,
            cancellationToken);
        return Results.Created($"/api/v1/scenes/{sceneId}/tokens/{token.Id}", token);
    }

    private static async Task<IResult> ListTokensAsync(
        HttpContext http, Guid sceneId, SceneService scenes, CancellationToken cancellationToken) =>
        Results.Ok(await scenes.ListTokensAsync(sceneId, GetUserId(http), cancellationToken));

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
