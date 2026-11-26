using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Rules;

/// <summary>Resolución de Rulesets por clave (registrados en Program.cs).</summary>
public interface IRulesetRegistry
{
    /// <summary>Todos los Rulesets disponibles, para el descriptor del cliente.</summary>
    IReadOnlyList<IRuleset> All { get; }

    /// <summary>Ruleset por clave; 400 si no está disponible.</summary>
    IRuleset Get(string key);
}

public sealed class RulesetRegistry(IEnumerable<IRuleset> rulesets) : IRulesetRegistry
{
    private readonly IReadOnlyDictionary<string, IRuleset> _byKey = rulesets
        .ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IRuleset> All => _byKey.Values.ToList();

    public IRuleset Get(string key) =>
        _byKey.TryGetValue(key, out var ruleset)
            ? ruleset
            : throw ApiException.BadRequest(
                $"El ruleset '{key}' no está disponible.",
                new Dictionary<string, object?> { ["field"] = "rulesetKey" });
}
