using System.Text.Json.Nodes;

namespace ProjectVTT.Desktop.Api;

/// <summary>
/// DTO del vertical slice (login, campañas, escenas, mapa, tokens y
/// movimiento en vivo). Deben mantenerse alineados con los contratos del
/// backend (`backend/Api/.../Dtos.cs`, `Realtime/Contracts.cs`) y con la
/// referencia web (`apps/web/src/api/types.ts`). El JSON va en camelCase
/// con enums como texto (`gridShape`: "square" | "hex").
/// </summary>
public sealed record UserDto(Guid Id, string UserName);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public sealed record RegisterRequest(string? UserName, string? Password);

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record CampaignSummary(
    Guid Id, string Name, string Role, DateTime CreatedAt, DateTime? ArchivedAt);

public sealed record CreateCampaignRequest(string? Name);

public sealed record SceneSummary(
    Guid Id, Guid CampaignId, string Name, int GridCellSize, string GridShape,
    DateTime CreatedAt, int TokenCount);

public sealed record SceneDetail(
    Guid Id, Guid CampaignId, string Name, int GridCellSize, string GridShape,
    DateTime CreatedAt);

public sealed record CreateSceneRequest(string? Name);

public sealed record TokenDto(
    Guid Id, Guid SceneId, string Name, double X, double Y, long Version,
    Guid? UpdatedByUserId, DateTime UpdatedAt, Guid? ActorId);

public sealed record CreateTokenRequest(string? Name, double? X, double? Y, Guid? ActorId);

/// <summary>Estado autoritativo de la escena (carga inicial y resync).</summary>
public sealed record SceneStateDto(
    Guid SceneId, Guid CampaignId, string SceneName, int GridCellSize,
    string GridShape, List<TokenDto> Tokens);

public sealed record MoveTokenCommand(
    Guid CommandId, Guid SceneId, Guid TokenId, long ExpectedVersion, double X, double Y);

public sealed record MoveTokenResult(
    Guid CommandId, Guid TokenId, long Version, double X, double Y, bool Duplicate);

public sealed record TokenMovedEvent(
    Guid EventId, Guid CampaignId, Guid SceneId, Guid TokenId, Guid ActorUserId,
    DateTime OccurredAt, int SchemaVersion, long AggregateVersion, double X, double Y);

public sealed record TokenCreatedEvent(
    Guid EventId, Guid CampaignId, Guid SceneId, Guid ActorUserId, DateTime OccurredAt,
    int SchemaVersion, TokenDto Token);

public sealed record MapAssetDto(
    Guid SceneId, string FileName, string ContentType, long SizeBytes, DateTime UpdatedAt);

public sealed record MapUpdatedEvent(
    Guid EventId, Guid CampaignId, Guid ActorUserId, DateTime OccurredAt, int SchemaVersion,
    Guid SceneId, MapAssetDto? Map);

// --- Rebanada 2: biblioteca de contenido (solo lectura) ------------------

/// <summary>Espejo de `ContentService`/`ContentEndpoints` (ADR-031).</summary>
public sealed record ContentTypeDto(
    Guid Id, string System, string Key, string DisplayName, JsonNode? Schema, DateTime CreatedAt);

/// <summary>
/// Espejo de `ContentItemDto`: el servidor ya aplicó ámbitos, grants y
/// secrecía del DM; la vista solo presenta lo que llega.
/// </summary>
public sealed record ContentItemDto(
    Guid Id,
    string System,
    Guid TypeId,
    string TypeKey,
    string Name,
    string? Summary,
    string? Description,
    Guid? ParentId,
    string[] Tags,
    Guid? ImageId,
    string? Focal,
    JsonNode Properties,
    Dictionary<string, string> Inheritance,
    string Scope,
    Guid? CampaignId,
    string WorkflowState,
    long CurrentRevision,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? PackKey,
    string? PackItemKey,
    bool IsPrivate,
    string[] PrivateFields,
    int UsageCount);

// --- Rebanada 3: superficie de actor y anotaciones (ADR-043) -------------

public sealed record ResourceValue(int Current, int Max);

/// <summary>Espejo de `ActorDto` (incluye los campos aditivos de PMB-3.3).</summary>
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
    string Kind,
    Guid? PortraitId,
    string? Biography,
    string? Personality,
    int Level,
    JsonNode? SystemData,
    JsonNode? Build);

public sealed record SceneAnnotationPointDto(double X, double Y);

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

public sealed record SceneAnnotationsStateDto(
    Guid SceneId, IReadOnlyList<SceneAnnotationDto> Items, long Version);

public sealed record SceneAnnotationsUpdatedEvent(
    Guid EventId,
    Guid CampaignId,
    Guid ActorUserId,
    DateTime OccurredAt,
    int SchemaVersion,
    SceneAnnotationsStateDto Annotations);
