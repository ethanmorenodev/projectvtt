namespace ProjectVTT.Api.Rules;

/// <summary>Modalidad de una tirada tras interpretar los efectos (ADR-015).</summary>
public static class RollModes
{
    public const string Normal = "normal";
    public const string Advantage = "advantage";
    public const string Disadvantage = "disadvantage";
}

/// <summary>Tipo de contribución de un efecto del catálogo de un Ruleset.</summary>
public static class EffectKinds
{
    public const string Advantage = RollModes.Advantage;
    public const string Disadvantage = RollModes.Disadvantage;
    public const string Modifier = "modifier";
}

/// <summary>
/// Efecto reconocido del catálogo que ha contribuido a una tirada.
/// <c>Value</c> es el bonificador fijo (0 para ventaja/desventaja).
/// </summary>
public sealed record AppliedEffect(string Name, string Kind, int Value);

/// <summary>
/// Plan de tirada derivado de los efectos activos del Actor (ADR-015):
/// cuántos dados lanza el servidor (1 en normal, 2 con ventaja o
/// desventaja), la modalidad resultante, el modificador fijo acumulado
/// y los efectos reconocidos del catálogo.
/// </summary>
public sealed record RollPlan(
    int DiceCount,
    string RollMode,
    int EffectModifier,
    IReadOnlyList<AppliedEffect> Effects);

/// <summary>Desglose auditable de una tirada resuelta por un Ruleset.</summary>
/// <param name="RulesetKey">Ruleset que resolvió la tirada.</param>
/// <param name="Kind">Tipo de tirada (p. ej. "ability-check").</param>
/// <param name="Attribute">Clave del atributo tirado.</param>
/// <param name="AttributeValue">Valor del atributo en la ficha.</param>
/// <param name="Die">Dado conservado en el total (el mayor con ventaja, el menor con desventaja, el único en normal).</param>
/// <param name="Modifier">Modificador derivado del atributo.</param>
/// <param name="Extra">Bonificador adicional aceptado en la petición.</param>
/// <param name="EffectModifier">Modificador fijo acumulado de los efectos reconocidos del catálogo.</param>
/// <param name="Dice">Dados lanzados por el servidor en orden de tirada.</param>
/// <param name="RollMode">Modalidad resultante ("normal", "advantage" o "disadvantage").</param>
/// <param name="Effects">Efectos del catálogo reconocidos que contribuyeron a la tirada.</param>
/// <param name="Total">Suma final (die + modifier + extra + effectModifier).</param>
/// <param name="dc">Clase de dificultad opcional.</param>
/// <param name="success">Resultado frente a la CD (null si no hubo CD).</param>
/// <param name="Formula">Representación textual del cálculo, para auditar.</param>
public sealed record RollBreakdown(
    string RulesetKey,
    string Kind,
    string Attribute,
    int AttributeValue,
    int Die,
    int Modifier,
    int Extra,
    int EffectModifier,
    IReadOnlyList<int> Dice,
    string RollMode,
    IReadOnlyList<AppliedEffect> Effects,
    int Total,
    int? Dc,
    bool? Success,
    string Formula)
{
    /// <summary>
    /// Completa el desglose de tiradas guardadas antes de ADR-015
    /// (historial e idempotencia en `processed_commands`): rellena los
    /// campos nuevos con su equivalente para que la respuesta sea
    /// siempre completa. Las tiradas nuevas no lo necesitan.
    /// </summary>
    public RollBreakdown WithLegacyDefaults() => this with
    {
        Dice = Dice is { Count: > 0 } ? Dice : [Die],
        RollMode = RollMode ?? RollModes.Normal,
        Effects = Effects ?? [],
    };
}

/// <summary>Tirada resuelta y persistida (el `RollId` es su `commandId`).</summary>
public sealed record RollDto(
    Guid RollId,
    Guid CampaignId,
    Guid ActorId,
    string Attribute,
    RollBreakdown Breakdown,
    Guid RolledByUserId,
    DateTime OccurredAt);

/// <summary>Respuesta HTTP de una tirada; `Duplicate=true` en reintentos.</summary>
public sealed record RollResultDto(RollDto Roll, bool Duplicate);

/// <summary>Comando `RequestRoll` (ARCHITECTURE.md §7) por HTTP.</summary>
public sealed record CreateRollRequest(
    Guid? CommandId,
    Guid? ActorId,
    string? Attribute,
    int? Extra,
    int? Dc,
    /// <summary>Habilidad opcional que aporta efectos a la tirada (PMB-5.4).</summary>
    Guid? AbilityId = null);

/// <summary>Descriptor de un Ruleset disponible (metadatos para el cliente).</summary>
public sealed record RulesetDto(
    string Key,
    string DisplayName,
    IReadOnlyList<string> AttributeKeys,
    IReadOnlyList<string> ResourceKeys);
