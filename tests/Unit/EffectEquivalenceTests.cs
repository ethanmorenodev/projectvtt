using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Rules;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Prueba de equivalencia (PMB-5.6, ADR-040): el catálogo en datos debe
/// producir exactamente el mismo plan y desglose que el catálogo en código
/// de ADR-015. Mientras no se retire el catálogo en código, esta prueba es
/// la red de seguridad que lo mantiene como autoridad de dnd5e.
/// </summary>
public class EffectEquivalenceTests
{
    private static readonly string[] CatalogNames =
    [
        "ventaja", "invisible", "desventaja", "envenenado", "aturdido",
        "cegado", "asustado", "bendecido", "maldito",
    ];

    private static readonly string[][] Scenarios =
    [
        [],
        ["ventaja"],
        ["desventaja"],
        ["ventaja", "desventaja"],
        ["ventaja", "ventaja", "desventaja"],
        ["bendecido", "maldito"],
        ["Bendecido", "  maldito "],
        ["ventaja", "bendecido", "desconocido"],
        ["desconocido"],
        CatalogNames,
    ];

    [Fact]
    public void PlanRoll_PlanYDesglose_IdenticosEntreCatalogos()
    {
        var ruleset = new Dnd5eRuleset();
        foreach (var scenario in Scenarios)
        {
            var code = ruleset.PlanRoll(scenario);
            var data = ruleset.PlanRollFromData(scenario);

            Assert.Equal(code.DiceCount, data.DiceCount);
            Assert.Equal(code.RollMode, data.RollMode);
            Assert.Equal(code.EffectModifier, data.EffectModifier);
            Assert.Equal(code.Effects.Count, data.Effects.Count);
            for (var index = 0; index < code.Effects.Count; index++)
            {
                Assert.Equal(code.Effects[index].Name, data.Effects[index].Name);
                Assert.Equal(code.Effects[index].Kind, data.Effects[index].Kind);
                Assert.Equal(code.Effects[index].Value, data.Effects[index].Value);
            }
        }
    }

    [Fact]
    public void Desglose_DeTirada_IdenticoEntreCatalogos()
    {
        var ruleset = new Dnd5eRuleset();
        var attributes = new Dictionary<string, int> { ["strength"] = 15 };
        foreach (var scenario in Scenarios)
        {
            var code = ruleset.PlanRoll(scenario);
            var data = ruleset.PlanRollFromData(scenario);
            var dice = code.DiceCount == 1 ? new List<int> { 12 } : new List<int> { 12, 7 };

            var codeBreakdown = ruleset.ResolveAbilityCheck(
                "strength", attributes, dice, extra: 2, dc: 14, code);
            var dataBreakdown = ruleset.ResolveAbilityCheck(
                "strength", attributes, dice, extra: 2, dc: 14, data);

            Assert.Equal(codeBreakdown.Total, dataBreakdown.Total);
            Assert.Equal(codeBreakdown.EffectModifier, dataBreakdown.EffectModifier);
            Assert.Equal(codeBreakdown.RollMode, dataBreakdown.RollMode);
            Assert.Equal(codeBreakdown.Dice.Count, dataBreakdown.Dice.Count);
        }
    }

    [Fact]
    public void CadaNombreDelCatalogo_ReconocidoPorAmbasVersiones()
    {
        var ruleset = new Dnd5eRuleset();
        foreach (var name in CatalogNames)
        {
            var code = ruleset.PlanRoll([name]);
            var data = ruleset.PlanRollFromData([name]);
            Assert.Equal(code.Effects.Count, data.Effects.Count);
            Assert.Single(code.Effects);
            Assert.Single(data.Effects);
        }
    }
}
