using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Lógica pura del primer Ruleset (D&amp;D 5e, ADR-005): sin base de datos
/// ni HTTP. El desglose de las tiradas debe ser verifiable por aritmética.
/// </summary>
public class Dnd5eRulesetTests
{
    private readonly Dnd5eRuleset _ruleset = new();

    private Dictionary<string, int> FullSheet(int value = 10) =>
        _ruleset.AttributeKeys.ToDictionary(key => key, _ => value);

    // --- Modificador -----------------------------------------------------

    [Theory]
    [InlineData(1, -5)]
    [InlineData(3, -4)]
    [InlineData(8, -1)]
    [InlineData(9, -1)] // piso: -0.5 → -1 (la división entera de C# daría 0)
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(15, 2)]
    [InlineData(16, 3)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void Modifier_FollowsDnd5eFormula(int value, int expected) =>
        Assert.Equal(expected, Dnd5eRuleset.Modifier(value));

    // --- Validación de la ficha ------------------------------------------

    [Fact]
    public void ValidateActorData_CompleteSheet_DoesNotThrow()
    {
        var attributes = FullSheet(15);
        var resources = new Dictionary<string, ResourceValue>
        {
            ["hitPoints"] = new(Current: 12, Max: 12),
        };

        _ruleset.ValidateActorData(attributes, resources);
    }

    [Fact]
    public void ValidateActorData_MissingAttribute_Throws400()
    {
        var attributes = FullSheet();
        attributes.Remove("wisdom");

        var exception = Assert.Throws<ApiException>(
            () => _ruleset.ValidateActorData(attributes, new Dictionary<string, ResourceValue>()));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("wisdom", exception.Detail);
    }

    [Fact]
    public void ValidateActorData_ValueOutOfRange_Throws400()
    {
        var attributes = FullSheet();
        attributes["strength"] = 31;

        var exception = Assert.Throws<ApiException>(
            () => _ruleset.ValidateActorData(attributes, new Dictionary<string, ResourceValue>()));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("strength", exception.Detail);
    }

    [Fact]
    public void ValidateActorData_UnknownAttribute_Throws400()
    {
        var attributes = FullSheet();
        attributes["luck"] = 10;

        var exception = Assert.Throws<ApiException>(
            () => _ruleset.ValidateActorData(attributes, new Dictionary<string, ResourceValue>()));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("luck", exception.Detail);
    }

    [Fact]
    public void ValidateActorData_CurrentAboveMax_Throws400()
    {
        var resources = new Dictionary<string, ResourceValue>
        {
            ["hitPoints"] = new(Current: 15, Max: 10),
        };

        var exception = Assert.Throws<ApiException>(
            () => _ruleset.ValidateActorData(FullSheet(), resources));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("hitPoints", exception.Detail);
    }

    [Fact]
    public void ValidateActorData_UnknownResource_Throws400()
    {
        var resources = new Dictionary<string, ResourceValue>
        {
            ["mana"] = new(Current: 5, Max: 5),
        };

        var exception = Assert.Throws<ApiException>(
            () => _ruleset.ValidateActorData(FullSheet(), resources));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("mana", exception.Detail);
    }

    [Fact]
    public void ValidateActorData_EmptyResources_IsAllowed()
    {
        _ruleset.ValidateActorData(FullSheet(), new Dictionary<string, ResourceValue>());
    }

    // --- Resolución de tiradas -------------------------------------------

    /// <summary>
    /// Resuelve con el plan derivado de los efectos indicados y un único
    /// dado (la modalidad normal). Los tests de ventaja/desventaja usan
    /// <see cref="_ruleset"/>.PlanRoll directamente.
    /// </summary>
    private RollBreakdown Resolve(
        string attribute,
        Dictionary<string, int> attributes,
        int die,
        int extra,
        int? dc,
        params string[] effects)
    {
        var plan = _ruleset.PlanRoll(effects);
        return _ruleset.ResolveAbilityCheck(attribute, attributes, [die], extra, dc, plan);
    }

    [Fact]
    public void ResolveAbilityCheck_SumsDieModifierAndExtra()
    {
        var attributes = FullSheet();
        attributes["strength"] = 16; // modificador +3

        var roll = Resolve("strength", attributes, die: 17, extra: 2, dc: 15);

        Assert.Equal("ability-check", roll.Kind);
        Assert.Equal(16, roll.AttributeValue);
        Assert.Equal(17, roll.Die);
        Assert.Equal(3, roll.Modifier);
        Assert.Equal(2, roll.Extra);
        Assert.Equal(0, roll.EffectModifier);
        Assert.Equal(RollModes.Normal, roll.RollMode);
        Assert.Equal(new[] { 17 }, roll.Dice);
        Assert.Empty(roll.Effects);
        Assert.Equal(22, roll.Total);
        Assert.Equal(15, roll.Dc);
        Assert.True(roll.Success);
        Assert.Contains("1d20(17)", roll.Formula);
        Assert.Contains("CD 15", roll.Formula);
    }

    [Fact]
    public void ResolveAbilityCheck_WithoutDc_HasNullSuccess()
    {
        var attributes = FullSheet();
        attributes["charisma"] = 9; // modificador -1

        var roll = Resolve("charisma", attributes, die: 1, extra: 0, dc: null);

        Assert.Equal(0, roll.Total);
        Assert.Null(roll.Dc);
        Assert.Null(roll.Success);
        Assert.DoesNotContain("CD", roll.Formula);
    }

    [Fact]
    public void ResolveAbilityCheck_FailedDc_ReturnsSuccessFalse()
    {
        var attributes = FullSheet();
        attributes["dexterity"] = 8; // modificador -1

        var roll = Resolve("dexterity", attributes, die: 5, extra: 0, dc: 15);

        Assert.Equal(4, roll.Total);
        Assert.False(roll.Success);
        Assert.Contains("no superada", roll.Formula);
    }

    [Fact]
    public void ResolveAbilityCheck_UnknownAttribute_Throws400()
    {
        var exception = Assert.Throws<ApiException>(() => Resolve(
            "stealth", FullSheet(), die: 10, extra: 0, dc: null));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void ResolveAbilityCheck_ExtraOutOfRange_Throws400()
    {
        var exception = Assert.Throws<ApiException>(() => Resolve(
            "strength", FullSheet(), die: 10, extra: 21, dc: null));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("extra", exception.Detail);
    }

    [Fact]
    public void ResolveAbilityCheck_DcOutOfRange_Throws400()
    {
        var exception = Assert.Throws<ApiException>(() => Resolve(
            "strength", FullSheet(), die: 10, extra: 0, dc: 101));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("dc", exception.Detail);
    }

    [Fact]
    public void ResolveAbilityCheck_InvalidDie_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Resolve(
            "strength", FullSheet(), die: 21, extra: 0, dc: null));
    }

    // --- Plan de efectos (ADR-015) ---------------------------------------

    [Fact]
    public void PlanRoll_UnknownEffects_AreIgnored()
    {
        var plan = _ruleset.PlanRoll(["Concentrado", "Marcado", "   "]);

        Assert.Equal(RollModes.Normal, plan.RollMode);
        Assert.Equal(1, plan.DiceCount);
        Assert.Equal(0, plan.EffectModifier);
        Assert.Empty(plan.Effects);
    }

    [Fact]
    public void PlanRoll_MatchesIgnoringCaseAccentsAndSpaces()
    {
        var plan = _ruleset.PlanRoll(["  ENVENENADO ", "envénenado"]);

        // El nombre tecleado se conserva para el desglose; la comparación
        // con el catálogo no distingue mayúsculas ni tildes.
        Assert.Equal(RollModes.Disadvantage, plan.RollMode);
        Assert.Equal(2, plan.DiceCount);
        Assert.Equal(
            new[] { "ENVENENADO", "envénenado" },
            plan.Effects.Select(e => e.Name));
        Assert.All(plan.Effects, e => Assert.Equal(EffectKinds.Disadvantage, e.Kind));
    }

    [Fact]
    public void PlanRoll_AdvantageAndDisadvantage_CancelEachOther()
    {
        // Regla 5e: ventaja y desventaja se anulan; ambos efectos siguen
        // siendo reconocidos y aparecen en el desglose.
        var plan = _ruleset.PlanRoll(["invisible", "aturdido"]);

        Assert.Equal(RollModes.Normal, plan.RollMode);
        Assert.Equal(1, plan.DiceCount);
        Assert.Equal(0, plan.EffectModifier);
        Assert.Equal(2, plan.Effects.Count);
    }

    [Fact]
    public void PlanRoll_ModifierEffects_SumTheirValues()
    {
        Assert.Equal(2, _ruleset.PlanRoll(["bendecido"]).EffectModifier);
        Assert.Equal(-2, _ruleset.PlanRoll(["maldito"]).EffectModifier);
        Assert.Equal(0, _ruleset.PlanRoll(["bendecido", "maldito"]).EffectModifier);

        var mixed = _ruleset.PlanRoll(["bendecido", "desventaja"]);
        Assert.Equal(RollModes.Disadvantage, mixed.RollMode);
        Assert.Equal(2, mixed.EffectModifier);
    }

    [Fact]
    public void ResolveAbilityCheck_Advantage_KeepsHigherDie()
    {
        var attributes = FullSheet();
        attributes["strength"] = 16; // modificador +3
        var plan = _ruleset.PlanRoll(["ventaja"]);

        var roll = _ruleset.ResolveAbilityCheck(
            "strength", attributes, [7, 18], extra: 0, dc: null, plan: plan);

        Assert.Equal(RollModes.Advantage, roll.RollMode);
        Assert.Equal(18, roll.Die);
        Assert.Equal(new[] { 7, 18 }, roll.Dice);
        Assert.Equal(18 + 3, roll.Total);
        Assert.Contains("2d20(7,18) → 18 (ventaja)", roll.Formula);
        Assert.Contains("Efectos: ventaja (ventaja)", roll.Formula);
    }

    [Fact]
    public void ResolveAbilityCheck_Disadvantage_KeepsLowerDie_AndAppliesModifier()
    {
        var attributes = FullSheet();
        attributes["strength"] = 16; // modificador +3
        var plan = _ruleset.PlanRoll(["envenenado", "bendecido"]);

        var roll = _ruleset.ResolveAbilityCheck(
            "strength", attributes, [7, 18], extra: 2, dc: null, plan: plan);

        Assert.Equal(RollModes.Disadvantage, roll.RollMode);
        Assert.Equal(7, roll.Die);
        Assert.Equal(2, roll.EffectModifier); // bendecido +2
        Assert.Equal(7 + 3 + 2 + 2, roll.Total); // die + mod + extra + efectos
        Assert.Contains("2d20(7,18) → 7 (desventaja)", roll.Formula);
        Assert.Contains("Efectos: envenenado (desventaja), bendecido (+2)", roll.Formula);
    }

    [Fact]
    public void ResolveAbilityCheck_DiceMismatchWithPlan_Throws()
    {
        var attributes = FullSheet();

        // Plan normal (1 dado) pero llegan dos.
        var normal = _ruleset.PlanRoll([]);
        Assert.Throws<InvalidOperationException>(() => _ruleset.ResolveAbilityCheck(
            "strength", attributes, [10, 12], extra: 0, dc: null, plan: normal));

        // Plan con ventaja (2 dados) pero llega uno.
        var advantage = _ruleset.PlanRoll(["ventaja"]);
        Assert.Throws<InvalidOperationException>(() => _ruleset.ResolveAbilityCheck(
            "strength", attributes, [10], extra: 0, dc: null, plan: advantage));
    }
}
