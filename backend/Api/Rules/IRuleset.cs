using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.Rules;

/// <summary>
/// Contrato de un Ruleset (`ARCHITECTURE.md` §4): el Core delega en él la
/// interpretación de atributos/recursos y la resolución de tiradas. Los
/// errores de validación se expresan como <see cref="Shared.ApiException"/>
/// 400, para que la API los traduzca en ProblemDetails consistentes.
/// </summary>
public interface IRuleset
{
    /// <summary>Clave estable del sistema (p. ej. "dnd5e").</summary>
    string Key { get; }

    string DisplayName { get; }

    /// <summary>Claves de atributos que exige este Ruleset, en orden de presentación.</summary>
    IReadOnlyList<string> AttributeKeys { get; }

    /// <summary>Claves de recursos que acepta este Ruleset (pueden omitirse).</summary>
    IReadOnlyList<string> ResourceKeys { get; }

    /// <summary>
    /// Valida la ficha completa de un Actor. Se valida siempre el estado
    /// completo resultante (creación o actualización), nunca de forma parcial.
    /// </summary>
    void ValidateActorData(
        IReadOnlyDictionary<string, int> attributes,
        IReadOnlyDictionary<string, ResourceValue> resources);

    /// <summary>
    /// Interpreta los efectos activos de un Actor según el catálogo de
    /// este Ruleset (ADR-015) y devuelve el plan de tirada: cuántos dados
    /// lanza el Core, la modalidad resultante y el modificador fijo
    /// acumulado. Los nombres que no pertenezcan al catálogo se ignoran.
    /// </summary>
    RollPlan PlanRoll(IReadOnlyList<string> effectNames);

    /// <summary>
    /// Resuelve una tirada de atributo con los dados ya lanzados por el
    /// Core (el Ruleset no genera aleatoriedad: el servidor sí; el número
    /// de dados lo indica el plan) y devuelve un desglose auditable: cada
    /// componente y el total son verificables.
    /// </summary>
    /// <exception cref="Shared.ApiException">Atributo o parámetros inválidos (400).</exception>
    /// <exception cref="InvalidOperationException">Dados que no coinciden con el plan.</exception>
    RollBreakdown ResolveAbilityCheck(
        string attribute,
        IReadOnlyDictionary<string, int> attributes,
        IReadOnlyList<int> dice,
        int extra,
        int? dc,
        RollPlan plan);
}
