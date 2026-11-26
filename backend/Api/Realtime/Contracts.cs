using ProjectVTT.Api.Actors;
using ProjectVTT.Api.Assets;
using ProjectVTT.Api.Chat;
using ProjectVTT.Api.Combats;
using ProjectVTT.Api.Fog;
using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Scenes;

namespace ProjectVTT.Api.Realtime;

/// <summary>
/// Primer contrato de comando/evento (ver ADR-010 y
/// `packages/contracts/events/`). Los esquemas JSON Schema autoritativos
/// viven en el repositorio; estos tipos deben mantenerse alineados con ellos.
/// </summary>

/// <summary>Comando: mover un token. El `commandId` da idempotencia.</summary>
public sealed record MoveTokenCommand(
    Guid CommandId,
    Guid SceneId,
    Guid TokenId,
    long ExpectedVersion,
    double X,
    double Y);

/// <summary>Respuesta al comando, también devuelta en reintentos (`Duplicate=true`).</summary>
public sealed record MoveTokenResult(
    Guid CommandId,
    Guid TokenId,
    long Version,
    double X,
    double Y,
    bool Duplicate);

/// <summary>
/// Evento `TokenMoved` emitido a la campaña. Sigue el envelope de
/// `ARCHITECTURE.md` §7: eventId, campaignId, sceneId, actorUserId,
/// occurredAt, schemaVersion, aggregateVersion y los datos del cambio.
/// </summary>
public sealed record TokenMovedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid SceneId,
    Guid TokenId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    long AggregateVersion,
    double X,
    double Y);

/// <summary>Estado autoritativo de una escena para carga inicial y resincronización.</summary>
public sealed record SceneStateDto(
    Guid SceneId,
    Guid CampaignId,
    string SceneName,
    int GridCellSize,
    string GridShape,
    IReadOnlyList<TokenDto> Tokens);

/// <summary>
/// Evento `TokenCreated` (aditivo a ADR-010): notifica a la campaña que un
/// token se creó por HTTP, para que los demás clientes lo vean sin recargar.
/// </summary>
public sealed record TokenCreatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid SceneId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    TokenDto Token);

/// <summary>
/// Evento `ActorCreated` (Épica E): notifica a la campaña que un Actor se
/// creó por HTTP. Sin `sceneId` (el Actor es de campaña) ni
/// `aggregateVersion` (versión inicial 0, como `TokenCreated`).
/// </summary>
public sealed record ActorCreatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    ActorDto Actor);

/// <summary>
/// Evento `ActorUpdated`: el Actor cambió (nombre, atributos o recursos);
/// `aggregateVersion` es la nueva versión del Actor.
/// </summary>
public sealed record ActorUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    long AggregateVersion,
    ActorDto Actor);

/// <summary>
/// Evento `RollResolved`: una tirada resuelta por el servidor se publica a
/// todos los miembros con su desglose auditable.
/// </summary>
public sealed record RollResolvedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    RollDto Roll);

/// <summary>
/// Evento `InventoryUpdated` (Épica E): el inventario de un Actor cambió
/// (ítem creado, editado o borrado) y se envía completo para que el
/// cliente sustituya su lista. Sin `aggregateVersion` (la colección no
/// tiene una única versión; cada ítem lleva la suya).
/// </summary>
public sealed record InventoryUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    Guid ActorId,
    IReadOnlyList<ActorItemDto> Items);

/// <summary>
/// Evento `EffectsUpdated` (Épica E): se aplicó o retiró un efecto o
/// condición del Actor y se envía la lista completa para que el cliente
/// la sustituya. Sin `aggregateVersion` (sigue el precedente de
/// <see cref="InventoryUpdatedEvent"/>).
/// </summary>
public sealed record EffectsUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    Guid ActorId,
    IReadOnlyList<ActorEffectDto> Effects);

/// <summary>
/// Evento `MessagePosted` (Épica F): un miembro publicó un mensaje en el
/// chat de campaña y se envía con el nombre de su autor. Sin
/// `aggregateVersion` (sigue el precedente de
/// <see cref="InventoryUpdatedEvent"/>).
/// </summary>
public sealed record MessagePostedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    ChatMessageDto Message);

/// <summary>
/// Evento `DirectMessagePosted` (Épica F, ADR-017): un miembro envió un
/// mensaje privado 1:1 dentro de la campaña. A diferencia de los demás
/// eventos no se envía al grupo de la campaña: el servidor lo entrega
/// solo a las conexiones de los dos participantes. Sin
/// `aggregateVersion` (sigue el precedente de
/// <see cref="InventoryUpdatedEvent"/>).
/// </summary>
public sealed record DirectMessagePostedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    DirectMessageDto Message);

/// <summary>
/// Evento `CombatUpdated` (Épica F, ADR-018): cambió el combate de una
/// Escena (inicio, alta/baja de combatientes, avance de turno o fin) y
/// se envía el estado completo para que el cliente lo sustituya. Sin
/// `aggregateVersion` (sigue el precedente de
/// <see cref="InventoryUpdatedEvent"/>).
/// </summary>
public sealed record CombatUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    CombatStateDto Combat);

/// <summary>
/// Evento `FogUpdated` (Épica F, ADR-020): cambió la niebla de guerra
/// de una Escena y se envía el estado completo (solo celdas reveladas;
/// nada secreto, por eso va a toda la campaña). Sin `aggregateVersion`
/// (sigue el precedente de <see cref="InventoryUpdatedEvent"/>).
/// </summary>
public sealed record FogUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    FogStateDto Fog);

/// <summary>
/// Evento `MapUpdated` (Épica D/Épica H, ADR-028): el mapa de una Escena
/// se subió, reemplazó o eliminó. `Map` es null cuando no queda mapa;
/// los clientes vuelven a pedirlo por HTTP autenticado.
/// </summary>
public sealed record MapUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    Guid SceneId,
    MapAssetDto? Map);

/// <summary>
/// Evento `SceneDeleted` (ADR-045): la escena y su estado dependiente se
/// borraron en el servidor. `MapUpdated` con `Map=null` sigue emitiéndose
/// por compatibilidad con clientes que aún no consumen este evento.
/// </summary>
public sealed record SceneDeletedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    Guid SceneId);
