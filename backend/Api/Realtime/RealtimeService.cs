using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Realtime;

/// <summary>
/// Lógica autoritativa de tiempo real. El servidor valida membresía y
/// versión esperada, <b>persiste el cambio antes de emitir</b> el evento y
/// registra el comando para que los reintentos sean idempotentes
/// (`ARCHITECTURE.md` §6).
/// </summary>
public sealed class RealtimeService(
    AppDbContext db,
    CampaignService campaigns,
    IHubContext<CampaignHub> hub)
{
    public const string MoveTokenKind = "MoveToken";
    public const string TokenMovedEventName = "TokenMoved";
    private const int CurrentSchemaVersion = 1;

    public async Task AssertMembershipAsync(Guid campaignId, Guid userId,
        CancellationToken cancellationToken = default) =>
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

    /// <summary>Estado autoritativo de la escena (carga inicial y reconexión).</summary>
    public async Task<SceneStateDto> SyncSceneAsync(Guid sceneId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        var tokens = await db.Tokens
            .Where(t => t.SceneId == scene.Id)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return new SceneStateDto(
            scene.Id, scene.CampaignId, scene.Name, scene.GridCellSize, scene.GridShape,
            tokens.Select(ToDto).ToList());
    }

    public async Task<MoveTokenResult> MoveTokenAsync(Guid userId, MoveTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        Validate(command);

        // La membresía se comprueba antes que nada: un reintento jamás
        // devuelve datos a un usuario que ya no tiene acceso.
        var scene = await RequireSceneAccessAsync(command.SceneId, userId, cancellationToken);

        // Reintento del mismo comando: se responde con lo ya aplicado.
        var processed = await FindProcessedAsync(command.CommandId, userId, cancellationToken);
        if (processed is not null)
        {
            return Replay(processed);
        }

        var token = await db.Tokens
            .FirstOrDefaultAsync(t => t.Id == command.TokenId, cancellationToken)
            ?? throw ApiException.NotFound("El token no existe en esta escena.");
        if (token.SceneId != scene.Id)
        {
            throw ApiException.NotFound("El token no existe en esta escena.");
        }

        if (token.Version != command.ExpectedVersion)
        {
            throw ApiException.Conflict(
                $"Conflicto de versión: se esperaba {command.ExpectedVersion} pero el token está en la versión {token.Version}.");
        }

        token.X = command.X;
        token.Y = command.Y;
        token.Version += 1;
        token.UpdatedByUserId = userId;
        token.UpdatedAt = DateTime.UtcNow;

        var result = new MoveTokenResult(
            command.CommandId, token.Id, token.Version, token.X, token.Y, Duplicate: false);

        db.ProcessedCommands.Add(new ProcessedCommand
        {
            Id = command.CommandId,
            UserId = userId,
            CampaignId = scene.CampaignId,
            Kind = MoveTokenKind,
            ResponseJson = JsonSerializer.Serialize(result),
            ProcessedAt = DateTime.UtcNow,
        });

        try
        {
            // Persiste ANTES de emitir: si el evento llega, el estado ya está guardado.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            db.ChangeTracker.Clear();
            var raced = await FindProcessedAsync(command.CommandId, userId, cancellationToken);
            if (raced is not null)
            {
                return Replay(raced);
            }

            if (exception is DbUpdateConcurrencyException)
            {
                throw ApiException.Conflict(
                    "El token cambió a la vez desde otro cliente; vuelve a intentarlo con la versión actual.");
            }

            throw;
        }

        var moved = new TokenMovedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            SceneId: scene.Id,
            TokenId: token.Id,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: CurrentSchemaVersion,
            AggregateVersion: token.Version,
            X: token.X,
            Y: token.Y);

        await hub.Clients
            .Group(CampaignHub.GroupName(scene.CampaignId))
            .SendAsync(TokenMovedEventName, moved, cancellationToken);

        return result;
    }

    private async Task<Scene> RequireSceneAccessAsync(Guid sceneId, Guid userId,
        CancellationToken cancellationToken)
    {
        var scene = await db.Scenes
            .FirstOrDefaultAsync(s => s.Id == sceneId, cancellationToken)
            ?? throw ApiException.NotFound("La escena no existe o no tienes acceso a ella.");

        await campaigns.RequireMemberAsync(scene.CampaignId, userId, cancellationToken);
        return scene;
    }

    private Task<ProcessedCommand?> FindProcessedAsync(Guid commandId, Guid userId,
        CancellationToken cancellationToken) =>
        db.ProcessedCommands.FirstOrDefaultAsync(
            c => c.Id == commandId && c.UserId == userId, cancellationToken);

    private static MoveTokenResult Replay(ProcessedCommand processed)
    {
        var result = JsonSerializer.Deserialize<MoveTokenResult>(processed.ResponseJson)
            ?? throw new InvalidOperationException("La respuesta almacenada del comando está corrupta.");
        return result with { Duplicate = true };
    }

    private static void Validate(MoveTokenCommand command)
    {
        if (command.CommandId == Guid.Empty)
        {
            throw ApiException.BadRequest("El campo 'commandId' es obligatorio.",
                new Dictionary<string, object?> { ["field"] = "commandId" });
        }

        if (command.SceneId == Guid.Empty || command.TokenId == Guid.Empty)
        {
            throw ApiException.BadRequest("Los campos 'sceneId' y 'tokenId' son obligatorios.");
        }

        ValidateCoordinate(command.X, "x");
        ValidateCoordinate(command.Y, "y");
    }

    private static void ValidateCoordinate(double value, string field)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > 1_000_000)
        {
            throw ApiException.BadRequest(
                $"El campo '{field}' debe ser un número finito entre -1000000 y 1000000.",
                new Dictionary<string, object?> { ["field"] = field });
        }
    }

    private static TokenDto ToDto(Token token) => new(
        token.Id, token.SceneId, token.Name, token.X, token.Y,
        token.Version, token.UpdatedByUserId, token.UpdatedAt, token.ActorId);
}
