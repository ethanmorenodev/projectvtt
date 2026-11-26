using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Actors;

/// <summary>
/// Actores por HTTP. Reglas de acceso (ADR-012, aceptada 2026-11-04):
/// leer requiere ser miembro de la campaña; crear puede cualquier miembro
/// y el actor queda vinculado a su creador (`createdByUserId`), que es su
/// propietario; editar solo el propietario o GM u Owner. Los atributos y
/// recursos se validan siempre con el Ruleset del Actor antes de persistir,
/// y cada cambio emite `ActorCreated`/`ActorUpdated` al grupo de la campaña
/// vía <see cref="IHubContext{THub}"/>.
/// </summary>
public sealed class ActorService(
    AppDbContext db,
    CampaignService campaigns,
    HistoryService history,
    IRulesetRegistry rulesets,
    IHubContext<CampaignHub> hub)
{
    public async Task<ActorDto> CreateAsync(
        Guid campaignId, Guid userId, CreateActorRequest request,
        CancellationToken cancellationToken = default)
    {
        // Cualquier miembro puede crear su propia ficha; el creador será el
        // propietario del actor (ADR-012).
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        var ruleset = rulesets.Get(
            InputGuard.RequiredText(request.RulesetKey, "rulesetKey", maxLength: 40));
        var attributes = request.Attributes ?? new Dictionary<string, int>();
        var resources = request.Resources ?? new Dictionary<string, ResourceValue>();
        ruleset.ValidateActorData(attributes, resources);

        var now = DateTime.UtcNow;
        var actor = new Actor
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Name = InputGuard.RequiredText(request.Name, "name", maxLength: 100),
            RulesetKey = ruleset.Key,
            AttributesJson = ActorJson.SerializeAttributes(attributes),
            ResourcesJson = ActorJson.SerializeResources(resources),
            Version = 0,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ApplyCharacterFields(
            actor, request.Kind, request.PortraitId, request.Biography,
            request.Personality, request.Level, request.SystemData, request.Build);

        db.Actors.Add(actor);
        history.Record(campaignId, userId, HistoryKinds.ActorCreated,
            $"Ficha '{actor.Name}' creada");
        await db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(actor);
        var created = new ActorCreatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: campaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Actor: dto);
        await hub.Clients
            .Group(CampaignHub.GroupName(campaignId))
            .SendAsync(CampaignHub.ActorCreatedEventName, created, cancellationToken);

        return dto;
    }

    public async Task<ActorDto> UpdateAsync(
        Guid actorId, Guid userId, UpdateActorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireEditableActorAsync(actorId, userId, cancellationToken);

        if (request.Version is null)
        {
            throw ApiException.BadRequest("El campo 'version' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "version" });
        }

        if (actor.Version != request.Version.Value)
        {
            throw ApiException.Conflict(
                $"Conflicto de versión: se esperaba {request.Version.Value} pero el actor está en la versión {actor.Version}.");
        }

        // Se valida siempre el estado completo resultante (p. ej. al cambiar
        // solo el nombre, los atributos actuales deben seguir siendo válidos).
        var attributes = request.Attributes is null
            ? ActorJson.ParseAttributes(actor.AttributesJson)
            : request.Attributes;
        var resources = request.Resources is null
            ? ActorJson.ParseResources(actor.ResourcesJson)
            : request.Resources;

        var ruleset = rulesets.Get(actor.RulesetKey);
        ruleset.ValidateActorData(attributes, resources);

        actor.Name = request.Name is null
            ? actor.Name
            : InputGuard.RequiredText(request.Name, "name", maxLength: 100);
        actor.AttributesJson = ActorJson.SerializeAttributes(attributes);
        actor.ResourcesJson = ActorJson.SerializeResources(resources);
        ApplyCharacterFields(
            actor, request.Kind, request.PortraitId, request.Biography,
            request.Personality, request.Level, request.SystemData, request.Build);
        actor.Version += 1;
        actor.UpdatedAt = DateTime.UtcNow;
        history.Record(actor.CampaignId, userId, HistoryKinds.ActorUpdated,
            $"Ficha '{actor.Name}' actualizada");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ApiException.Conflict(
                "El actor cambió a la vez desde otro cliente; vuelve a intentarlo con la versión actual.");
        }

        var dto = ToDto(actor);
        var updated = new ActorUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: actor.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            AggregateVersion: actor.Version,
            Actor: dto);
        await hub.Clients
            .Group(CampaignHub.GroupName(actor.CampaignId))
            .SendAsync(CampaignHub.ActorUpdatedEventName, updated, cancellationToken);

        return dto;
    }

    public async Task<IReadOnlyList<ActorDto>> ListAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        var actors = await db.Actors
            .Where(a => a.CampaignId == campaignId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return actors.Select(ToDto).ToList();
    }

    public async Task<ActorDto> GetAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAccessAsync(actorId, userId, cancellationToken);
        return ToDto(actor);
    }

    /// <summary>
    /// Devuelve el Actor solo si el usuario es miembro de su campaña;
    /// si no, 404 sin revelar que el actor existe.
    /// </summary>
    internal async Task<Actor> RequireActorAccessAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        var actor = await db.Actors
            .FirstOrDefaultAsync(a => a.Id == actorId, cancellationToken)
            ?? throw ApiException.NotFound("El actor no existe o no tienes acceso a él.");

        await campaigns.RequireMemberAsync(actor.CampaignId, userId, cancellationToken);
        return actor;
    }

    /// <summary>
    /// Actor editable por el usuario (ADR-012): 404 si no es miembro de la
    /// campaña; 403 si el actor es de otro y no tiene rol GM+. Lo usan el
    /// `PATCH` del actor y el servicio de inventario (misma regla, sin
    /// duplicarla).
    /// </summary>
    internal async Task<Actor> RequireEditableActorAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAccessAsync(actorId, userId, cancellationToken);
        if (actor.CreatedByUserId != userId)
        {
            CampaignService.RequireRole(await campaigns.RequireMemberAsync(
                actor.CampaignId, userId, cancellationToken), CampaignRole.GM);
        }

        return actor;
    }

    private static ActorDto ToDto(Actor actor) => new(
        actor.Id,
        actor.CampaignId,
        actor.Name,
        actor.RulesetKey,
        ActorJson.ParseAttributes(actor.AttributesJson),
        ActorJson.ParseResources(actor.ResourcesJson),
        actor.Version,
        actor.CreatedByUserId,
        actor.CreatedAt,
        actor.UpdatedAt,
        actor.Kind,
        actor.PortraitId,
        actor.Biography,
        actor.Personality,
        actor.Level,
        ParseJson(actor.SystemDataJson),
        ParseJson(actor.BuildJson));

    // --- Ampliación de personaje (PMB-3.3, ADR-034) ---------------------

    /// <summary>Aplica los campos de personaje (null = sin cambios).</summary>
    private static void ApplyCharacterFields(
        Actor actor,
        string? kind, Guid? portraitId, string? biography, string? personality,
        int? level, JsonNode? systemData, JsonNode? build)
    {
        if (kind is not null)
        {
            var normalized = kind.Trim().ToLowerInvariant();
            if (normalized is not ("pc" or "npc" or "creature"))
            {
                throw ApiException.BadRequest(
                    "El campo 'kind' debe ser 'pc', 'npc' o 'creature'.",
                    new Dictionary<string, object?> { ["field"] = "kind" });
            }
            actor.Kind = normalized;
        }
        if (portraitId is not null)
        {
            actor.PortraitId = portraitId;
        }
        if (biography is not null)
        {
            actor.Biography = TextOrNull(biography, 10_000);
        }
        if (personality is not null)
        {
            actor.Personality = TextOrNull(personality, 2_000);
        }
        if (level is not null)
        {
            if (level is < 1 or > 20)
            {
                throw ApiException.BadRequest(
                    "El campo 'level' debe estar entre 1 y 20.",
                    new Dictionary<string, object?> { ["field"] = "level" });
            }
            actor.Level = level.Value;
        }
        if (systemData is not null)
        {
            if (systemData is not JsonObject)
            {
                throw ApiException.BadRequest(
                    "El campo 'systemData' debe ser un objeto JSON.",
                    new Dictionary<string, object?> { ["field"] = "systemData" });
            }
            actor.SystemDataJson = systemData.ToJsonString();
        }
        if (build is not null)
        {
            if (build is not JsonObject)
            {
                throw ApiException.BadRequest(
                    "El campo 'build' debe ser un objeto JSON.",
                    new Dictionary<string, object?> { ["field"] = "build" });
            }
            actor.BuildJson = build.ToJsonString();
        }
    }

    private static string? TextOrNull(string value, int maxLength)
    {
        var text = value.Trim();
        return text.Length == 0 ? null : text[..Math.Min(text.Length, maxLength)];
    }

    private static JsonNode? ParseJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);
}
