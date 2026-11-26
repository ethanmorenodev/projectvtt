using System.Text.Json;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Actors;

/// <summary>
/// Serialización de los datos JSON del Actor. La validación del contenido
/// la hace el Ruleset; aquí solo se convierte entre JSON y valores.
/// </summary>
public static class ActorJson
{
    public static Dictionary<string, int> ParseAttributes(string json) =>
        Parse<Dictionary<string, int>>(json, nameof(Actor.AttributesJson));

    public static Dictionary<string, ResourceValue> ParseResources(string json) =>
        Parse<Dictionary<string, ResourceValue>>(json, nameof(Actor.ResourcesJson));

    public static string SerializeAttributes(IReadOnlyDictionary<string, int> attributes) =>
        JsonSerializer.Serialize(attributes);

    public static string SerializeResources(IReadOnlyDictionary<string, ResourceValue> resources) =>
        JsonSerializer.Serialize(resources);

    private static T Parse<T>(string json, string field) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json)
                ?? throw new InvalidOperationException(
                    $"Los datos '{field}' del actor están vacíos.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Los datos '{field}' del actor están corruptos.", exception);
        }
    }
}
