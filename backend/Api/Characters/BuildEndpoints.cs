using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Characters;

/// <summary>
/// Endpoints del creador de personajes (PMB-3.2): blueprints por sistema y
/// previsualización/validación de builds. Las reglas viven solo en
/// servidor; el cliente las muestra, no las reimplementa (ADR-034).
/// </summary>
public static class BuildEndpoints
{
    public static IEndpointRouteBuilder MapBuildEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Characters")
            .RequireAuthorization();

        group.MapGet("/characters/blueprints", ListBlueprintsAsync);
        group.MapPost("/characters/build/preview", PreviewAsync);

        return endpoints;
    }

    private static async Task<IResult> ListBlueprintsAsync(
        string? system, CharacterBlueprintService blueprints,
        CancellationToken cancellationToken) =>
        Results.Ok(await blueprints.ListAsync(system, cancellationToken));

    private static async Task<IResult> PreviewAsync(
        HttpContext http, BuildPreviewRequest request,
        CharacterBlueprintService blueprints, CancellationToken cancellationToken) =>
        Results.Ok(await blueprints.PreviewAsync(
            GetUserId(http), request, cancellationToken));

    private static Guid GetUserId(HttpContext http) =>
        http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } raw && Guid.TryParse(raw, out var id)
            ? id
            : throw ApiException.Unauthorized();
}
