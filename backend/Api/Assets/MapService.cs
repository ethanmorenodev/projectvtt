using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Assets;

/// <summary>
/// Mapa persistido de la Escena (Épica D/Épica H, ADR-028): subida con
/// validación de tipo, tamaño y firma binaria (los límites también son
/// configurables: `Assets:MaxBytes`), lectura para miembros y borrado
/// para GM+. Un mapa por escena: subir otro lo reemplaza. Cada cambio
/// emite `MapUpdated` a la campaña.
/// </summary>
public sealed class MapService(
    AppDbContext db,
    CampaignService campaigns,
    SceneService scenes,
    IAssetStore assets,
    IHubContext<CampaignHub> hub,
    IConfiguration configuration)
{
    /// <summary>Tipos admitidos (los mismos que valida la web).</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes = ImageGuard.AllowedContentTypes;

    /// <summary>Límite de tamaño por defecto (configurable con `Assets:MaxBytes`).</summary>
    public const int DefaultMaxBytes = ImageGuard.DefaultMaxBytes;

    public long MaxBytes => configuration.GetValue("Assets:MaxBytes", DefaultMaxBytes);

    private static string AssetKey(Guid sceneId) => $"scene-maps/{sceneId}";

    public async Task<MapAssetDto> UploadAsync(
        Guid sceneId, Guid userId, string fileName, string contentType, Stream content,
        long sizeBytes, CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);

        // Tipo, tamaño y firma (validación compartida: ImageGuard).
        await ImageGuard.ValidateAsync(contentType, sizeBytes, content, MaxBytes, cancellationToken);

        await assets.SaveAsync(AssetKey(scene.Id), content, cancellationToken);

        var now = DateTime.UtcNow;
        var map = await db.SceneMaps.FirstOrDefaultAsync(
            m => m.SceneId == scene.Id, cancellationToken);
        if (map is null)
        {
            map = new SceneMap
            {
                SceneId = scene.Id,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.SceneMaps.Add(map);
        }
        else
        {
            map.FileName = fileName;
            map.ContentType = contentType;
            map.SizeBytes = sizeBytes;
            map.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        var dto = ToDto(map);
        await PublishAsync(scene, dto, userId, cancellationToken);
        return dto;
    }

    /// <summary>Abre el mapa para lectura; null si la escena no tiene.</summary>
    public async Task<(MapAssetDto Map, Stream Content)?> OpenAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await scenes.RequireSceneAccessAsync(sceneId, userId, cancellationToken);
        var map = await db.SceneMaps.AsNoTracking()
            .FirstOrDefaultAsync(m => m.SceneId == scene.Id, cancellationToken);
        if (map is null)
        {
            return null;
        }

        var content = await assets.OpenReadAsync(AssetKey(scene.Id), cancellationToken);
        return content is null ? null : (ToDto(map), content);
    }

    public async Task<MapAssetDto> DeleteAsync(
        Guid sceneId, Guid userId, CancellationToken cancellationToken = default)
    {
        var scene = await RequireGmSceneAsync(sceneId, userId, cancellationToken);
        var map = await db.SceneMaps
            .FirstOrDefaultAsync(m => m.SceneId == scene.Id, cancellationToken)
            ?? throw ApiException.NotFound("La escena no tiene mapa.");

        db.SceneMaps.Remove(map);
        await db.SaveChangesAsync(cancellationToken);
        await assets.DeleteAsync(AssetKey(scene.Id), cancellationToken);

        var dto = ToDto(map);
        await PublishAsync(scene, null, userId, cancellationToken);
        return dto;
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

    private static MapAssetDto ToDto(SceneMap map) => new(
        map.SceneId, map.FileName, map.ContentType, map.SizeBytes, map.UpdatedAt);

    /// <summary>Publica `MapUpdated` (mapa nuevo o eliminado) a la campaña.</summary>
    private async Task PublishAsync(
        Scene scene, MapAssetDto? map, Guid userId, CancellationToken cancellationToken)
    {
        var updated = new MapUpdatedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: scene.CampaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            SceneId: scene.Id,
            Map: map);
        await hub.Clients
            .Group(CampaignHub.GroupName(scene.CampaignId))
            .SendAsync(CampaignHub.MapUpdatedEventName, updated, cancellationToken);
    }
}
