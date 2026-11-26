using System.Text.Json;
using ProjectVTT.Api.Rules;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Desglose de tiradas persistidas antes de ADR-015 (historial e
/// idempotencia en `processed_commands`, escrito con el serializador por
/// defecto, en PascalCase): al leerlo se completan los campos nuevos
/// para que la respuesta sea siempre completa.
/// </summary>
public class RollBreakdownTests
{
    [Fact]
    public void LegacyBreakdown_WithoutAtr015Fields_GetsDefaults()
    {
        const string legacyJson = """
            {"RulesetKey":"dnd5e","Kind":"ability-check","Attribute":"strength",
             "AttributeValue":16,"Die":17,"Modifier":3,"Extra":2,"Total":22,
             "Dc":15,"Success":true,"Formula":"1d20(17) + 3 + 2 = 22 · CD 15 → superada"}
            """;

        var legacy = JsonSerializer.Deserialize<RollBreakdown>(legacyJson);
        Assert.NotNull(legacy);

        var breakdown = legacy!.WithLegacyDefaults();
        Assert.Equal(new[] { 17 }, breakdown.Dice);
        Assert.Equal(RollModes.Normal, breakdown.RollMode);
        Assert.Empty(breakdown.Effects);
        Assert.Equal(0, breakdown.EffectModifier);
        // El resto del desglose no cambia.
        Assert.Equal(22, breakdown.Total);
        Assert.True(breakdown.Success);
        Assert.Equal("1d20(17) + 3 + 2 = 22 · CD 15 → superada", breakdown.Formula);
    }

    [Fact]
    public void CurrentBreakdown_IsUnchangedByLegacyDefaults()
    {
        var current = new RollBreakdown(
            RulesetKey: "dnd5e",
            Kind: "ability-check",
            Attribute: "strength",
            AttributeValue: 16,
            Die: 18,
            Modifier: 3,
            Extra: 0,
            EffectModifier: 2,
            Dice: [7, 18],
            RollMode: RollModes.Advantage,
            Effects: [new AppliedEffect("bendecido", EffectKinds.Modifier, 2)],
            Total: 23,
            Dc: null,
            Success: null,
            Formula: "2d20(7,18) → 18 (ventaja) + 3 + 0 + 2 = 23");

        // La normalización es idempotente: una tirada nueva no se altera.
        Assert.Equal(current, current.WithLegacyDefaults());
    }
}
