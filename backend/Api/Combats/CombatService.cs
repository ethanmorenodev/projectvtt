using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Combats;

/// <summary>
/// Iniciativa y turnos de una Escena (Épica F, ADR-018): un único
/// encuentro por escena con combatientes de iniciativa manual y turno
/// actual. Leer es de miembro; toda modificación es de GM u Owner (es
/// estado compartido de la mesa, como la configuración de la escena).
/// Cada mutación devuelve el estado completo y emite `CombatUpdated` al
/// grupo de la campaña vía <see cref="IHubContext{THub}"/>.
/// </summary>
public sealed class CombatService(
    AppDbContext db,
    CampaignService campaigns,
    SceneService scenes,
    HistoryService history,
    IHubContext<CampaignHub> hub)
{
    /// <summary>Límites de sanidad de la iniciativa (el servidor valida lo mismo).</summary>
    public const int MinInitiative = -100;
    public const int MaxInitiative = 100;

    public async Task<CombatStateDto> GetAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await scenes.RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        return await BuildStateAsync(scene.Id, cancellationToken);
    }

    public async Task<CombatStateDto> StartAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        if (await db.SceneCombats.AnyAsync(c => c.SceneId == scene.Id, cancellationToken))
        {
            throw ApiException.Conflict("Ya hay un combate en curso en esta escena.");
        }

        db.SceneCombats.Add(new SceneCombat
        {
            SceneId = scene.Id,
            Round = 1,
            CurrentCombatantId = null,
            StartedAt = DateTime.UtcNow,
        });
        history.Record(scene.CampaignId, userId, HistoryKinds.CombatStarted,
            $"Combate iniciado en '{scene.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(scene, userId, cancellationToken);
    }

    public async Task<CombatStateDto> EndAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        var combat = await db.SceneCombats
            .FirstOrDefaultAsync(c => c.SceneId == scene.Id, cancellationToken)
            ?? throw ApiException.Conflict("No hay ningún combate en curso en esta escena.");

        // El encuentro termina y no deja historial (ADR-018): borra los
        // combatientes y el estado.
        db.Combatants
            .RemoveRange(db.Combatants.Where(c => c.SceneId == scene.Id));
        db.SceneCombats.Remove(combat);
        history.Record(scene.CampaignId, userId, HistoryKinds.CombatEnded,
            $"Combate terminado en '{scene.Name}'");
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(scene, userId, cancellationToken);
    }

    public async Task<CombatStateDto> AddCombatantAsync(
        Guid sceneId, Guid userId, CreateCombatantRequest request,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        await RequireCombatAsync(scene.Id, cancellationToken);

        string name;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            name = InputGuard.RequiredText(request.Name, "name", maxLength: 100);
        }
        else if (request.TokenId is { } tokenId)
        {
            // Sin nombre: se toma el del token vinculado.
            name = (await db.Tokens.FirstOrDefaultAsync(
                    t => t.Id == tokenId && t.SceneId == scene.Id, cancellationToken)
                ?? throw ApiException.NotFound(
                    "El token no existe o no pertenece a esta escena.")).Name;
        }
        else
        {
            throw ApiException.BadRequest(
                "El campo 'name' es obligatorio si no se vincula un token.",
                new Dictionary<string, object?> { ["field"] = "name" });
        }

        if (request.TokenId is { } linkedId)
        {
            if (!await db.Tokens.AnyAsync(
                    t => t.Id == linkedId && t.SceneId == scene.Id, cancellationToken))
            {
                throw ApiException.NotFound(
                    "El token no existe o no pertenece a esta escena.");
            }
        }

        var initiative = ValidateInitiative(request.Initiative);

        db.Combatants.Add(new Combatant
        {
            Id = Guid.NewGuid(),
            SceneId = scene.Id,
            Name = name,
            Initiative = initiative,
            TokenId = request.TokenId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(scene, userId, cancellationToken);
    }

    public async Task<CombatStateDto> UpdateCombatantAsync(
        Guid sceneId, Guid combatantId, Guid userId, UpdateCombatantRequest request,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        await RequireCombatAsync(scene.Id, cancellationToken);
        var combatant = await db.Combatants
            .FirstOrDefaultAsync(c => c.Id == combatantId && c.SceneId == scene.Id, cancellationToken)
            ?? throw ApiException.NotFound("El combatiente no existe o no pertenece a esta escena.");

        if (request.Name is not null)
        {
            combatant.Name = InputGuard.RequiredText(request.Name, "name", maxLength: 100);
        }

        if (request.Initiative is not null)
        {
            combatant.Initiative = ValidateInitiative(request.Initiative);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(scene, userId, cancellationToken);
    }

    public async Task<CombatStateDto> RemoveCombatantAsync(
        Guid sceneId, Guid combatantId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        await RequireCombatAsync(scene.Id, cancellationToken);
        var combatant = await db.Combatants
            .FirstOrDefaultAsync(c => c.Id == combatantId && c.SceneId == scene.Id, cancellationToken)
            ?? throw ApiException.NotFound("El combatiente no existe o no pertenece a esta escena.");

        db.Combatants.Remove(combatant);
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(scene, userId, cancellationToken);
    }

    /// <summary>
    /// Avanza el turno al siguiente combatiente por iniciativa
    /// descendente; al dar la vuelta incrementa la ronda. Con el turno
    /// actual vacío pasa al primero sin tocar la ronda.
    /// </summary>
    public async Task<CombatStateDto> AdvanceAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        var combat = await RequireCombatAsync(scene.Id, cancellationToken);

        var ordered = await QueryOrderedAsync(scene.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        if (ordered.Count == 0)
        {
            throw ApiException.Conflict(
                "No hay combatientes en el combate; añade al menos uno.");
        }

        var currentIndex = combat.CurrentCombatantId is { } currentId
            ? ordered.IndexOf(currentId)
            : -1;
        var nextIndex = currentIndex + 1;
        if (currentIndex >= 0 && nextIndex >= ordered.Count)
        {
            // Vuelta completa: nueva ronda.
            nextIndex = 0;
            combat.Round += 1;
        }

        combat.CurrentCombatantId = ordered[nextIndex];
        await db.SaveChangesAsync(cancellationToken);
        return await PublishAsync(scene, userId, cancellationToken);
    }

    // --- Utilidades -------------------------------------------------------

    /// <summary>Garantiza acceso de GM u Owner a la escena (miembro + rol).</summary>
    private async Task<Scene> RequireGmSceneAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken)
    {
        var scene = await scenes.RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(
            await campaigns.RequireMemberAsync(scene.CampaignId, userId, cancellationToken),
            CampaignRole.GM);
        return scene;
    }

    private async Task<SceneCombat> RequireCombatAsync(
        Guid sceneId, CancellationToken cancellationToken) =>
        await db.SceneCombats
            .FirstOrDefaultAsync(c => c.SceneId == sceneId, cancellationToken)
        ?? throw ApiException.Conflict("No hay ningún combate en curso en esta escena.");

    private static int ValidateInitiative(int? initiative)
    {
        if (initiative is not { } value)
        {
            throw ApiException.BadRequest(
                "El campo 'initiative' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "initiative" });
        }

        if (value < MinInitiative || value > MaxInitiative)
        {
            throw ApiException.BadRequest(
                $"El campo 'initiative' debe estar entre {MinInitiative} y {MaxInitiative}.",
                new Dictionary<string, object?> { ["field"] = "initiative" });
        }

        return value;
    }

    /// <summary>Orden de turno: iniciativa descendente; empates por orden de alta.</summary>
    private IQueryable<Combatant> QueryOrderedAsync(Guid sceneId) =>
        db.Combatants
            .Where(c => c.SceneId == sceneId)
            .OrderByDescending(c => c.Initiative)
            .ThenBy(c => c.CreatedAt)
            .ThenBy(c => c.Id);

    private async Task<CombatStateDto> BuildStateAsync(
        Guid sceneId, CancellationToken cancellationToken)
    {
        var combat = await db.SceneCombats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SceneId == sceneId, cancellationToken);
        if (combat is null)
        {
            return new CombatStateDto(sceneId, Active: false, Round: 0,
                CurrentCombatantId: null, Combatants: []);
        }

        var combatants = await QueryOrderedAsync(sceneId)
            .Select(c => new CombatantDto(c.Id, c.SceneId, c.Name, c.Initiative, c.TokenId, c.CreatedAt))
            .ToListAsync(cancellationToken);
        return new CombatStateDto(
            sceneId, Active: true, combat.Round, combat.CurrentCombatantId, combatants);
    }

    /// <summary>Publica `CombatUpdated` con el estado completo resultante.</summary>
    private async Task<CombatStateDto> PublishAsync(
        Scene scene, Guid userId, CancellationToken cancellationToken)
    {
        var state = await BuildStateAsync(scene.Id, cancellationToken);
        var updated = new CombatUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Combat: state);
        await hub.Clients
            .Group(CampaignHub.GroupName(scene.CampaignId))
            .SendAsync(CampaignHub.CombatUpdatedEventName, updated, cancellationToken);
        return state;
    }
}
