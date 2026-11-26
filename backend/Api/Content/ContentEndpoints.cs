using System.Security.Claims;
using System.Text.Json.Nodes;
using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Endpoints del modelo de contenido (PMB-P.3): tipos, items (CRUD),
/// publicación con revisiones, usos por personaje. Autoridad en servidor:
/// permisos por ámbito + grants y secrecía del DM aplicados en
/// <see cref="ContentService"/>. El usuario sale de la sesión, nunca de la
/// petición.
/// </summary>
public static class ContentEndpoints
{
    /// <summary>Política para mutar el catálogo global (P0.1).</summary>
    public const string ContentOperatorPolicy = "ContentOperator";

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Content")
            .RequireAuthorization();

        group.MapGet("/content/types", ListTypesAsync);
        // Mutación del catálogo global: excluye a usuarios normales.
        group.MapPost("/content/types", CreateTypeAsync)
            .RequireAuthorization(ContentOperatorPolicy);

        group.MapGet("/content/items", ListItemsAsync);
        group.MapPost("/content/items", CreateItemAsync);
        group.MapGet("/content/items/{itemId:guid}", GetItemAsync);
        group.MapPatch("/content/items/{itemId:guid}", UpdateItemAsync);
        group.MapDelete("/content/items/{itemId:guid}", DeleteItemAsync);
        group.MapPost("/content/items/{itemId:guid}/publish", PublishAsync);

        // Flujo del DM (PMB-4.8, aditivo aprobado 2026-11-16).
        group.MapPost("/content/items/{itemId:guid}/propose", ProposeAsync);
        group.MapGet("/content/proposals", ListProposalsAsync);
        group.MapGet("/content/lore/graph", LoreGraphAsync);
        group.MapGet("/content/lore/timeline", LoreTimelineAsync);
        group.MapPost("/content/items/{itemId:guid}/approve", ApproveAsync);
        group.MapPost("/content/items/{itemId:guid}/reject", RejectAsync);
        group.MapGet("/content/items/{itemId:guid}/grants", ListGrantsAsync);
        group.MapPost("/content/items/{itemId:guid}/grants", AddGrantAsync);
        group.MapDelete("/content/items/{itemId:guid}/grants/{grantId:guid}", DeleteGrantAsync);
        group.MapGet("/content/items/{itemId:guid}/revisions", ListRevisionsAsync);

        // Relaciones tipadas (PMB-4.6, aditivo aprobado 2026-11-16):
        // navegación entrante/saliente y edición con validación en servidor.
        group.MapGet("/content/items/{itemId:guid}/relations", ListRelationsAsync);
        group.MapGet("/content/items/{itemId:guid}/diff", GetDiffAsync);
        group.MapGet("/content/items/{itemId:guid}/ability-guide", AbilityGuideAsync);
        group.MapPost("/content/items/{itemId:guid}/relations", CreateRelationAsync);
        group.MapDelete(
            "/content/items/{itemId:guid}/relations/{relationId:guid}", DeleteRelationAsync);

        // Usos por item (PMB-4.5, aditivo aprobado 2026-11-12): resumen sin
        // snapshot, solo de campañas donde el solicitante es miembro.
        group.MapGet("/content/items/{itemId:guid}/usages", ListUsagesForItemAsync);

        // Procedencia y licencias (ADR-036, PMB-4.2): metadatos globales
        // de paquetes — sin datos de campaña, solo requiere sesión.
        group.MapGet("/content/packs", ListPacksAsync);
        group.MapPost("/content/packs/import", ImportPackAsync)
            .RequireAuthorization(ContentOperatorPolicy);
        group.MapGet("/content/packs/{key}/export", ExportPackAsync);

        // Usos por personaje (ADR-031 §7): sub-recurso del actor.
        group.MapGet("/actors/{actorId:guid}/usages", ListUsagesAsync);
        group.MapPost("/actors/{actorId:guid}/usages", CreateUsageAsync);

        return endpoints;
    }

    private static async Task<IResult> ListTypesAsync(
        string? system, ContentService content, CancellationToken cancellationToken) =>
        Results.Ok(await content.ListTypesAsync(system, cancellationToken));

    private static async Task<IResult> CreateTypeAsync(
        CreateContentTypeRequest request, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Created("/api/v1/content/types",
            await content.CreateTypeAsync(request, cancellationToken));

    private static async Task<IResult> ListItemsAsync(
        HttpContext http, Guid? campaignId, string? system, string? typeKey,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Ok(await content.ListItemsAsync(
            GetUserId(http), campaignId, system, typeKey, cancellationToken));

    private static async Task<IResult> CreateItemAsync(
        HttpContext http, CreateContentItemRequest request, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Created("/api/v1/content/items",
            await content.CreateAsync(GetUserId(http), request, cancellationToken));

    private static async Task<IResult> GetItemAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.GetAsync(itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> UpdateItemAsync(
        HttpContext http, Guid itemId, UpdateContentItemRequest request,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Ok(await content.UpdateAsync(
            itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> DeleteItemAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken)
    {
        await content.DeleteAsync(itemId, GetUserId(http), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> PublishAsync(
        HttpContext http, Guid itemId, PublishContentRequest request,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Ok(await content.PublishAsync(
            itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> ListRevisionsAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ListRevisionsAsync(
            itemId, GetUserId(http), cancellationToken));

    /// <summary>Diff de homebrew (PMB-5.5): autor o DM de la campaña.</summary>
    private static async Task<IResult> GetDiffAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.GetDiffAsync(itemId, GetUserId(http), cancellationToken));

    /// <summary>Resolución asistida de habilidades (PMB-5.4): solo datos y pasos.</summary>
    private static async Task<IResult> AbilityGuideAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken)
    {
        var item = await content.GetAsync(itemId, GetUserId(http), cancellationToken);
        var properties = item.Properties as JsonObject ?? [];
        var resolution = properties["resolution"]?.GetValue<string>() ?? "manual";
        return Results.Ok(new
        {
            itemId,
            resolution,
            automatic = EffectResolver.AutomaticNames(properties),
            steps = EffectResolver.ManualSteps(properties),
        });
    }

    private static async Task<IResult> LoreGraphAsync(
        HttpContext http, Guid? campaignId, LoreService lore,
        CancellationToken cancellationToken) =>
        Results.Ok(await lore.GraphAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> LoreTimelineAsync(
        HttpContext http, Guid? campaignId, LoreService lore,
        CancellationToken cancellationToken) =>
        Results.Ok(await lore.TimelineAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ProposeAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ProposeAsync(itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ListProposalsAsync(
        HttpContext http, Guid? campaignId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ListProposalsAsync(
            campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ApproveAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ApproveAsync(itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> RejectAsync(
        HttpContext http, Guid itemId, RejectContentRequest request,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Ok(await content.RejectAsync(
            itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> ListGrantsAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ListGrantsAsync(itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> AddGrantAsync(
        HttpContext http, Guid itemId, CreateContentGrantRequest request,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Created($"/api/v1/content/items/{itemId}/grants",
            await content.AddGrantAsync(itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> DeleteGrantAsync(
        HttpContext http, Guid itemId, Guid grantId,
        ContentService content, CancellationToken cancellationToken)
    {
        await content.DeleteGrantAsync(itemId, grantId, GetUserId(http), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListRelationsAsync(
        HttpContext http, Guid itemId, string? direction, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ListRelationsAsync(
            itemId, GetUserId(http), direction, cancellationToken));

    private static async Task<IResult> CreateRelationAsync(
        HttpContext http, Guid itemId, CreateContentRelationRequest request,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Created($"/api/v1/content/items/{itemId}/relations",
            await content.CreateRelationAsync(
                itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> DeleteRelationAsync(
        HttpContext http, Guid itemId, Guid relationId,
        ContentService content, CancellationToken cancellationToken)
    {
        await content.DeleteRelationAsync(
            itemId, relationId, GetUserId(http), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListUsagesForItemAsync(
        HttpContext http, Guid itemId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ListUsagesForItemAsync(
            itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ImportPackAsync(
        HttpContext http, PackageService packages, CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file")
            ?? throw ApiException.BadRequest(
                "Falta el archivo 'file'.",
                new Dictionary<string, object?> { ["field"] = "file" });
        if (!Enum.TryParse(form["strategy"].ToString(), ignoreCase: true,
                out ImportConflictStrategy strategy))
        {
            throw ApiException.BadRequest(
                "'strategy' debe ser Keep, Update o Copy.",
                new Dictionary<string, object?> { ["field"] = "strategy" });
        }
        await using var content = file.OpenReadStream();
        return Results.Ok(await packages.ImportAsync(content, strategy, cancellationToken));
    }

    private static async Task<IResult> ExportPackAsync(
        string key, PackageService packages, CancellationToken cancellationToken)
    {
        var bytes = await packages.ExportAsync(key, cancellationToken);
        return Results.File(bytes, "application/zip", $"{key}.pvttpkg");
    }

    private static async Task<IResult> ListPacksAsync(
        ContentPackService packs, CancellationToken cancellationToken) =>
        Results.Ok(await packs.ListAsync(cancellationToken));

    private static async Task<IResult> ListUsagesAsync(
        HttpContext http, Guid actorId, ContentService content,
        CancellationToken cancellationToken) =>
        Results.Ok(await content.ListUsagesAsync(
            actorId, GetUserId(http), cancellationToken));

    private static async Task<IResult> CreateUsageAsync(
        HttpContext http, Guid actorId, CreateItemUsageRequest request,
        ContentService content, CancellationToken cancellationToken) =>
        Results.Created($"/api/v1/actors/{actorId}/usages",
            await content.CreateUsageAsync(actorId, GetUserId(http), request, cancellationToken));

    private static Guid GetUserId(HttpContext http) =>
        http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } raw && Guid.TryParse(raw, out var id)
            ? id
            : throw ApiException.Unauthorized();
}
