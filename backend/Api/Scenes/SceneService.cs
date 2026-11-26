using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Scenes;

/// <summary>
/// Escenas y tokens por HTTP (carga inicial y consultas). Los cambios en
/// tiempo real (movimiento) pasan por el hub de SignalR, que persiste antes
/// de emitir el evento; la creación de tokens emite `TokenCreated` al grupo
/// de la campaña. Reglas de acceso: leer requiere ser miembro de la
/// campaña; crear escena, token o cambiar la configuración requiere GM u Owner.
/// </summary>
public sealed class SceneService(
    AppDbContext db,
    CampaignService campaigns,
    HistoryService history,
    IHubContext<CampaignHub> hub,
    IAssetStore assets)
{
    private const int MinGridCellSize = 8;
    private const int MaxGridCellSize = 512;

    public async Task<SceneDetailDto> CreateSceneAsync(
        Guid campaignId, Guid userId, string? name, CancellationToken cancellationToken = default)
    {
        var member = await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);
        CampaignService.RequireRole(member, CampaignRole.GM);

        var scene = new Scene
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Name = InputGuard.RequiredText(name, "name", maxLength: 100),
            CreatedAt = DateTime.UtcNow,
        };
        db.Scenes.Add(scene);
        history.Record(campaignId, userId, HistoryKinds.SceneCreated,
            $"Escena '{scene.Name}' creada");
        await db.SaveChangesAsync(cancellationToken);

        return ToDetail(scene);
    }

    public async Task<SceneDetailDto> UpdateSceneAsync(
        Guid sceneId, Guid userId, int? gridCellSize, string? gridShape,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(await campaigns.RequireMemberAsync(
            scene.CampaignId, userId, cancellationToken), CampaignRole.GM);

        if (gridCellSize is null)
        {
            throw ApiException.BadRequest("El campo 'gridCellSize' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "gridCellSize" });
        }

        if (gridCellSize is < MinGridCellSize or > MaxGridCellSize)
        {
            throw ApiException.BadRequest(
                $"'gridCellSize' debe estar entre {MinGridCellSize} y {MaxGridCellSize}.",
                new Dictionary<string, object?> { ["field"] = "gridCellSize" });
        }

        if (gridShape is not null)
        {
            if (gridShape is not (GridShapes.Square or GridShapes.Hex))
            {
                throw ApiException.BadRequest(
                    "'gridShape' debe ser 'square' o 'hex'.",
                    new Dictionary<string, object?> { ["field"] = "gridShape" });
            }

            scene.GridShape = gridShape;
        }

        if (scene.GridCellSize != gridCellSize.Value)
        {
            scene.GridCellSize = gridCellSize.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(scene);
    }

    /// <summary>
    /// Borra la escena y su estado dependiente; solo GM u Owner. Los blobs
    /// (mapa y portada) viven fuera de la BD y se borran explícitamente.
    /// </summary>
    public async Task DeleteSceneAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(await campaigns.RequireMemberAsync(
            scene.CampaignId, userId, cancellationToken), CampaignRole.GM);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Tokens, mapa, anotaciones, niebla y combate referencian SceneId con
        // DeleteBehavior.Cascade en AppDbContext: los elimina la propia BD.
        db.Scenes.Remove(scene);
        history.Record(scene.CampaignId, userId, HistoryKinds.SceneDeleted,
            $"Escena '{scene.Name}' eliminada");
        await db.SaveChangesAsync(cancellationToken);

        await assets.DeleteAsync(CoverKey(scene.Id), cancellationToken);
        await assets.DeleteAsync(MapKey(scene.Id), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Compatibilidad: los clientes antiguos siguen recibiendo `MapUpdated`
        // con Map=null. La señal propia del borrado es `SceneDeleted` (ADR-045).
        var updated = new MapUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            SceneId: scene.Id,
            Map: null);
        var deleted = new SceneDeletedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            SceneId: scene.Id);
        var group = hub.Clients.Group(CampaignHub.GroupName(scene.CampaignId));
        await group.SendAsync(CampaignHub.MapUpdatedEventName, updated, cancellationToken);
        await group.SendAsync(CampaignHub.SceneDeletedEventName, deleted, cancellationToken);
    }

    public async Task<IReadOnlyList<SceneSummaryDto>> ListScenesAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        var scenes = await db.Scenes
            .Where(s => s.CampaignId == campaignId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var counts = await db.Tokens
            .Where(t => scenes.Select(s => s.Id).Contains(t.SceneId))
            .GroupBy(t => t.SceneId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return scenes
            .Select(s => new SceneSummaryDto(
                s.Id, s.CampaignId, s.Name, s.GridCellSize, s.GridShape, s.CreatedAt,
                counts.GetValueOrDefault(s.Id), s.CoverContentType is not null))
            .ToList();
    }

    public async Task<SceneDetailDto> GetSceneAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        return ToDetail(scene);
    }

    public async Task<TokenDto> CreateTokenAsync(
        Guid sceneId, Guid userId, string? name, double? x, double? y, Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(await campaigns.RequireMemberAsync(
            scene.CampaignId, userId, cancellationToken), CampaignRole.GM);

        if (actorId is { } linkedActorId)
        {
            // Un token solo puede vincularse a un actor de la MISMA campaña.
            var actorExists = await db.Actors.AnyAsync(
                a => a.Id == linkedActorId && a.CampaignId == scene.CampaignId,
                cancellationToken);
            if (!actorExists)
            {
                throw ApiException.BadRequest(
                    "El actor indicado no existe en esta campaña.",
                    new Dictionary<string, object?> { ["field"] = "actorId" });
            }
        }

        var token = new Token
        {
            Id = Guid.NewGuid(),
            SceneId = sceneId,
            Name = InputGuard.RequiredText(name, "name", maxLength: 100),
            X = ValidateCoordinate(x, 0, "x"),
            Y = ValidateCoordinate(y, 0, "y"),
            ActorId = actorId,
            Version = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Tokens.Add(token);
        history.Record(scene.CampaignId, userId, HistoryKinds.TokenCreated,
            $"Token '{token.Name}' creado en '{scene.Name}'");
        await db.SaveChangesAsync(cancellationToken);

        // Avisa a la campaña para que los demás clientes vean el token sin
        // recargar la escena (el endpoint HTTP no pasa por el hub).
        var created = new TokenCreatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            SceneId: sceneId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Token: ToDto(token));
        await hub.Clients
            .Group(CampaignHub.GroupName(scene.CampaignId))
            .SendAsync(CampaignHub.TokenCreatedEventName, created, cancellationToken);

        return ToDto(token);
    }

    public async Task<IReadOnlyList<TokenDto>> ListTokensAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);

        var tokens = await db.Tokens
            .Where(t => t.SceneId == scene.Id)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens.Select(ToDto).ToList();
    }

    /// <summary>
    /// Devuelve la escena solo si el usuario es miembro de su campaña;
    /// si no, 404 sin revelar que la escena existe.
    /// </summary>
    public async Task<Scene> RequireSceneAccessAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken)
    {
        var scene = await db.Scenes
            .FirstOrDefaultAsync(s => s.Id == sceneId, cancellationToken)
            ?? throw ApiException.NotFound("La escena no existe o no tienes acceso a ella.");

        await campaigns.RequireMemberAsync(scene.CampaignId, userId, cancellationToken);
        return scene;
    }

    /// <summary>Coordenada opcional finita y dentro de un rango razonable.</summary>
    private static double ValidateCoordinate(double? value, double fallback, string field)
    {
        var coordinate = value ?? fallback;
        if (!double.IsFinite(coordinate) || Math.Abs(coordinate) > 1_000_000)
        {
            throw ApiException.BadRequest(
                $"El campo '{field}' debe ser un número finito entre -1000000 y 1000000.",
                new Dictionary<string, object?> { ["field"] = field });
        }

        return coordinate;
    }

    private static TokenDto ToDto(Token token) => new(
        token.Id, token.SceneId, token.Name, token.X, token.Y,
        token.Version, token.UpdatedByUserId, token.UpdatedAt, token.ActorId);

    private static SceneDetailDto ToDetail(Scene scene) => new(
        scene.Id, scene.CampaignId, scene.Name, scene.GridCellSize, scene.GridShape,
        scene.CreatedAt, scene.CoverContentType is not null);

    // --- Portada de escena -------------------------------------------------

    /// <summary>Sube o reemplaza la portada; requiere GM u Owner de la campaña.</summary>
    public async Task UploadCoverAsync(
        Guid sceneId, Guid userId, string contentType, Stream content, long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(await campaigns.RequireMemberAsync(
            scene.CampaignId, userId, cancellationToken), CampaignRole.GM);

        await ImageGuard.ValidateAsync(
            contentType, sizeBytes, content, CampaignService.MaxCoverBytes, cancellationToken);
        await assets.SaveAsync(CoverKey(scene.Id), content, cancellationToken);
        scene.CoverContentType = contentType;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Abre la portada; null si la escena no tiene.</summary>
    public async Task<(string ContentType, Stream Content)?> OpenCoverAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        if (scene.CoverContentType is null)
        {
            return null;
        }
        var content = await assets.OpenReadAsync(CoverKey(scene.Id), cancellationToken);
        return content is null ? null : (scene.CoverContentType, content);
    }

    /// <summary>Borra la portada (GM/Owner; idempotente).</summary>
    public async Task DeleteCoverAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(await campaigns.RequireMemberAsync(
            scene.CampaignId, userId, cancellationToken), CampaignRole.GM);

        await assets.DeleteAsync(CoverKey(scene.Id), cancellationToken);
        scene.CoverContentType = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string CoverKey(Guid sceneId) => $"scene-covers/{sceneId}";

    private static string MapKey(Guid sceneId) => $"scene-maps/{sceneId}";
}
