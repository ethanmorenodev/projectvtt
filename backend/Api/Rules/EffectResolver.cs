using System.Text.Json.Nodes;
using ProjectVTT.Api.Content;

namespace ProjectVTT.Api.Rules;

/// <summary>
/// Puente entre las habilidades como datos (ADR-040) y el Ruleset (ADR-015):
/// convierte los efectos `auto` en nombres que `PlanRoll` ya sabe interpretar
/// y expone los efectos `manual` como pasos guiados, sin aplicarlos nunca.
/// </summary>
public static class EffectResolver
{
    private static readonly HashSet<string> AutomaticKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "advantage", "disadvantage", "modifier", "condition",
    };

    /// <summary>Nombre de cada efecto automático de la habilidad, en orden.</summary>
    public static IReadOnlyList<string> AutomaticNames(JsonObject properties) =>
        AutomaticEntries(properties).Select(entry => entry.Key).ToList();

    /// <summary>
    /// Entradas automáticas `{key, kind, value}` de un contenido (habilidad
    /// u objeto equipado) que el Ruleset sabe aplicar (PMB-6.5).
    /// </summary>
    public static IReadOnlyList<(string Key, string Kind, double Value)> AutomaticEntries(
        JsonObject properties)
    {
        if (properties["effects"] is not JsonArray effects)
        {
            return [];
        }
        return effects.OfType<JsonObject>()
            .Where(effect =>
            {
                var kind = effect["kind"]?.GetValue<string>() ?? string.Empty;
                return AutomaticKinds.Contains(kind);
            })
            .Select(effect => (
                Key: effect["key"]?.GetValue<string>()?.Trim() ?? string.Empty,
                Kind: effect["kind"]?.GetValue<string>() ?? string.Empty,
                Value: effect["value"] is JsonValue node && node.TryGetValue<double>(out var v)
                    ? v
                    : 0d))
            .Where(entry => entry.Key.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Pasos para resolución asistida: la UI los muestra y la persona confirma;
    /// el servidor no lanza dados ni aplica efectos por ella.
    /// </summary>
    public static IReadOnlyList<string> ManualSteps(JsonObject? properties)
    {
        if (properties?["effects"] is not JsonArray effects)
        {
            return [];
        }
        return effects.OfType<JsonObject>()
            .Where(effect =>
            {
                var kind = effect["kind"]?.GetValue<string>() ?? string.Empty;
                return !AutomaticKinds.Contains(kind);
            })
            .Select(effect => Describe(effect))
            .Where(step => step.Length > 0)
            .ToList();
    }

    private static string Describe(JsonObject effect)
    {
        var key = effect["key"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var kind = effect["kind"]?.GetValue<string>()?.Trim() ?? "manual";
        var value = effect["value"]?.ToJsonString() ?? string.Empty;
        return key.Length == 0
            ? string.Empty
            : $"{key} ({kind}{(value.Length > 0 ? ": " + value : string.Empty)}) — aplicar manualmente";
    }
}
