using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Actors;

/// <summary>
/// Efectos y condiciones del Actor (Épica E): nombres genéricos sin
/// duración (ADR-014). Permisos idénticos a los de editar la ficha
/// (ADR-012): leer requiere ser miembro; aplicar y retirar requiere ser
/// el propietario del Actor o GM u Owner. Una condición no se aplica dos
/// veces al mismo actor (índice único → 409). Cada mutación devuelve la
/// lista completa y emite `EffectsUpdated` al grupo de la campaña vía
/// <see cref="IHubContext{THub}"/>.
/// </summary>
public sealed class ActorEffectService(
    AppDbContext db,
    ActorService actors,
    HistoryService history,
    IHubContext<CampaignHub> hub)
{
    public async Task<ActorEffectsDto> ListAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireActorAccessAsync(actorId, userId, cancellationToken);
        return await BuildEffectsAsync(actor.Id, cancellationToken);
    }

    public async Task<ActorEffectsDto> ApplyAsync(
        Guid actorId, Guid userId, CreateActorEffectRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var name = InputGuard.RequiredText(request.Name, "name", maxLength: 100);

        if (await db.ActorEffects.AnyAsync(
                e => e.ActorId == actor.Id && e.Name == name, cancellationToken))
        {
            throw ApiException.Conflict(
                $"El efecto '{name}' ya está aplicado a este actor.");
        }

        db.ActorEffects.Add(new ActorEffect
        {
            Id = Guid.NewGuid(),
            ActorId = actor.Id,
            Name = name,
            CreatedAt = DateTime.UtcNow,
        });
        history.Record(actor.CampaignId, userId, HistoryKinds.EffectApplied,
            $"Efecto '{name}' aplicado a '{actor.Name}'");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Índice único (ActorId, Name): otro cliente lo aplicó entre
            // la comprobación y el guardado.
            throw ApiException.Conflict(
                $"El efecto '{name}' ya está aplicado a este actor.");
        }

        return await PublishAsync(actor, userId, cancellationToken);
    }

    public async Task<ActorEffectsDto> RemoveAsync(
        Guid actorId, Guid effectId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await actors.RequireEditableActorAsync(actorId, userId, cancellationToken);
        var effect = await db.ActorEffects
            .FirstOrDefaultAsync(
                e => e.Id == effectId && e.ActorId == actor.Id, cancellationToken)
            ?? throw ApiException.NotFound("El efecto no existe o no pertenece a este actor.");

        db.ActorEffects.Remove(effect);
        history.Record(actor.CampaignId, userId, HistoryKinds.EffectRemoved,
            $"Efecto '{effect.Name}' retirado de '{actor.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(actor, userId, cancellationToken);
    }

    private async Task<ActorEffectsDto> BuildEffectsAsync(
        Guid actorId, CancellationToken cancellationToken)
    {
        var effects = await db.ActorEffects
            .Where(e => e.ActorId == actorId)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
        return new ActorEffectsDto(actorId, effects.Select(ToDto).ToList());
    }

    /// <summary>Publica `EffectsUpdated` con la lista resultante.</summary>
    private async Task<ActorEffectsDto> PublishAsync(
        Actor actor, Guid userId, CancellationToken cancellationToken)
    {
        var effects = await BuildEffectsAsync(actor.Id, cancellationToken);
        var updated = new EffectsUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: actor.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            ActorId: actor.Id,
            Effects: effects.Effects);
        await hub.Clients
            .Group(CampaignHub.GroupName(actor.CampaignId))
            .SendAsync(CampaignHub.EffectsUpdatedEventName, updated, cancellationToken);
        return effects;
    }

    private static ActorEffectDto ToDto(ActorEffect effect) => new(
        effect.Id,
        effect.ActorId,
        effect.Name,
        effect.CreatedAt);
}
