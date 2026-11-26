using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Actors;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Rules;

/// <summary>
/// Tiradas. El servidor autentica al miembro, el Ruleset planifica e
/// interpreta los efectos del Actor (ADR-015) y resuelve con dados
/// lanzados por el servidor (el cliente nunca envía resultados), y el
/// resultado se persiste con idempotencia por `commandId` en
/// `processed_commands` antes de emitir `RollResolved` a la campaña
/// (`ARCHITECTURE.md` §6). Permisos (ADR-012): cada miembro tira con sus
/// propios actores (creador); GM u Owner con cualquiera de la campaña.
/// </summary>
public sealed class RollService(
    AppDbContext db,
    CampaignService campaigns,
    HistoryService history,
    IRulesetRegistry rulesets,
    IHubContext<CampaignHub> hub,
    ContentService content)
{
    public const string RollKind = "Roll";
    private const int CurrentSchemaVersion = 1;
    private const int HistoryLimit = 50;

    public async Task<RollResultDto> RollAsync(
        Guid campaignId, Guid userId, CreateRollRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CommandId is not { } commandId || commandId == Guid.Empty)
        {
            throw ApiException.BadRequest("El campo 'commandId' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "commandId" });
        }

        if (request.ActorId is not { } actorId || actorId == Guid.Empty)
        {
            throw ApiException.BadRequest("El campo 'actorId' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "actorId" });
        }

        var attribute = InputGuard.RequiredText(
            request.Attribute, "attribute", maxLength: 40);

        // La membresía se comprueba antes que nada: un reintento jamás
        // devuelve datos a un usuario que ya no tiene acceso.
        var membership = await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        // Reintento del mismo comando: se responde con la tirada ya hecha,
        // sin volver a lanzar el dado.
        var processed = await FindProcessedAsync(commandId, userId, cancellationToken);
        if (processed is not null)
        {
            return Replay(processed);
        }

        var actor = await db.Actors
            .FirstOrDefaultAsync(a => a.Id == actorId, cancellationToken)
            ?? throw ApiException.NotFound("El actor no existe o no tienes acceso a él.");
        if (actor.CampaignId != campaignId)
        {
            throw ApiException.NotFound("El actor no existe o no tienes acceso a él.");
        }

        // Cada quien tira con sus propios actores; GM u Owner con
        // cualquiera de la campaña (ADR-012).
        if (actor.CreatedByUserId != userId)
        {
            CampaignService.RequireRole(membership, CampaignRole.GM);
        }

        var ruleset = rulesets.Get(actor.RulesetKey);
        var attributes = ActorJson.ParseAttributes(actor.AttributesJson);

        // Los efectos activos se interpretan en el Ruleset (ADR-015): el
        // Core solo pasa los nombres y lanza los dados que el plan indique.
        var effectNames = await db.ActorEffects
            .Where(effect => effect.ActorId == actorId)
            .Select(effect => effect.Name)
            .ToListAsync(cancellationToken);

        // Habilidades como datos (ADR-040 / PMB-5.4): solo `auto` puede
        // alimentar el plan; `manual` se guía en la UI, nunca se aplica aquí.
        if (request.AbilityId is { } abilityId)
        {
            var ability = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == abilityId, cancellationToken)
                ?? throw ApiException.NotFound("La habilidad no existe.");
            if (!await content.IsVisibleAsync(abilityId, userId, cancellationToken))
            {
                throw ApiException.NotFound("La habilidad no existe.");
            }
            var properties = JsonNode.Parse(ability.PropertiesJson) as JsonObject ?? [];
            var resolution = properties["resolution"]?.GetValue<string>() ?? "manual";
            if (!resolution.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                throw ApiException.BadRequest(
                    "Esta habilidad requiere resolución manual asistida; el servidor no aplica sus efectos.",
                    new Dictionary<string, object?> { ["field"] = "abilityId" });
            }
            effectNames.AddRange(EffectResolver.AutomaticNames(properties));
            // El uso fija la revisión vigente: cambios posteriores no lo alteran.
            await content.CreateUsageAsync(actorId, userId,
                new CreateItemUsageRequest(abilityId), cancellationToken);
        }

        // Equipamiento (PMB-6.5): los objetos equipados con efectos
        // automáticos se aplican a la tirada; el desglose deja constancia.
        var equipped = await db.ActorItems.AsNoTracking()
            .Where(item => item.ActorId == actorId
                && item.SlotId != null && item.ContentItemId != null)
            .ToListAsync(cancellationToken);
        foreach (var entry in equipped)
        {
            var catalogItem = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == entry.ContentItemId, cancellationToken);
            if (catalogItem is null)
            {
                continue;
            }
            var properties = JsonNode.Parse(catalogItem.PropertiesJson) as JsonObject ?? [];
            effectNames.AddRange(EffectResolver.AutomaticNames(properties));
        }

        var plan = ruleset.PlanRoll(effectNames);

        // La aleatoriedad vive en el servidor; el Ruleset solo resuelve.
        var dice = new List<int>(plan.DiceCount);
        for (var index = 0; index < plan.DiceCount; index++)
        {
            dice.Add(Random.Shared.Next(1, 21));
        }

        var breakdown = ruleset.ResolveAbilityCheck(
            attribute, attributes, dice, request.Extra ?? 0, request.Dc, plan);

        var roll = new RollDto(
            commandId, campaignId, actorId, attribute, breakdown, userId, DateTime.UtcNow);
        var result = new RollResultDto(roll, Duplicate: false);

        db.ProcessedCommands.Add(new ProcessedCommand
        {
            Id = commandId,
            UserId = userId,
            CampaignId = campaignId,
            Kind = RollKind,
            ResponseJson = JsonSerializer.Serialize(result),
            ProcessedAt = DateTime.UtcNow,
        });
        history.Record(campaignId, userId, HistoryKinds.RollResolved,
            $"Tirada de '{actor.Name}' ({attribute}): {breakdown.Total}");

        try
        {
            // Persiste ANTES de emitir: si el evento llega, la tirada ya está guardada.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            db.ChangeTracker.Clear();
            var raced = await FindProcessedAsync(commandId, userId, cancellationToken);
            if (raced is not null)
            {
                return Replay(raced);
            }

            if (exception is DbUpdateConcurrencyException)
            {
                throw ApiException.Conflict(
                    "La tirada se registró a la vez desde otro cliente; reintenta con un commandId nuevo.");
            }

            throw;
        }

        var resolved = new RollResolvedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: campaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: CurrentSchemaVersion,
            Roll: roll);
        await hub.Clients
            .Group(CampaignHub.GroupName(campaignId))
            .SendAsync(CampaignHub.RollResolvedEventName, resolved, cancellationToken);

        return result;
    }

    /// <summary>Historial reciente de tiradas de la campaña (miembros).</summary>
    public async Task<IReadOnlyList<RollDto>> ListAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        var rows = await db.ProcessedCommands
            .Where(c => c.CampaignId == campaignId && c.Kind == RollKind)
            .OrderByDescending(c => c.ProcessedAt)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => JsonSerializer.Deserialize<RollResultDto>(row.ResponseJson)?.Roll)
            .Where(roll => roll is not null)
            .Select(roll => Normalize(roll!))
            .ToList();
    }

    private Task<ProcessedCommand?> FindProcessedAsync(Guid commandId, Guid userId,
        CancellationToken cancellationToken) =>
        db.ProcessedCommands.FirstOrDefaultAsync(
            c => c.Id == commandId && c.UserId == userId, cancellationToken);

    private static RollResultDto Replay(ProcessedCommand processed)
    {
        var result = JsonSerializer.Deserialize<RollResultDto>(processed.ResponseJson)
            ?? throw new InvalidOperationException("La respuesta almacenada de la tirada está corrupta.");
        return result with { Roll = Normalize(result.Roll), Duplicate = true };
    }

    /// <summary>
    /// Tiradas persistidas antes de ADR-015: completa el desglose con sus
    /// campos nuevos (dados, modalidad y efectos) para que la respuesta
    /// sea siempre completa. Las tiradas nuevas ya los traen.
    /// </summary>
    private static RollDto Normalize(RollDto roll) =>
        roll with { Breakdown = roll.Breakdown.WithLegacyDefaults() };
}
