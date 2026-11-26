using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Rules;

/// <summary>
/// Primer Ruleset: D&amp;D 5e / 5.5e (ADR-005). Atributos: seis
/// puntuaciones de 3 a 30; modificador = piso((valor − 10) / 2). Recursos:
/// puntos de vida actuales/máximos (opcional). Tirada de atributo:
/// 1d20 + modificador + extra + efectos, con CD opcional. El catálogo de
/// efectos (ADR-015) interpreta los nombres libres del Actor: ventaja o
/// desventaja (2d20, se anulan entre sí) y modificadores planos.
/// </summary>
public sealed class Dnd5eRuleset : IRuleset
{
    public const string RulesetKey = "dnd5e";

    private const int MinAttributeValue = 3;
    private const int MaxAttributeValue = 30;
    private const int MaxExtra = 20;
    private const int MaxDc = 100;
    private const int MaxResource = 999;

    private static readonly string[] Abilities =
        ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"];

    /// <summary>Entrada del catálogo de efectos reconocidos.</summary>
    private sealed record CatalogEntry(string Kind, int Value);

    /// <summary>
    /// Catálogo de efectos reconocidos (ADR-015, Propuesta): clave
    /// normalizada (minúsculas, sin tildes, sin espacios sobrantes) →
    /// contribución a la tirada. La ventaja/desventaja es la semántica de
    /// 5e sobre comprobaciones; los modificadores planos son una
    /// simplificación. El catálogo está pendiente de confirmación del
    /// propietario y vive solo en el servidor: la web no lo replica.
    /// </summary>
    private static readonly Dictionary<string, CatalogEntry> EffectCatalog = new(StringComparer.Ordinal)
    {
        // Ventaja en la tirada (2d20, se queda el mayor).
        ["ventaja"] = new(EffectKinds.Advantage, 0),
        ["invisible"] = new(EffectKinds.Advantage, 0),
        // Desventaja en la tirada (2d20, se queda el menor).
        ["desventaja"] = new(EffectKinds.Disadvantage, 0),
        ["envenenado"] = new(EffectKinds.Disadvantage, 0),
        ["aturdido"] = new(EffectKinds.Disadvantage, 0),
        ["cegado"] = new(EffectKinds.Disadvantage, 0),
        ["asustado"] = new(EffectKinds.Disadvantage, 0),
        // Modificador fijo (simplificación; pendiente de confirmación).
        ["bendecido"] = new(EffectKinds.Modifier, 2),
        ["maldito"] = new(EffectKinds.Modifier, -2),
    };

    public string Key => RulesetKey;

    public string DisplayName => "D&D 5e / 5.5e";

    public IReadOnlyList<string> AttributeKeys => Abilities;

    public IReadOnlyList<string> ResourceKeys => ["hitPoints"];

    public void ValidateActorData(
        IReadOnlyDictionary<string, int> attributes,
        IReadOnlyDictionary<string, ResourceValue> resources)
    {
        foreach (var key in attributes.Keys)
        {
            if (!Abilities.Contains(key, StringComparer.Ordinal))
            {
                throw ApiException.BadRequest(
                    $"Atributo desconocido para {DisplayName}: '{key}'.",
                    new Dictionary<string, object?> { ["field"] = $"attributes.{key}" });
            }
        }

        foreach (var ability in Abilities)
        {
            if (!attributes.TryGetValue(ability, out var value))
            {
                throw ApiException.BadRequest(
                    $"Falta el atributo obligatorio '{ability}'.",
                    new Dictionary<string, object?> { ["field"] = $"attributes.{ability}" });
            }

            if (value is < MinAttributeValue or > MaxAttributeValue)
            {
                throw ApiException.BadRequest(
                    $"El atributo '{ability}' debe estar entre {MinAttributeValue} y {MaxAttributeValue}.",
                    new Dictionary<string, object?> { ["field"] = $"attributes.{ability}" });
            }
        }

        foreach (var (key, resource) in resources)
        {
            if (!ResourceKeys.Contains(key, StringComparer.Ordinal))
            {
                throw ApiException.BadRequest(
                    $"Recurso desconocido para {DisplayName}: '{key}'.",
                    new Dictionary<string, object?> { ["field"] = $"resources.{key}" });
            }

            if (resource.Current is < 0 or > MaxResource || resource.Max is < 1 or > MaxResource)
            {
                throw ApiException.BadRequest(
                    $"El recurso '{key}' debe tener 'current' entre 0 y {MaxResource} y 'max' entre 1 y {MaxResource}.",
                    new Dictionary<string, object?> { ["field"] = $"resources.{key}" });
            }

            if (resource.Current > resource.Max)
            {
                throw ApiException.BadRequest(
                    $"El recurso '{key}' no puede tener 'current' mayor que 'max'.",
                    new Dictionary<string, object?> { ["field"] = $"resources.{key}" });
            }
        }
    }

    /// <summary>
    /// Catálogo en datos (PMB-5.6, puente de equivalencia): representa la
    /// misma semántica que <see cref="EffectCatalog"/> pero como contenido.
    /// </summary>
    public const string EffectCatalogJson = """
    {
      "ventaja": { "kind": "advantage", "value": 0 },
      "invisible": { "kind": "advantage", "value": 0 },
      "desventaja": { "kind": "disadvantage", "value": 0 },
      "envenenado": { "kind": "disadvantage", "value": 0 },
      "aturdido": { "kind": "disadvantage", "value": 0 },
      "cegado": { "kind": "disadvantage", "value": 0 },
      "asustado": { "kind": "disadvantage", "value": 0 },
      "bendecido": { "kind": "modifier", "value": 2 },
      "maldito": { "kind": "modifier", "value": -2 }
    }
    """;

    public RollPlan PlanRoll(IReadOnlyList<string> effectNames) =>
        PlanRollCore(effectNames, normalized =>
            EffectCatalog.TryGetValue(normalized, out var entry) ? entry : null);

    /// <summary>
    /// Plan calculado desde el catálogo en datos: debe producir exactamente
    /// el mismo resultado que <see cref="PlanRoll"/> (prueba de equivalencia).
    /// </summary>
    public RollPlan PlanRollFromData(IReadOnlyList<string> effectNames) =>
        PlanRollCore(effectNames, DataLookup);

    private static CatalogEntry? DataLookup(string normalized)
    {
        var node = JsonNode.Parse(EffectCatalogJson)?[normalized];
        return node is JsonObject entry
            ? new CatalogEntry(
                entry["kind"]!.GetValue<string>(), entry["value"]!.GetValue<int>())
            : null;
    }

    private static RollPlan PlanRollCore(
        IReadOnlyList<string> effectNames, Func<string, CatalogEntry?> lookup)
    {
        var applied = new List<AppliedEffect>();
        var hasAdvantage = false;
        var hasDisadvantage = false;
        var modifier = 0;

        foreach (var raw in effectNames)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var entry = lookup(NormalizeEffectName(raw));
            if (entry is null)
            {
                continue; // Nombre fuera del catálogo: solo se muestra en la ficha.
            }

            applied.Add(new AppliedEffect(raw.Trim(), entry.Kind, entry.Value));
            switch (entry.Kind)
            {
                case EffectKinds.Advantage:
                    hasAdvantage = true;
                    break;
                case EffectKinds.Disadvantage:
                    hasDisadvantage = true;
                    break;
                default:
                    modifier += entry.Value;
                    break;
            }
        }

        // Regla de 5e: ventaja y desventaja se anulan entre sí, da igual
        // cuántas fuentes haya. Los modificadores planos siguen computando.
        var rollMode = hasAdvantage == hasDisadvantage
            ? RollModes.Normal
            : hasAdvantage ? RollModes.Advantage : RollModes.Disadvantage;

        return new RollPlan(
            rollMode == RollModes.Normal ? 1 : 2,
            rollMode,
            modifier,
            applied);
    }

    public RollBreakdown ResolveAbilityCheck(
        string attribute,
        IReadOnlyDictionary<string, int> attributes,
        IReadOnlyList<int> dice,
        int extra,
        int? dc,
        RollPlan plan)
    {
        if (dice is null || dice.Count != plan.DiceCount || dice.Count is < 1 or > 2)
        {
            throw new InvalidOperationException(
                "Los dados deben coincidir con el plan de tirada: 1 en normal, 2 con ventaja o desventaja.");
        }

        if (dice.Any(value => value is < 1 or > 20))
        {
            throw new InvalidOperationException("Los dados de una tirada de atributo deben ser 1d20.");
        }

        if (!Abilities.Contains(attribute, StringComparer.Ordinal))
        {
            throw ApiException.BadRequest(
                $"'{attribute}' no es un atributo de {DisplayName}.",
                new Dictionary<string, object?> { ["field"] = "attribute" });
        }

        if (!attributes.TryGetValue(attribute, out var value))
        {
            throw ApiException.BadRequest(
                $"El actor no tiene el atributo '{attribute}'.",
                new Dictionary<string, object?> { ["field"] = "attribute" });
        }

        if (extra is < -MaxExtra or > MaxExtra)
        {
            throw ApiException.BadRequest(
                $"'extra' debe estar entre {-MaxExtra} y {MaxExtra}.",
                new Dictionary<string, object?> { ["field"] = "extra" });
        }

        if (dc is < 0 or > MaxDc)
        {
            throw ApiException.BadRequest(
                $"'dc' debe estar entre 0 y {MaxDc}.",
                new Dictionary<string, object?> { ["field"] = "dc" });
        }

        // El dado conservado depende de la modalidad: el mayor con
        // ventaja, el menor con desventaja, el único en normal.
        var die = plan.RollMode switch
        {
            RollModes.Advantage => dice.Max(),
            RollModes.Disadvantage => dice.Min(),
            _ => dice[0],
        };

        var modifier = Modifier(value);
        var total = die + modifier + extra + plan.EffectModifier;
        var success = dc is null ? (bool?)null : total >= dc.Value;

        var formula = $"{DicePart(dice, die, plan.RollMode)}" +
            $" {Sign(modifier)} {Sign(extra)} {Sign(plan.EffectModifier)} = {total}";
        if (dc is not null)
        {
            formula += $" · CD {dc} → {(success == true ? "superada" : "no superada")}";
        }

        if (plan.Effects.Count > 0)
        {
            formula += $" · Efectos: {string.Join(", ", plan.Effects.Select(DescribeEffect))}";
        }

        return new RollBreakdown(
            RulesetKey, "ability-check", attribute, value, die, modifier, extra,
            plan.EffectModifier, dice.ToList(), plan.RollMode, plan.Effects,
            total, dc, success, formula);
    }

    /// <summary>Modificador de D&amp;D 5e: piso((valor − 10) / 2).</summary>
    public static int Modifier(int attributeValue) =>
        (int)Math.Floor((attributeValue - 10) / 2.0);

    private static string Sign(int value) => value >= 0 ? $"+ {value}" : $"- {-value}";

    /// <summary>Parte de la fórmula con los dados lanzados y la modalidad.</summary>
    private static string DicePart(IReadOnlyList<int> dice, int die, string rollMode) =>
        rollMode == RollModes.Normal
            ? $"1d20({die})"
            : $"2d20({string.Join(",", dice)}) → {die} ({ModeLabel(rollMode)})";

    private static string ModeLabel(string rollMode) => rollMode switch
    {
        RollModes.Advantage => "ventaja",
        RollModes.Disadvantage => "desventaja",
        _ => "normal",
    };

    /// <summary>Entrada de efecto para la fórmula: "Envenenado (desventaja)".</summary>
    private static string DescribeEffect(AppliedEffect effect) => effect.Kind switch
    {
        EffectKinds.Modifier => $"{effect.Name} ({(effect.Value >= 0 ? $"+{effect.Value}" : $"-{-effect.Value}")})",
        _ => $"{effect.Name} ({ModeLabel(effect.Kind)})",
    };

    /// <summary>
    /// Normaliza un nombre tecleado para compararlo con el catálogo:
    /// minúsculas y sin tildes/diacríticos ("ENVENENADO" = "envénenado").
    /// </summary>
    private static string NormalizeEffectName(string name)
    {
        var formD = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var character in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
