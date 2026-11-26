using System.Text.Json.Nodes;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Validación de propiedades contra el TypeSchema del tipo (ADR-031 §3).
/// Reglas mínimas del puente: claves conocidas, tipo primitivo correcto y
/// campos obligatorios presentes. Rangos, referencias y validación profunda
/// llegan con PMB-P.6.
/// </summary>
public static class ContentSchema
{
    /// <summary>
    /// Valida <paramref name="properties"/> contra <paramref name="schema"/>.
    /// Lanza <see cref="ApiException"/> con el campo que falla.
    /// </summary>
    public static void Validate(JsonNode? schema, JsonObject? properties, string field = "properties")
    {
        properties ??= [];
        var known = ParseFields(schema);

        foreach (var (key, value) in properties)
        {
            if (!known.TryGetValue(key, out var type))
            {
                throw ApiException.BadRequest(
                    $"La propiedad '{key}' no existe en el esquema del tipo.",
                    new Dictionary<string, object?> { ["field"] = $"{field}.{key}" });
            }

            if (!Matches(type, value))
            {
                throw ApiException.BadRequest(
                    $"La propiedad '{key}' debe ser de tipo '{type}'.",
                    new Dictionary<string, object?> { ["field"] = $"{field}.{key}" });
            }
        }

        foreach (var (key, type) in known)
        {
            if (type.EndsWith('?'))
            {
                continue; // campo opcional
            }
            if (!properties.ContainsKey(key))
            {
                throw ApiException.BadRequest(
                    $"Falta la propiedad obligatoria '{key}'.",
                    new Dictionary<string, object?> { ["field"] = $"{field}.{key}" });
            }
        }
    }

    /// <summary>Claves permitidas → tipo declarado (p. ej. "number?").</summary>
    public static Dictionary<string, string> ParseFields(JsonNode? schema)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (schema?["fields"] is not JsonArray array)
        {
            return fields;
        }

        foreach (var entry in array)
        {
            if (entry is not JsonObject obj)
            {
                continue;
            }
            var key = obj["key"]?.GetValue<string>();
            var type = obj["type"]?.GetValue<string>() ?? "string";
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            var optional = obj["required"]?.GetValue<bool>() == false;
            fields[key] = optional ? $"{type}?" : type;
        }

        return fields;
    }

    private static bool Matches(string declaredType, JsonNode? value)
    {
        var type = declaredType.TrimEnd('?');
        if (value is null)
        {
            return declaredType.EndsWith('?');
        }

        return type switch
        {
            "number" => value is JsonValue v && (v.TryGetValue<double>(out _) || v.TryGetValue<long>(out _)),
            "string" => value is JsonValue v && v.TryGetValue<string>(out _),
            "boolean" => value is JsonValue v && v.TryGetValue<bool>(out _),
            "list" => value is JsonArray,
            "object" => value is JsonObject,
            "reference" => value is JsonValue v && v.TryGetValue<string>(out var s)
                && Guid.TryParse(s, out _),
            _ => true, // tipo desconocido del esquema: no se rechaza aquí
        };
    }
}
