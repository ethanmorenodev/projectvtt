using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.Actors;

public sealed record ActorDto(
    Guid Id,
    Guid CampaignId,
    string Name,
    string RulesetKey,
    IReadOnlyDictionary<string, int> Attributes,
    IReadOnlyDictionary<string, ResourceValue> Resources,
    long Version,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Ampliación de personaje (PMB-3.3): aditivo para los clientes.
    string Kind,
    Guid? PortraitId,
    string? Biography,
    string? Personality,
    int Level,
    System.Text.Json.Nodes.JsonNode? SystemData,
    System.Text.Json.Nodes.JsonNode? Build);

public sealed record CreateActorRequest(
    string? Name,
    string? RulesetKey,
    Dictionary<string, int>? Attributes,
    Dictionary<string, ResourceValue>? Resources,
    string? Kind,
    Guid? PortraitId,
    string? Biography,
    string? Personality,
    int? Level,
    System.Text.Json.Nodes.JsonNode? SystemData,
    System.Text.Json.Nodes.JsonNode? Build);

/// <summary>
/// Actualización con testigo de concurrencia optimista: `Version` es la
/// versión que el cliente leyó; si no coincide, 409.
/// </summary>
public sealed record UpdateActorRequest(
    long? Version,
    string? Name,
    Dictionary<string, int>? Attributes,
    Dictionary<string, ResourceValue>? Resources,
    string? Kind,
    Guid? PortraitId,
    string? Biography,
    string? Personality,
    int? Level,
    System.Text.Json.Nodes.JsonNode? SystemData,
    System.Text.Json.Nodes.JsonNode? Build);

/// <summary>Ítem del inventario de un Actor: nombre y cantidad genéricos.</summary>
public sealed record ActorItemDto(
    Guid Id,
    Guid ActorId,
    string Name,
    int Quantity,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>Hueco de equipamiento (ADR-041): null = mochila.</summary>
    string? SlotId,
    Guid? ContentItemId,
    double? Weight,
    double? Volume);

/// <summary>
/// Inventario completo de un Actor: lo que devuelven las mutaciones y lo
/// que transporta el evento `InventoryUpdated` (el cliente sustituye la
/// lista entera).
/// </summary>
public sealed record ActorInventoryDto(
    Guid ActorId,
    IReadOnlyList<ActorItemDto> Items);

public sealed record CreateActorItemRequest(
    string? Name,
    int? Quantity,
    /// <summary>Referencia opcional al objeto del catálogo (PMB-6.2).</summary>
    Guid? ContentItemId);

/// <summary>Petición de equipar un ítem en un hueco del sistema (ADR-041).</summary>
public sealed record EquipActorItemRequest(
    long? Version,
    string? SlotId);

/// <summary>
/// Efecto automático aportado por un objeto equipado (PMB-6.5): reflejo
/// auditable de lo que `ApplyEquipment` suma a las tiradas.
/// </summary>
public sealed record EquipmentEffectDto(
    Guid ItemId,
    string ItemName,
    string Key,
    string Kind,
    double Value);

/// <summary>
/// Actualización de ítem con testigo de concurrencia optimista: `Version`
/// es la que leyó el cliente; si no coincide, 409.
/// </summary>
public sealed record UpdateActorItemRequest(
    long? Version,
    string? Name,
    int? Quantity);

/// <summary>
/// Transferencia de un ítem entre actores de la misma campaña (PMB-6.7).
/// `Quantity` null o igual al total mueve la fila; `Version` es el testigo
/// optimista del ítem de origen (si se envía).
/// </summary>
public sealed record TransferActorItemRequest(
    Guid TargetActorId,
    int? Quantity,
    long? Version);

/// <summary>Efecto o condición genérica aplicada a un Actor.</summary>
public sealed record ActorEffectDto(
    Guid Id,
    Guid ActorId,
    string Name,
    DateTime CreatedAt);

/// <summary>
/// Lista completa de efectos de un Actor: lo que devuelven las
/// mutaciones y lo que transporta el evento `EffectsUpdated` (el cliente
/// sustituye su lista).
/// </summary>
public sealed record ActorEffectsDto(
    Guid ActorId,
    IReadOnlyList<ActorEffectDto> Effects);

public sealed record CreateActorEffectRequest(string? Name);
