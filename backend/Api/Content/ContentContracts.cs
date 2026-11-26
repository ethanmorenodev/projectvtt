using System.Text.Json.Nodes;

namespace ProjectVTT.Api.Content;

// Contratos HTTP del modelo de contenido (PMB-P.3). Si se consumen desde
// web/escritorio, su anuncio es obligatorio (PMB-P.7, AGENTS.md §6).

/// <summary>Tipo de contenido (ADR-031 §3): los tipos son datos.</summary>
public sealed record ContentTypeDto(
    Guid Id,
    string System,
    string Key,
    string DisplayName,
    JsonNode? Schema,
    DateTime CreatedAt);

/// <summary>
/// Item de contenido visible para el usuario actual. La secrecía del DM ya
/// está aplicada: los campos privados se eliminan aquí, en el servidor.
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

    /// <summary>Procedencia (ADR-036): paquete de origen del item, si lo hay.</summary>
    string? PackKey,

    /// <summary>Clave del item dentro de su paquete (identidad importada).</summary>
    string? PackItemKey,

    /// <summary>
    /// «Solo DM» (ADR-031 §6). Como el item completo está oculto a no DM
    /// (<see cref="ContentService"/>), la marca no aporta información nueva
    /// a quien no puede verlo. Aditivo aprobado 2026-11-11 (PMB-4.4).
    /// </summary>
    bool IsPrivate,

    /// <summary>
    /// Claves de los campos privados del DM, para que el editor pueda
    /// mostrarlas. Solo se exponen cuando el viewer recibió sus valores
    /// (DM, o item sin autor); hacia el resto va la lista vacía, sin
    /// revelar que existe algún campo oculto.
    /// </summary>
    string[] PrivateFields,

    /// <summary>
    /// Número de usos visibles para el solicitante (PMB-4.5, aditivo
    /// aprobado 2026-11-12): solo cuenta usos de campañas donde es miembro,
    /// la misma regla de <c>GET /content/items/{id}/usages</c>.
    /// </summary>
    int UsageCount);

/// <summary>Revisión inmutable publicada (ADR-031 §7).</summary>
public sealed record ContentRevisionDto(
    Guid Id,
    long Number,
    JsonNode? Payload,
    string? Message,
    Guid? CreatedByUserId,
    DateTime CreatedAt);

/// <summary>Uso de contenido por un personaje, con snapshot materializado.</summary>
public sealed record ItemUsageDto(
    Guid Id,
    Guid ActorId,
    Guid ItemId,
    long RevisionNumber,
    JsonNode Snapshot,
    DateTime CreatedAt);

/// <summary>
/// Resumen de uso para «Ver usos» (PMB-4.5, aditivo aprobado 2026-11-12):
/// sin snapshot, solo visible en campañas donde el solicitante es miembro.
/// </summary>
public sealed record ItemUsageSummaryDto(
    string ActorName,
    string CampaignName,
    long RevisionNumber,
    DateTime CreatedAt);

public sealed record CreateContentTypeRequest(
    string? System,
    string? Key,
    string? DisplayName,
    JsonNode? Schema);

public sealed record CreateContentItemRequest(
    Guid? TypeId,
    string? Name,
    string? Summary,
    string? Description,
    Guid? ParentId,
    string[]? Tags,
    Guid? ImageId,
    string? Focal,
    JsonNode? Properties,
    Dictionary<string, string>? Inheritance,
    string? Scope,
    Guid? CampaignId,
    bool? IsPrivate,
    string[]? PrivateFields);

/// <summary>Actualización parcial: solo lo presente cambia.</summary>
public sealed record UpdateContentItemRequest(
    string? Name,
    string? Summary,
    string? Description,
    Guid? ParentId,
    string[]? Tags,
    Guid? ImageId,
    string? Focal,
    JsonNode? Properties,
    Dictionary<string, string>? Inheritance,
    bool? IsPrivate,
    string[]? PrivateFields);

public sealed record PublishContentRequest(string? Message);

public sealed record CreateItemUsageRequest(Guid? ItemId);

/// <summary>
/// Relación tipada entre items (PMB-4.6, ADR-031 §8). Aditivo aprobado
/// 2026-11-16: no rompe clientes existentes.
/// </summary>
public sealed record ContentRelationDto(
    Guid Id,
    Guid FromItemId,
    string Kind,
    Guid ToItemId,
    string? Label,
    JsonNode? Metadata);

public sealed record CreateContentRelationRequest(
    string? Kind, Guid? ToItemId, string? Label);

/// <summary>
/// Propuesta pendiente de revisión (PMB-4.8): `changes` se calcula en el
/// servidor comparando el borrador con la última revisión publicada.
/// </summary>
public sealed record ContentProposalDto(
    Guid Id,
    string Name,
    string AuthorName,
    string WorkflowState,
    long CurrentRevision,
    string[] Changes,
    DateTime UpdatedAt);

public sealed record RejectContentRequest(string? Reason);

/// <summary>Diff de un borrador/propuesta frente a su última revisión publicada.</summary>
public sealed record ContentDiffDto(
    Guid ItemId, long LastRevision, string[] Changes);

/// <summary>Permiso explícito (ADR-031 §6): usuario/rol → view/edit.</summary>
public sealed record ContentGrantDto(
    Guid Id, Guid ItemId, string SubjectKind, string SubjectId, string Permission);

public sealed record CreateContentGrantRequest(
    string? SubjectKind, string? SubjectId, string? Permission);
