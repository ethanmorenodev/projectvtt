using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectVTT.Api.Characters;

/// <summary>
/// Carga de blueprints seed (`Characters/Blueprints/*.json`) al arrancar,
/// idempotente por (system, key). Los sistemas nuevos añaden su fichero
/// sin tocar el motor (ADR-034).
/// </summary>
public static class CharacterBlueprintLoader
{
    public static async Task ImportAsync(
        IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Characters", "Blueprints");
        if (!Directory.Exists(directory))
        {
            return;
        }

        using var scope = services.CreateScope();
        var blueprints = scope.ServiceProvider.GetRequiredService<CharacterBlueprintService>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(x => x))
        {
            var node = JsonNode.Parse(await File.ReadAllTextAsync(file, cancellationToken))
                as JsonObject ?? throw new InvalidOperationException(
                    $"El blueprint seed '{Path.GetFileName(file)}' no es un objeto JSON.");

            await blueprints.EnsureAsync(
                node["system"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("Blueprint seed sin 'system'."),
                node["key"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("Blueprint seed sin 'key'."),
                node["name"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("Blueprint seed sin 'name'."),
                node["steps"] ?? new JsonArray(),
                node["derivedStats"] ?? new JsonArray(),
                node["startingEquipmentRules"],
                node["equipmentSlots"],
                cancellationToken);
        }
    }
}
