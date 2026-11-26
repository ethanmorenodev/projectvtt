using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Realtime;

/// <summary>
/// Hub de campaña: agrupa las conexiones de una campaña autorizada
/// (`JoinCampaign` verifica la membresía antes de añadir al grupo) y expone
/// el primer contrato de comando (`MoveToken`) con su evento (`TokenMoved`).
/// El token de sesión se acepta como `Authorization: Bearer` o `?access_token=`.
/// </summary>
[Authorize]
public class CampaignHub(RealtimeService realtime) : Hub
{
    public static string GroupName(Guid campaignId) => $"campaign:{campaignId}";

    /// <summary>Nombre del evento `TokenCreated` (emito desde HTTP vía IHubContext).</summary>
    public const string TokenCreatedEventName = "TokenCreated";

    /// <summary>Nombre del evento `ActorCreated` (emito desde HTTP vía IHubContext).</summary>
    public const string ActorCreatedEventName = "ActorCreated";

    /// <summary>Nombre del evento `ActorUpdated` (emito desde HTTP vía IHubContext).</summary>
    public const string ActorUpdatedEventName = "ActorUpdated";

    /// <summary>Nombre del evento `InventoryUpdated` (emito desde HTTP vía IHubContext).</summary>
    public const string InventoryUpdatedEventName = "InventoryUpdated";

    /// <summary>Nombre del evento `EffectsUpdated` (emito desde HTTP vía IHubContext).</summary>
    public const string EffectsUpdatedEventName = "EffectsUpdated";

    /// <summary>Nombre del evento `RollResolved` (emito desde HTTP vía IHubContext).</summary>
    public const string RollResolvedEventName = "RollResolved";

    /// <summary>Nombre del evento `MessagePosted` (emito desde HTTP vía IHubContext).</summary>
    public const string MessagePostedEventName = "MessagePosted";

    /// <summary>Nombre del evento `DirectMessagePosted` (solo a los dos participantes).</summary>
    public const string DirectMessagePostedEventName = "DirectMessagePosted";

    /// <summary>Nombre del evento `CombatUpdated` (emito desde HTTP vía IHubContext).</summary>
    public const string CombatUpdatedEventName = "CombatUpdated";

    /// <summary>Nombre del evento `FogUpdated` (emito desde HTTP vía IHubContext).</summary>
    public const string FogUpdatedEventName = "FogUpdated";

    /// <summary>Nombre del evento `MapUpdated` (emito desde HTTP vía IHubContext).</summary>
    public const string MapUpdatedEventName = "MapUpdated";

    /// <summary>Nombre del evento `SceneDeleted` (emito desde HTTP vía IHubContext).</summary>
    public const string SceneDeletedEventName = "SceneDeleted";

    public async Task JoinCampaign(string campaignId)
    {
        var id = ParseId(campaignId, "campaignId");
        await Guard(async () =>
        {
            await realtime.AssertMembershipAsync(id, GetUserId());
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(id));
        });
    }

    public async Task LeaveCampaign(string campaignId)
    {
        var id = ParseId(campaignId, "campaignId");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(id));
    }

    /// <summary>Estado autoritativo para carga inicial y reconexión.</summary>
    public Task<SceneStateDto> SyncScene(string sceneId)
    {
        var id = ParseId(sceneId, "sceneId");
        return Guard(() => realtime.SyncSceneAsync(id, GetUserId()));
    }

    /// <summary>Comando validado, persistido y confirmado con `TokenMoved`.</summary>
    public Task<MoveTokenResult> MoveToken(MoveTokenCommand command) =>
        Guard(() => realtime.MoveTokenAsync(GetUserId(), command));

    private static Guid ParseId(string value, string field)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            throw new HubException($"El campo '{field}' debe ser un identificador válido.");
        }

        return id;
    }

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            throw new HubException("Se requiere una sesión válida.");
        }

        return userId;
    }

    /// <summary>
    /// Convierte los errores controlados de la API en `HubException`, cuyo
    /// mensaje sí se transmite al cliente (los demás errores se ocultan).
    /// </summary>
    private static async Task Guard(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ApiException exception)
        {
            throw new HubException(exception.Detail);
        }
    }

    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiException exception)
        {
            throw new HubException(exception.Detail);
        }
    }
}
