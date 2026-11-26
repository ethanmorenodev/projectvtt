using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Validación profunda de las propiedades de un tipo `ability` (ADR-040,
/// PMB-5.2). `ContentSchema` valida claves y tipos primitivos; aquí se valida
/// la semántica de activación, efectos, duración, usos, costes y requisitos.
/// Ninguna regla se ejecuta en el cliente: el servidor la rechaza con 400.
/// </summary>
public static partial class AbilitySchema
{
    private static readonly HashSet<string> Activations = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "bonus", "reaction", "free",
    };
    private static readonly HashSet<string> Resolutions = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "manual",
    };
    private static readonly HashSet<string> AutomaticKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "advantage", "disadvantage", "modifier", "resource", "condition",
    };

    public static void Validate(JsonObject properties, string field = "properties")
    {
        var activation = Text(properties, "activation", field);
        if (!Activations.Contains(activation))
        {
            throw Invalid(field + ".activation", "Activación no válida (action|bonus|reaction|free).");
        }

        var resolution = Text(properties, "resolution", field);
        if (!Resolutions.Contains(resolution))
        {
            throw Invalid(field + ".resolution", "Resolución no válida (auto|manual).");
        }

        ValidateDuration(Text(properties, "duration", field), field);
        ValidateEffects(properties["effects"] as JsonArray, resolution, field);
        if (properties.TryGetPropertyValue("uses", out var uses) && uses is not null)
        {
            ValidateUses(uses as JsonObject, field);
        }
        if (properties.TryGetPropertyValue("costs", out var costs) && costs is not null)
        {
            ValidateCosts(costs as JsonArray, field);
        }
        if (properties.TryGetPropertyValue("requirements", out var requirements)
            && requirements is not null)
        {
            ValidateRequirements(requirements as JsonArray, field);
        }
    }

    private static void ValidateEffects(JsonArray? effects, string resolution, string field)
    {
        if (effects is null || effects.Count == 0)
        {
            throw Invalid(field + ".effects", "La habilidad debe declarar al menos un efecto.");
        }
        foreach (var (node, index) in effects.Select((node, index) => (node, index)))
        {
            var path = $"{field}.effects[{index}]";
            if (node is not JsonObject effect)
            {
                throw Invalid(path, "Cada efecto debe ser un objeto.");
            }
            var key = Text(effect, "key", path);
            var kind = Text(effect, "kind", path);
            var value = effect["value"]
                ?? throw Invalid(path + ".value", "Falta el valor del efecto.");
            if (value is not JsonValue scalar
                || !(scalar.TryGetValue<double>(out _) || scalar.TryGetValue<string>(out _)))
            {
                throw Invalid(path + ".value", "El valor debe ser número o texto.");
            }
            if (!AutomaticKinds.Contains(kind)
                && resolution.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(path + ".kind",
                    $"El efecto '{key}' no es automatizable: usa resolution=manual.");
            }
            if (kind.Equals("modifier", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("resource", StringComparison.OrdinalIgnoreCase))
            {
                if (value is not JsonValue number || !number.TryGetValue<double>(out _))
                {
                    throw Invalid(path + ".value", "Este efecto exige un valor numérico.");
                }
            }
            if (effect.TryGetPropertyValue("target", out var target) && target is not null
                && target is not JsonValue targetValue)
            {
                throw Invalid(path + ".target", "El destino debe ser texto.");
            }
        }
    }

    private static void ValidateDuration(string duration, string field)
    {
        if (!duration.Equals("instant", StringComparison.OrdinalIgnoreCase)
            && !duration.Equals("concentration", StringComparison.OrdinalIgnoreCase)
            && !RoundsRegex().IsMatch(duration))
        {
            throw Invalid(field + ".duration",
                "Duración no válida (instant|concentration|rounds:N).");
        }
    }

    private static void ValidateUses(JsonObject? uses, string field)
    {
        if (uses is null)
        {
            throw Invalid(field + ".uses", "'uses' debe ser un objeto {max, reset}.");
        }
        if (uses["max"] is not JsonValue max
            || !max.TryGetValue<double>(out var count) || count < 1)
        {
            throw Invalid(field + ".uses.max", "'max' debe ser un entero positivo.");
        }
        _ = Text(uses, "reset", field + ".uses");
    }

    private static void ValidateCosts(JsonArray? costs, string field)
    {
        if (costs is null)
        {
            throw Invalid(field + ".costs", "'costs' debe ser una lista.");
        }
        foreach (var (node, index) in costs.Select((node, index) => (node, index)))
        {
            var path = $"{field}.costs[{index}]";
            if (node is not JsonObject cost)
            {
                throw Invalid(path, "Cada coste debe ser un objeto.");
            }
            _ = Text(cost, "key", path);
            if (cost["amount"] is not JsonValue amount
                || !amount.TryGetValue<double>(out var value) || value <= 0)
            {
                throw Invalid(path + ".amount", "El coste debe ser positivo.");
            }
        }
    }

    private static void ValidateRequirements(JsonArray? requirements, string field)
    {
        if (requirements is null)
        {
            throw Invalid(field + ".requirements", "'requirements' debe ser una lista de texto.");
        }
        foreach (var (node, index) in requirements.Select((node, index) => (node, index)))
        {
            if (node is not JsonValue value
                || !value.TryGetValue<string>(out var text) || text.Length == 0)
            {
                throw Invalid($"{field}.requirements[{index}]", "Cada requisito debe ser texto.");
            }
        }
    }

    private static string Text(JsonObject parent, string key, string field)
    {
        if (parent[key] is not JsonValue value
            || !value.TryGetValue<string>(out var text) || text.Length == 0)
        {
            throw Invalid(field + "." + key, $"Falta o no es texto '{key}'.");
        }
        return text;
    }

    private static ApiException Invalid(string field, string detail) =>
        ApiException.BadRequest(detail,
            new Dictionary<string, object?> { ["field"] = field });

    [GeneratedRegex(@"^rounds:[1-9]\d*$", RegexOptions.IgnoreCase)]
    private static partial Regex RoundsRegex();
}
