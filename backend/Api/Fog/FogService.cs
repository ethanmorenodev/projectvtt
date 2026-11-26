using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Fog;

/// <summary>
/// Niebla de guerra manual de una Escena (Épica F, ADR-020): por
/// celdas del grid de la escena; lo revelado se guarda y lo demás queda
/// oculto para los jugadores (el GM la edita y la ve solo en modo
/// edición). Leer es de miembro; revelar u ocultar es de GM u Owner.
/// Cada mutación devuelve el estado completo y emite `FogUpdated` a la
/// campaña (todos lo necesitan: solo viajan celdas reveladas).
/// </summary>
public sealed class FogService(
    AppDbContext db,
    CampaignService campaigns,
    SceneService scenes,
    IHubContext<CampaignHub> hub)
{
    /// <summary>Límites de sanidad de las coordenadas de celda.</summary>
    public const int MinCell = -10000;
    public const int MaxCell = 10000;

    public async Task<FogStateDto> GetAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await scenes.RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        return await BuildStateAsync(scene, cancellationToken);
    }

    public async Task<FogStateDto> RevealAsync(
        Guid sceneId, Guid userId, SetFogCellRequest request,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        var (x, y) = ValidateCell(request);

        if (!await db.FogCells.AnyAsync(
                c => c.SceneId == scene.Id && c.X == x && c.Y == y, cancellationToken))
        {
            db.FogCells.Add(new FogCell { SceneId = scene.Id, X = x, Y = y });
            await db.SaveChangesAsync(cancellationToken);
        }

        return await PublishAsync(scene, userId, cancellationToken);
    }

    public async Task<FogStateDto> HideAsync(
        Guid sceneId, Guid userId, SetFogCellRequest request,
        CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        var (x, y) = ValidateCell(request);

        var cells = await db.FogCells
            .Where(c => c.SceneId == scene.Id && c.X == x && c.Y == y)
            .ToListAsync(cancellationToken);
        if (cells.Count > 0)
        {
            db.FogCells.RemoveRange(cells);
            await db.SaveChangesAsync(cancellationToken);
        }

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

    private static (int X, int Y) ValidateCell(SetFogCellRequest request)
    {
        if (request.X is not { } x || request.Y is not { } y)
        {
            throw ApiException.BadRequest(
                "Los campos 'x' e 'y' son obligatorios.",
                new Dictionary<string, object?> { ["field"] = request.X is null ? "x" : "y" });
        }

        if (x < MinCell || x > MaxCell || y < MinCell || y > MaxCell)
        {
            throw ApiException.BadRequest(
                $"Las coordenadas deben estar entre {MinCell} y {MaxCell}.",
                new Dictionary<string, object?> { ["field"] = "x" });
        }

        return (x, y);
    }

    private async Task<FogStateDto> BuildStateAsync(
        Scene scene, CancellationToken cancellationToken)
    {
        var revealed = await db.FogCells
            .AsNoTracking()
            .Where(c => c.SceneId == scene.Id)
            .OrderBy(c => c.X)
            .ThenBy(c => c.Y)
            .Select(c => new FogCellDto(c.X, c.Y))
            .ToListAsync(cancellationToken);
        return new FogStateDto(scene.Id, scene.GridCellSize, revealed);
    }

    /// <summary>Publica `FogUpdated` con el estado completo resultante.</summary>
    private async Task<FogStateDto> PublishAsync(
        Scene scene, Guid userId, CancellationToken cancellationToken)
    {
        var state = await BuildStateAsync(scene, cancellationToken);
        var updated = new FogUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Fog: state);
        await hub.Clients
            .Group(CampaignHub.GroupName(scene.CampaignId))
            .SendAsync(CampaignHub.FogUpdatedEventName, updated, cancellationToken);
        return state;
    }
}
