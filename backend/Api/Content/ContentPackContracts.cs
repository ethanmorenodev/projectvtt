using System.Text.Json.Nodes;

namespace ProjectVTT.Api.Content;

// Contratos de paquetes de contenido (ADR-036, PMB-P.8). El consumo por
// clientes se anuncia en PMB-P.7.

/// <summary>Paquete importado con su procedencia.</summary>
public sealed record ContentPackDto(
    Guid Id,
    string Key,
    string Name,
    string Version,
    string System,
    string? SourceUrl,
    string License,
    string? LicenseUrl,
    string[] Attribution,
    DateTime ImportedAt);

public sealed record CreateContentPackRequest(
    string? Key,
    string? Name,
    string? Version,
    string? System,
    string? SourceUrl,
    string? License,
    string? LicenseUrl,
    string[]? Attribution);

/// <summary>Item importado de un paquete (identidad por (paquete, key)).</summary>
public sealed record ImportContentItemRequest(
    Guid? TypeId,
    string? ItemKey,
    string? Name,
    string? Summary,
    string? Description,
    string[]? Tags,
    JsonNode? Properties,
    Dictionary<string, string>? Inheritance,
    string? Message,
    string? ParentItemKey = null);

/// <summary>
/// Resolución explícita de conflictos al reimportar (ADR-036): nunca
/// silenciosa. `Keep` conserva el item existente; `Update` lo actualiza
/// publicando una revisión nueva; `Copy` conserva el anterior y crea una
/// copia con identidad derivada.
/// </summary>
public enum ImportConflictStrategy
{
    Keep,
    Update,
    Copy,
}
