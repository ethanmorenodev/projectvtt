using System.Text.Json.Nodes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Elecciones configurables (CONTENT_MODEL §9, PMB-5.3): los grupos viven
/// como propiedad estructurada del contenido; ni la definición ni la
/// selección se validan en el cliente. El creador usa el grupo cuyo `id`
/// coincide con la clave del paso (`skills`), con fallback a la regla
/// heredada `countFromClass` para contenido sin grupos.
/// </summary>
public static partial class ChoiceGroupSchema
{
    private static readonly HashSet<string> Timings = new(StringComparer.OrdinalIgnoreCase)
    {
        "creation", "levelUp", "free",
    };

    public static void Validate(JsonArray groups, string field = "properties.choiceGroups")
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (node, index) in groups.Select((node, index) => (node, index)))
        {
            var path = $"{field}[{index}]";
            if (node is not JsonObject group)
            {
                throw Invalid(path, "Cada grupo debe ser un objeto.");
            }
            var id = Text(group, "id", path);
            if (!ids.Add(id))
            {
                throw Invalid(path + ".id", $"Grupo duplicado '{id}'.");
            }
            if (group["options"] is not JsonArray options || options.Count == 0)
            {
                throw Invalid(path + ".options", "El grupo necesita al menos una opción.");
            }
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (optionNode, optionIndex) in options.Select((o, i) => (o, i)))
            {
                var optionPath = $"{path}.options[{optionIndex}]";
                if (optionNode is JsonValue textOption)
                {
                    if (!textOption.TryGetValue<string>(out var keyText) || keyText.Length == 0)
                    {
                        throw Invalid(optionPath, "La opción debe ser texto.");
                    }
                    keys.Add(keyText);
                    continue;
                }
                if (optionNode is not JsonObject option)
                {
                    throw Invalid(optionPath, "La opción debe ser texto u objeto.");
                }
                var key = Text(option, "key", optionPath);
                if (!keys.Add(key))
                {
                    throw Invalid(optionPath + ".key", $"Opción duplicada '{key}'.");
                }
                var referenceNode = option["itemId"];
                if (referenceNode is not null
                    && (referenceNode is not JsonValue reference
                        || !reference.TryGetValue<string>(out var referenceText)
                        || !Guid.TryParse(referenceText, out _)))
                {
                    throw Invalid(optionPath + ".itemId", "Referencia de contenido no válida.");
                }
            }

            if (group["count"] is not JsonValue countValue
                || !countValue.TryGetValue<int>(out var count)
                || count < 1 || count > keys.Count)
            {
                throw Invalid(path + ".count", "El número de elecciones debe estar entre 1 y las opciones.");
            }
            if (group["mandatory"] is not JsonValue mandatory
                || !mandatory.TryGetValue<bool>(out _))
            {
                throw Invalid(path + ".mandatory", "«mandatory» debe ser booleano.");
            }
            var timing = Text(group, "timing", path);
            if (!Timings.Contains(timing))
            {
                throw Invalid(path + ".timing", "Timing no válido (creation|levelUp|free).");
            }
            if (group["changeable"] is not JsonValue changeable
                || !changeable.TryGetValue<bool>(out _))
            {
                throw Invalid(path + ".changeable", "«changeable» debe ser booleano.");
            }
            if (group["constraints"] is JsonObject constraints)
            {
                ValidateConstraintList(constraints, "requires", path);
                ValidateConstraintList(constraints, "excludes", path);
            }
        }
    }

    /// <summary>Grupo con ese id dentro de las propiedades del item, si existe.</summary>
    public static JsonObject? Find(JsonObject properties, string id) =>
        properties["choiceGroups"] is JsonArray groups
            ? groups.OfType<JsonObject>().FirstOrDefault(
                group => group["id"]?.GetValue<string>()?.Equals(
                    id, StringComparison.OrdinalIgnoreCase) == true)
            : null;

    /// <summary>Valida la selección del jugador contra la definición del grupo.</summary>
    public static string[] ValidateSelection(
        JsonObject group, JsonArray? chosen, List<string> errors)
    {
        var id = group["id"]?.GetValue<string>() ?? "grupo";
        var selected = (chosen ?? [])
            .Select(node => node?.GetValue<string>()?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();
        var count = group["count"]?.GetValue<int>() ?? 1;
        var options = (group["options"] as JsonArray ?? [])
            .Select(node => node is JsonObject obj
                ? obj["key"]?.GetValue<string>() ?? string.Empty
                : node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selected.Length != selected.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            errors.Add($"Hay opciones repetidas en «{id}».");
        }
        if (selected.Any(value => !options.Contains(value)))
        {
            errors.Add($"Alguna opción de «{id}» no es válida.");
        }
        if (selected.Length != count)
        {
            errors.Add($"Debes elegir {count} opción(es) en «{id}» (has elegido {selected.Length}).");
        }
        if (group["constraints"] is JsonObject constraints)
        {
            var requires = StringList(constraints, "requires");
            var excludes = StringList(constraints, "excludes");
            if (requires.Any(value => !selected.Contains(value, StringComparer.OrdinalIgnoreCase)))
            {
                errors.Add($"Faltan requisitos de la elección «{id}».");
            }
            if (excludes.Any(value => selected.Contains(value, StringComparer.OrdinalIgnoreCase)))
            {
                errors.Add($"La elección «{id}» incluye una opción excluida.");
            }
        }
        return selected;
    }

    private static void ValidateConstraintList(JsonObject parent, string key, string path)
    {
        if (parent[key] is null)
        {
            return;
        }
        if (parent[key] is not JsonArray list)
        {
            throw Invalid($"{path}.constraints.{key}", "Debe ser una lista de texto.");
        }
        foreach (var (node, index) in list.Select((node, index) => (node, index)))
        {
            if (node is not JsonValue value
                || !value.TryGetValue<string>(out var text) || text.Length == 0)
            {
                throw Invalid($"{path}.constraints.{key}[{index}]", "Cada entrada debe ser texto.");
            }
        }
    }

    private static string[] StringList(JsonObject parent, string key) =>
        parent[key] is JsonArray list
            ? list.Select(node => node?.GetValue<string>() ?? string.Empty)
                .Where(value => value.Length > 0).ToArray()
            : [];

    private static string Text(JsonObject parent, string key, string path)
    {
        if (parent[key] is not JsonValue value
            || !value.TryGetValue<string>(out var text) || text.Length == 0)
        {
            throw Invalid(path + "." + key, $"Falta o no es texto '{key}'.");
        }
        return text;
    }

    private static ApiException Invalid(string field, string detail) =>
        ApiException.BadRequest(detail,
            new Dictionary<string, object?> { ["field"] = field });
}
