using System.Text.Json.Nodes;

namespace ProjectVTT.Api.Characters;

// Contratos de creación de personajes (PMB-3.2/3.3, ADR-034). El consumo
// por clientes se anuncia en PMB-P.7.

/// <summary>Blueprint visible para el cliente (el asistente lo renderiza).</summary>
public sealed record CharacterBlueprintDto(
    Guid Id,
    string System,
    string Key,
    string Name,
    JsonNode Steps,
    JsonNode DerivedStats,
    JsonNode? StartingEquipmentRules);

/// <summary>
/// Previsualización de un build: los pasos recogidos hasta el momento,
/// validados en servidor contra el blueprint y el contenido de la campaña.
/// </summary>
public sealed record BuildPreviewRequest(
    string? System,
    string? BlueprintKey,
    Guid? CampaignId,
    JsonNode? Steps);

public sealed record BuildStepResult(string Step, bool Ok, string[] Errors);

public sealed record BuildPreviewResponse(
    bool Valid,
    BuildStepResult[] StepResults,
    JsonNode DerivedStats);
