using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Annotations;

/// <summary>Contratos HTTP de las anotaciones de una escena (ADR-043).</summary>
public sealed record SceneAnnotationPointDto(double X, double Y);

/// <summary>Una anotación persistida, con sus puntos ya resueltos.</summary>
public sealed record SceneAnnotationDto(
    Guid Id,
    Guid SceneId,
    Guid AuthorUserId,
    string Kind,
    IReadOnlyList<SceneAnnotationPointDto> Points,
    string? Text,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Estado completo de las anotaciones de una escena (carga y reconexión).</summary>
public sealed record SceneAnnotationsStateDto(
    Guid SceneId,
    IReadOnlyList<SceneAnnotationDto> Items,
    long Version);

/// <summary>
/// Comando de escritura: la ruta aporta `sceneId`; `commandId` da
/// idempotencia y `expectedVersion` concurrencia optimista. `clear` vacía
/// la escena y devuelve la versión a 0.
/// </summary>
public sealed record SetSceneAnnotationRequest(
    Guid CommandId,
    long ExpectedVersion,
    Guid? AnnotationId,
    string? Kind,
    IReadOnlyList<SceneAnnotationPointDto>? Points,
    string? Text,
    bool Clear);

/// <summary>
/// Evento en vivo `SceneAnnotationsUpdated`: sigue el envelope de
/// `ARCHITECTURE.md` §7 (igual que `FogUpdated`) y notifica el estado
/// completo sin sustituir a la HTTP para carga/reconexión.
/// </summary>
public sealed record SceneAnnotationsUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    SceneAnnotationsStateDto Annotations);

/// <summary>
/// Servidor autoritario de las anotaciones compartidas de una escena
/// (ADR-043). Lee cualquier miembro; escribe solo GM/Owner. Valida antes
/// de persistir, guarda el comando para reintentos idempotentes y emite el
/// evento a la campaña <b>después</b> de persistir.
/// </summary>
public sealed class SceneAnnotationService(
    AppDbContext db,
    CampaignService campaigns,
    SceneService scenes,
    IHubContext<CampaignHub> hub)
{
    public const string KindDraw = "draw";
    public const string KindNote = "note";
    public const string SetCommandKind = "SetSceneAnnotation";
    public const string SceneAnnotationsUpdatedEventName = "SceneAnnotationsUpdated";
    public const int MaxPoints = 500;
    public const int MaxTextLength = 500;
    public const double MinCoordinate = 0;
    public const double MaxCoordinate = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SceneAnnotationsStateDto> GetAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await scenes.RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        return await BuildStateAsync(scene.Id, cancellationToken);
    }

    public async Task<SceneAnnotationsStateDto> SetAsync(
        Guid sceneId, Guid userId, SetSceneAnnotationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Membresía y rol antes que nada: un reintento nunca devuelve datos
        // a un usuario que ya no tiene acceso.
        var scene = await scenes.RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        CampaignService.RequireRole(
            await campaigns.RequireMemberAsync(scene.CampaignId, userId, cancellationToken),
            CampaignRole.GM);
        Validate(request);

        var processed = await FindProcessedAsync(request.CommandId, userId, cancellationToken);
        if (processed is not null)
        {
            return Replay(processed);
        }

        var stored = await db.SceneAnnotations
            .Where(a => a.SceneId == scene.Id)
            .ToListAsync(cancellationToken);
        var currentVersion = NextVersion(stored);
        if (request.ExpectedVersion != currentVersion)
        {
            throw ApiException.Conflict(
                $"Conflicto de versión: se esperaba {request.ExpectedVersion} " +
                $"pero las anotaciones están en la versión {currentVersion}.");
        }

        SceneAnnotationsStateDto state;
        if (request.Clear)
        {
            db.SceneAnnotations.RemoveRange(stored);
            state = new SceneAnnotationsStateDto(scene.Id, [], 0);
        }
        else
        {
            var now = DateTime.UtcNow;
            var kind = request.Kind!;
            var pointsJson = JsonSerializer.Serialize(request.Points ?? [], JsonOptions);
            var id = request.AnnotationId ?? Guid.NewGuid();
            var annotation = stored.FirstOrDefault(a => a.Id == id);
            // Un ID de otra escena no se puede reutilizar aquí (sin IDOR).
            if (annotation is null && request.AnnotationId is not null &&
                await db.SceneAnnotations.AnyAsync(a => a.Id == id, cancellationToken))
            {
                throw ApiException.NotFound("La anotación no existe en esta escena.");
            }

            if (annotation is null)
            {
                annotation = new SceneAnnotation
                {
                    Id = id,
                    SceneId = scene.Id,
                    Kind = kind,
                    PointsJson = pointsJson,
                    CreatedAt = now,
                };
                db.SceneAnnotations.Add(annotation);
                stored.Add(annotation);
            }

            // La versión de la entidad es la versión anterior del estado;
            // así la versión resultante (máxima + 1) avanza exactamente una.
            annotation.AuthorUserId = userId;
            annotation.Kind = kind;
            annotation.PointsJson = pointsJson;
            annotation.Text = request.Text;
            annotation.Version = currentVersion;
            annotation.UpdatedAt = now;
            state = ToState(scene.Id, stored, currentVersion + 1);
        }

        db.ProcessedCommands.Add(new ProcessedCommand
        {
            Id = request.CommandId,
            UserId = userId,
            CampaignId = scene.CampaignId,
            Kind = SetCommandKind,
            ResponseJson = JsonSerializer.Serialize(state, JsonOptions),
            ProcessedAt = DateTime.UtcNow,
        });

        try
        {
            // Persiste ANTES de emitir: si el evento llega, el estado ya está guardado.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var raced = await FindProcessedAsync(request.CommandId, userId, cancellationToken);
            if (raced is not null)
            {
                return Replay(raced);
            }

            // Índice único (SceneId, Version): otra escritura ganó la misma
            // versión entre la lectura y el guardado. Es un conflicto, no un 500.
            throw ApiException.Conflict(
                "Conflicto de versión: otra escritura ganó; vuelve a leer el estado.");
        }

        var updated = new SceneAnnotationsUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Annotations: state);
        await hub.Clients
            .Group(CampaignHub.GroupName(scene.CampaignId))
            .SendAsync(SceneAnnotationsUpdatedEventName, updated, cancellationToken);

        return state;
    }

    // --- Utilidades -------------------------------------------------------

    private async Task<SceneAnnotationsStateDto> BuildStateAsync(
        Guid sceneId, CancellationToken cancellationToken)
    {
        var stored = await db.SceneAnnotations
            .AsNoTracking()
            .Where(a => a.SceneId == sceneId)
            .ToListAsync(cancellationToken);
        return ToState(sceneId, stored, NextVersion(stored));
    }

    private static SceneAnnotationsStateDto ToState(
        Guid sceneId, IEnumerable<SceneAnnotation> stored, long version) =>
        new(sceneId, stored
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(ToDto)
            .ToList(), version);

    private static SceneAnnotationDto ToDto(SceneAnnotation annotation) => new(
        annotation.Id,
        annotation.SceneId,
        annotation.AuthorUserId,
        annotation.Kind,
        JsonSerializer.Deserialize<List<SceneAnnotationPointDto>>(annotation.PointsJson, JsonOptions)
            ?? [],
        annotation.Text,
        annotation.Version,
        annotation.CreatedAt,
        annotation.UpdatedAt);

    /// <summary>Versión del estado: máxima de las anotaciones + 1 (0 si no hay).</summary>
    private static long NextVersion(IReadOnlyCollection<SceneAnnotation> stored) =>
        stored.Count == 0 ? 0 : stored.Max(a => a.Version) + 1;

    private Task<ProcessedCommand?> FindProcessedAsync(Guid commandId, Guid userId,
        CancellationToken cancellationToken) =>
        db.ProcessedCommands.FirstOrDefaultAsync(
            c => c.Id == commandId && c.UserId == userId, cancellationToken);

    private static SceneAnnotationsStateDto Replay(ProcessedCommand processed) =>
        JsonSerializer.Deserialize<SceneAnnotationsStateDto>(processed.ResponseJson, JsonOptions)
            ?? throw new InvalidOperationException("La respuesta almacenada del comando está corrupta.");

    private static void Validate(SetSceneAnnotationRequest request)
    {
        if (request.CommandId == Guid.Empty)
        {
            throw ApiException.BadRequest("El campo 'commandId' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "commandId" });
        }

        if (request.ExpectedVersion < 0)
        {
            throw ApiException.BadRequest("El campo 'expectedVersion' no puede ser negativo.",
                new Dictionary<string, object?> { ["field"] = "expectedVersion" });
        }

        // Un borrado completo no necesita kind/puntos/texto.
        if (request.Clear)
        {
            return;
        }

        if (request.Kind is not (KindDraw or KindNote))
        {
            throw ApiException.BadRequest("El campo 'kind' debe ser 'draw' o 'note'.",
                new Dictionary<string, object?> { ["field"] = "kind" });
        }

        var points = request.Points ?? [];
        if (points.Count > MaxPoints)
        {
            throw ApiException.BadRequest($"El máximo es {MaxPoints} puntos por anotación.",
                new Dictionary<string, object?> { ["field"] = "points" });
        }

        foreach (var point in points)
        {
            if (!IsValidCoordinate(point.X) || !IsValidCoordinate(point.Y))
            {
                throw ApiException.BadRequest(
                    $"Las coordenadas deben ser finitas y estar entre {MinCoordinate} y {MaxCoordinate}.",
                    new Dictionary<string, object?> { ["field"] = "points" });
            }
        }

        if (request.Text?.Length > MaxTextLength)
        {
            throw ApiException.BadRequest($"El campo 'text' no puede superar {MaxTextLength} caracteres.",
                new Dictionary<string, object?> { ["field"] = "text" });
        }
    }

    private static bool IsValidCoordinate(double value) =>
        double.IsFinite(value) && value >= MinCoordinate && value <= MaxCoordinate;
}
