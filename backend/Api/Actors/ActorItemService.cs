using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Characters;
using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Actors;

/// <summary>
/// Inventario básico del Actor (Épica E): ítems con nombre y cantidad,
/// genéricos para cualquier sistema (PROJECT_SPEC §3). Permisos idénticos
/// a los de editar la ficha (ADR-012): leer requiere ser miembro; crear,
/// editar y borrar requiere ser el propietario del Actor o GM u Owner.
/// Cada mutación devuelve el inventario completo y emite `InventoryUpdated`
/// al grupo de la campaña vía <see cref="IHubContext{THub}"/>.
/// </summary>
public sealed class ActorItemService(
    AppDbContext db,
    ActorService actors,
    HistoryService history,
    IHubContext<CampaignHub> hub,
    CharacterBlueprintService blueprints)
{
    /// <summary>Cantidad máxima por ímite (tope de sanidad en el servidor).</summary>
    public const int MaxQuantity = 9999;

    public async Task<ActorInventoryDto> ListAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireActorAccessAsync(actorId, userId, cancellationToken);
        return await BuildInventoryAsync(actor.Id, cancellationToken);
    }

    public async Task<ActorInventoryDto> CreateAsync(
        Guid actorId, Guid userId, CreateActorItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);

        var name = request.Name is { Length: > 0 }
            ? InputGuard.RequiredText(request.Name, "name", maxLength: 100)
            : null;
        double? weight = null;
        double? volume = null;
        if (request.ContentItemId is { } contentItemId)
        {
            var catalogItem = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == contentItemId, cancellationToken)
                ?? throw ApiException.NotFound("El objeto del catálogo no existe.");
            var properties = JsonNode.Parse(catalogItem.PropertiesJson) as JsonObject ?? [];
            weight = properties["weight"]?.GetValue<double?>();
            volume = properties["volume"]?.GetValue<double?>();
            name ??= catalogItem.Name;
        }
        if (name is null)
        {
            throw ApiException.BadRequest("Falta el campo 'name'.",
                new Dictionary<string, object?> { ["field"] = "name" });
        }

        var now = DateTime.UtcNow;
        var item = new ActorItem
        {
            Id = Guid.NewGuid(),
            ActorId = actor.Id,
            Name = name,
            Quantity = ValidateQuantity(request.Quantity ?? 1),
            ContentItemId = request.ContentItemId,
            Weight = weight,
            Volume = volume,
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ActorItems.Add(item);
        history.Record(actor.CampaignId, userId, HistoryKinds.ItemCreated,
            $"Ítem '{item.Name}' añadido a '{actor.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(actor, userId, cancellationToken);
    }

    public async Task<ActorInventoryDto> UpdateAsync(
        Guid actorId, Guid itemId, Guid userId, UpdateActorItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var item = await FindItemAsync(actor.Id, itemId, cancellationToken);

        if (request.Version is null)
        {
            throw ApiException.BadRequest("El campo 'version' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "version" });
        }

        if (item.Version != request.Version.Value)
        {
            throw ApiException.Conflict(
                $"Conflicto de versión: se esperaba {request.Version.Value} pero el ítem está en la versión {item.Version}.");
        }

        item.Name = request.Name is null
            ? item.Name
            : InputGuard.RequiredText(request.Name, "name", maxLength: 100);
        item.Quantity = request.Quantity is null
            ? item.Quantity
            : ValidateQuantity(request.Quantity.Value);
        item.Version += 1;
        item.UpdatedAt = DateTime.UtcNow;
        history.Record(actor.CampaignId, userId, HistoryKinds.ItemUpdated,
            $"Ítem '{item.Name}' actualizado en '{actor.Name}'");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ApiException.Conflict(
                "El ítem cambió a la vez desde otro cliente; vuelve a intentarlo con la versión actual.");
        }

        return await PublishAsync(actor, userId, cancellationToken);
    }

    /// <summary>
    /// Mueve un ítem (o parte de él) a otro actor de la misma campaña
    /// (PMB-6.7). Ambos actores requieren permisos de edición; el cambio
    /// es atómico y publica `InventoryUpdated` para los dos actores.
    /// Devuelve el inventario del actor de origen, como los endpoints vecinos.
    /// </summary>
    public async Task<ActorInventoryDto> TransferAsync(
        Guid actorId, Guid itemId, Guid userId, TransferActorItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var target = await actors.RequireEditableActorAsync(
            request.TargetActorId, userId, cancellationToken);
        if (target.CampaignId != source.CampaignId)
        {
            throw ApiException.NotFound("El actor destino no existe o no tienes acceso a él.");
        }

        if (target.Id == source.Id)
        {
            throw ApiException.BadRequest("El actor destino debe ser distinto del origen.",
                new Dictionary<string, object?> { ["field"] = "targetActorId" });
        }

        var item = await FindItemAsync(source.Id, itemId, cancellationToken);
        if (request.Version is not null && item.Version != request.Version.Value)
        {
            throw ApiException.Conflict(
                $"Conflicto de versión: se esperaba {request.Version.Value} pero el ítem está en la versión {item.Version}.");
        }

        var quantity = request.Quantity ?? item.Quantity;
        if (quantity < 1 || quantity > item.Quantity)
        {
            throw ApiException.BadRequest(
                $"La cantidad debe estar entre 1 y {item.Quantity}.",
                new Dictionary<string, object?> { ["field"] = "quantity" });
        }
        ValidateQuantity(quantity);

        // Un mismo objeto se acumula en una sola fila del destino.
        var targetItems = await db.ActorItems
            .Where(i => i.ActorId == target.Id)
            .ToListAsync(cancellationToken);
        var matching = targetItems.FirstOrDefault(i =>
            item.ContentItemId is { } contentItemId
                ? i.ContentItemId == contentItemId
                : i.ContentItemId is null
                    && string.Equals(i.Name, item.Name, StringComparison.OrdinalIgnoreCase));

        var itemName = item.Name;
        var now = DateTime.UtcNow;

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (matching is not null)
            {
                ValidateQuantity(matching.Quantity + quantity);
                matching.Quantity += quantity;
                matching.Version += 1;
                matching.UpdatedAt = now;
                if (quantity == item.Quantity)
                {
                    db.ActorItems.Remove(item);
                }
                else
                {
                    item.Quantity -= quantity;
                    item.Version += 1;
                    item.UpdatedAt = now;
                }
                history.Record(target.CampaignId, userId, HistoryKinds.ItemUpdated,
                    $"Ítem '{itemName}' actualizado en '{target.Name}' (+{quantity})");
            }
            else if (quantity == item.Quantity)
            {
                // La fila se mueve tal cual, pero el hueco de equipamiento
                // NO se hereda: SlotId/ContentItemId se validan en EquipAsync
                // (ADR-041) contra el blueprint del actor destino. Llega a
                // la mochila y el nuevo dueño decide dónde equiparlo.
                item.ActorId = target.Id;
                item.SlotId = null;
                item.Version += 1;
                item.UpdatedAt = now;
                history.Record(target.CampaignId, userId, HistoryKinds.ItemCreated,
                    $"Ítem '{itemName}' transferido a '{target.Name}'");
            }
            else
            {
                item.Quantity -= quantity;
                item.Version += 1;
                item.UpdatedAt = now;
                db.ActorItems.Add(new ActorItem
                {
                    Id = Guid.NewGuid(),
                    ActorId = target.Id,
                    Name = item.Name,
                    Quantity = quantity,
                    ContentItemId = item.ContentItemId,
                    Weight = item.Weight,
                    Volume = item.Volume,
                    SlotId = null,
                    Version = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                history.Record(target.CampaignId, userId, HistoryKinds.ItemCreated,
                    $"Ítem '{itemName}' transferido a '{target.Name}'");
            }
            history.Record(source.CampaignId, userId, HistoryKinds.ItemUpdated,
                $"Ítem '{itemName}' transferido desde '{source.Name}' ({quantity})");

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw ApiException.Conflict(
                    "El ítem cambió a la vez desde otro cliente; vuelve a intentarlo con la versión actual.");
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var sourceInventory = await PublishAsync(source, userId, cancellationToken);
        await PublishAsync(target, userId, cancellationToken);
        return sourceInventory;
    }

    public async Task<ActorInventoryDto> DeleteAsync(
        Guid actorId, Guid itemId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var item = await FindItemAsync(actor.Id, itemId, cancellationToken);

        db.ActorItems.Remove(item);
        history.Record(actor.CampaignId, userId, HistoryKinds.ItemDeleted,
            $"Ítem '{item.Name}' eliminado de '{actor.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(actor, userId, cancellationToken);
    }

    /// <summary>
    /// Efectos automáticos del equipamiento (PMB-5.5/6.5): reflejo auditable
    /// de lo que `ApplyEquipment` suma a las tiradas.
    /// </summary>
    public async Task<List<EquipmentEffectDto>> EquipmentEffectsAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireActorAccessAsync(actorId, userId, cancellationToken);
        return await EquipmentEffectsForActorAsync(actor.Id, cancellationToken);
    }

    /// <summary>Lista de efectos automáticos de los objetos equipados.</summary>
    public async Task<List<EquipmentEffectDto>> EquipmentEffectsForActorAsync(
        Guid actorId, CancellationToken cancellationToken = default)
    {
        var equipped = await db.ActorItems.AsNoTracking()
            .Where(item => item.ActorId == actorId
                && item.SlotId != null && item.ContentItemId != null)
            .ToListAsync(cancellationToken);
        var result = new List<EquipmentEffectDto>();
        foreach (var entry in equipped)
        {
            var catalogItem = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == entry.ContentItemId, cancellationToken);
            if (catalogItem is null)
            {
                continue;
            }
            var properties = JsonNode.Parse(catalogItem.PropertiesJson) as JsonObject ?? [];
            foreach (var (key, kind, value) in EffectResolver.AutomaticEntries(properties))
            {
                result.Add(new EquipmentEffectDto(entry.Id, entry.Name, key, kind, value));
            }
        }
        return result;
    }

    /// <summary>
    /// Equipa el ítem en un hueco del sistema (ADR-041): el servidor valida
    /// hueco, compatibilidad de tipo y unicidad salvo `multiple`.
    /// </summary>
    public async Task<ActorInventoryDto> EquipAsync(
        Guid actorId, Guid itemId, Guid userId, EquipActorItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var item = await FindItemAsync(actor.Id, itemId, cancellationToken);
        if (request.Version is not null && item.Version != request.Version.Value)
        {
            throw ApiException.Conflict(
                $"Conflicto de versión: se esperaba {request.Version.Value} pero el ítem está en la versión {item.Version}.");
        }

        var slotId = InputGuard.RequiredText(request.SlotId, "slotId", maxLength: 50);
        var slots = await blueprints.GetEquipmentSlotsAsync(
            actor.RulesetKey, cancellationToken);
        var slot = slots.FirstOrDefault(slot => slot.Id.Equals(
            slotId, StringComparison.OrdinalIgnoreCase))
            ?? throw ApiException.BadRequest(
                $"El hueco '{slotId}' no existe en {actor.RulesetKey}.",
                new Dictionary<string, object?> { ["field"] = "slotId" });

        if (item.ContentItemId is { } contentItemId && slot.Accepts.Length > 0)
        {
            var catalogItem = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == contentItemId, cancellationToken);
            var typeKey = catalogItem is null
                ? null
                : await db.ContentTypes.AsNoTracking()
                    .Where(x => x.Id == catalogItem.TypeId)
                    .Select(x => x.Key)
                    .FirstOrDefaultAsync(cancellationToken);
            if (typeKey is null || !slot.Accepts.Contains(typeKey, StringComparer.OrdinalIgnoreCase))
            {
                throw ApiException.BadRequest(
                    $"El hueco '{slotId}' no acepta objetos de tipo '{typeKey ?? "desconocido"}'.",
                    new Dictionary<string, object?> { ["field"] = "slotId" });
            }
        }

        if (!slot.Multiple && await db.ActorItems.AnyAsync(
            x => x.ActorId == actor.Id && x.Id != item.Id && x.SlotId == slotId,
            cancellationToken))
        {
            throw ApiException.Conflict($"El hueco '{slotId}' ya está ocupado.");
        }

        item.SlotId = slotId;
        item.Version += 1;
        item.UpdatedAt = DateTime.UtcNow;
        history.Record(actor.CampaignId, userId, HistoryKinds.ItemUpdated,
            $"Ítem '{item.Name}' equipado en '{slotId}' para '{actor.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(actor, userId, cancellationToken);
    }

    /// <summary>Devuelve el ítem a la mochila.</summary>
    public async Task<ActorInventoryDto> UnequipAsync(
        Guid actorId, Guid itemId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var item = await FindItemAsync(actor.Id, itemId, cancellationToken);
        item.SlotId = null;
        item.Version += 1;
        item.UpdatedAt = DateTime.UtcNow;
        history.Record(actor.CampaignId, userId, HistoryKinds.ItemUpdated,
            $"Ítem '{item.Name}' devuelto a la mochila de '{actor.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(actor, userId, cancellationToken);
    }

    /// <summary>Ítem de ESTE actor; 404 si no existe o pertenece a otro.</summary>
    private async Task<ActorItem> FindItemAsync(
        Guid actorId, Guid itemId, CancellationToken cancellationToken) =>
        await db.ActorItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ActorId == actorId, cancellationToken)
        ?? throw ApiException.NotFound("El ítem no existe o no pertenece a este actor.");

    private static int ValidateQuantity(int value)
    {
        if (value is < 1 or > MaxQuantity)
        {
            throw ApiException.BadRequest(
                $"La cantidad debe estar entre 1 y {MaxQuantity}.",
                new Dictionary<string, object?> { ["field"] = "quantity" });
        }

        return value;
    }

    private async Task<ActorInventoryDto> BuildInventoryAsync(
        Guid actorId, CancellationToken cancellationToken)
    {
        var items = await db.ActorItems
            .Where(i => i.ActorId == actorId)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        return new ActorInventoryDto(actorId, items.Select(ToDto).ToList());
    }

    /// <summary>Publica `InventoryUpdated` con el inventario resultante.</summary>
    private async Task<ActorInventoryDto> PublishAsync(
        Actor actor, Guid userId, CancellationToken cancellationToken)
    {
        var inventory = await BuildInventoryAsync(actor.Id, cancellationToken);
        var updated = new InventoryUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: actor.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            ActorId: actor.Id,
            Items: inventory.Items);
        await hub.Clients
            .Group(CampaignHub.GroupName(actor.CampaignId))
            .SendAsync(CampaignHub.InventoryUpdatedEventName, updated, cancellationToken);
        return inventory;
    }

    private static ActorItemDto ToDto(ActorItem item) => new(
        item.Id,
        item.ActorId,
        item.Name,
        item.Quantity,
        item.Version,
        item.CreatedAt,
        item.UpdatedAt,
        item.SlotId,
        item.ContentItemId,
        item.Weight,
        item.Volume);
}
