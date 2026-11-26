namespace ProjectVTT.Api.Combats;

/// <summary>Combatiente del encuentro, ya ordenado por turno.</summary>
public sealed record CombatantDto(
    Guid Id,
    Guid SceneId,
    string Name,
    int Initiative,
    Guid? TokenId,
    DateTime CreatedAt);

/// <summary>
/// Estado completo del combate de una Escena. `Active=false` significa
/// que no hay encuentro en curso (entonces no hay rondas ni
/// combatientes). Viaja completo en cada evento `CombatUpdated`.
/// </summary>
public sealed record CombatStateDto(
    Guid SceneId,
    bool Active,
    int Round,
    Guid? CurrentCombatantId,
    IReadOnlyList<CombatantDto> Combatants);

/// <summary>Alta de combatiente: nombre (o token del que tomarlo) e iniciativa.</summary>
public sealed record CreateCombatantRequest(string? Name, int? Initiative, Guid? TokenId);

/// <summary>Cambio de un combatiente; `null` deja el campo sin tocar.</summary>
public sealed record UpdateCombatantRequest(string? Name, int? Initiative);
