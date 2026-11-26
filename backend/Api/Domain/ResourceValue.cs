namespace ProjectVTT.Api.Domain;

/// <summary>
/// Recurso de un Actor como valor actual/máximo (p. ej. puntos de vida).
/// Es un concepto genérico del Core: qué recursos existen lo decide el
/// Ruleset (`ARCHITECTURE.md` §4).
/// </summary>
public sealed record ResourceValue(int Current, int Max);
