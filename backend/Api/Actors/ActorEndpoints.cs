using System.Security.Claims;
using ProjectVTT.Api.Characters;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Actors;

public static class ActorEndpoints
{
    public static IEndpointRouteBuilder MapActorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Actors")
            .RequireAuthorization();

        group.MapPost("/campaigns/{campaignId:guid}/actors", CreateActorAsync);
        group.MapGet("/campaigns/{campaignId:guid}/actors", ListActorsAsync);
        group.MapGet("/actors/{actorId:guid}", GetActorAsync);
        group.MapPatch("/actors/{actorId:guid}", UpdateActorAsync);

        // Inventario del Actor (sub-recurso): mismos permisos que editar
        // la ficha (ADR-012) y respuesta con el inventario completo.
        group.MapGet("/actors/{actorId:guid}/items", ListItemsAsync);
        group.MapGet("/equipment-slots/{system}", ListEquipmentSlotsAsync);
        group.MapGet("/actors/{actorId:guid}/equipment-effects", ListEquipmentEffectsAsync);
        group.MapPost("/actors/{actorId:guid}/items", CreateItemAsync);
        group.MapPatch("/actors/{actorId:guid}/items/{itemId:guid}", UpdateItemAsync);
        group.MapDelete("/actors/{actorId:guid}/items/{itemId:guid}", DeleteItemAsync);
        group.MapPost("/actors/{actorId:guid}/items/{itemId:guid}/equip", EquipItemAsync);
        group.MapPost("/actors/{actorId:guid}/items/{itemId:guid}/transfer", TransferItemAsync);
        group.MapDelete("/actors/{actorId:guid}/items/{itemId:guid}/equip", UnequipItemAsync);

        // Efectos y condiciones del Actor: misma regla de permisos y
        // respuesta con la lista completa (ADR-014).
        group.MapGet("/actors/{actorId:guid}/effects", ListEffectsAsync);
        group.MapPost("/actors/{actorId:guid}/effects", ApplyEffectAsync);
        group.MapDelete("/actors/{actorId:guid}/effects/{effectId:guid}", RemoveEffectAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateActorAsync(
        HttpContext http, Guid campaignId, CreateActorRequest request, ActorService actors,
        CancellationToken cancellationToken)
    {
        var actor = await actors.CreateAsync(
            campaignId, GetUserId(http), request, cancellationToken);
        return Results.Created($"/api/v1/actors/{actor.Id}", actor);
    }

    private static async Task<IResult> ListActorsAsync(
        HttpContext http, Guid campaignId, ActorService actors, CancellationToken cancellationToken) =>
        Results.Ok(await actors.ListAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> GetActorAsync(
        HttpContext http, Guid actorId, ActorService actors, CancellationToken cancellationToken) =>
        Results.Ok(await actors.GetAsync(actorId, GetUserId(http), cancellationToken));

    private static async Task<IResult> UpdateActorAsync(
        HttpContext http, Guid actorId, UpdateActorRequest request, ActorService actors,
        CancellationToken cancellationToken) =>
        Results.Ok(await actors.UpdateAsync(
            actorId, GetUserId(http), request, cancellationToken));

    /// <summary>Efectos automáticos del equipamiento (PMB-6.5): reflejo auditable.</summary>
    private static async Task<IResult> ListEquipmentEffectsAsync(
        HttpContext http, Guid actorId, ActorItemService items,
        CancellationToken cancellationToken) =>
        Results.Ok(await items.EquipmentEffectsAsync(
            actorId, GetUserId(http), cancellationToken));

    /// <summary>Huecos de equipamiento del sistema (ADR-041, PMB-6.4).</summary>
    private static async Task<IResult> ListEquipmentSlotsAsync(
        string system, CharacterBlueprintService blueprints,
        CancellationToken cancellationToken) =>
        Results.Ok(await blueprints.GetEquipmentSlotsAsync(system, cancellationToken));

    private static async Task<IResult> ListItemsAsync(
        HttpContext http, Guid actorId, ActorItemService items,
        CancellationToken cancellationToken) =>
        Results.Ok(await items.ListAsync(actorId, GetUserId(http), cancellationToken));

    private static async Task<IResult> CreateItemAsync(
        HttpContext http, Guid actorId, CreateActorItemRequest request, ActorItemService items,
        CancellationToken cancellationToken) =>
        Results.Created($"/api/v1/actors/{actorId}/items",
            await items.CreateAsync(actorId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> UpdateItemAsync(
        HttpContext http, Guid actorId, Guid itemId, UpdateActorItemRequest request,
        ActorItemService items, CancellationToken cancellationToken) =>
        Results.Ok(await items.UpdateAsync(
            actorId, itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> TransferItemAsync(
        HttpContext http, Guid actorId, Guid itemId, TransferActorItemRequest request,
        ActorItemService items, CancellationToken cancellationToken) =>
        Results.Ok(await items.TransferAsync(
            actorId, itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> EquipItemAsync(
        HttpContext http, Guid actorId, Guid itemId, EquipActorItemRequest request,
        ActorItemService items, CancellationToken cancellationToken) =>
        Results.Ok(await items.EquipAsync(
            actorId, itemId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> UnequipItemAsync(
        HttpContext http, Guid actorId, Guid itemId, ActorItemService items,
        CancellationToken cancellationToken) =>
        Results.Ok(await items.UnequipAsync(
            actorId, itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> DeleteItemAsync(
        HttpContext http, Guid actorId, Guid itemId, ActorItemService items,
        CancellationToken cancellationToken) =>
        Results.Ok(await items.DeleteAsync(actorId, itemId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ListEffectsAsync(
        HttpContext http, Guid actorId, ActorEffectService effects,
        CancellationToken cancellationToken) =>
        Results.Ok(await effects.ListAsync(actorId, GetUserId(http), cancellationToken));

    private static async Task<IResult> ApplyEffectAsync(
        HttpContext http, Guid actorId, CreateActorEffectRequest request,
        ActorEffectService effects, CancellationToken cancellationToken) =>
        Results.Created($"/api/v1/actors/{actorId}/effects",
            await effects.ApplyAsync(actorId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> RemoveEffectAsync(
        HttpContext http, Guid actorId, Guid effectId, ActorEffectService effects,
        CancellationToken cancellationToken) =>
        Results.Ok(await effects.RemoveAsync(actorId, effectId, GetUserId(http), cancellationToken));

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
