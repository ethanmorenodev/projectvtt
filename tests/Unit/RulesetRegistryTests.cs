using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Unit;

public class RulesetRegistryTests
{
    [Fact]
    public void Get_ReturnsRegisteredRuleset_CaseInsensitive()
    {
        var registry = new RulesetRegistry([new Dnd5eRuleset()]);

        Assert.Equal("dnd5e", registry.Get("DND5E").Key);
    }

    [Fact]
    public void Get_UnknownKey_Throws400()
    {
        var registry = new RulesetRegistry([new Dnd5eRuleset()]);

        var exception = Assert.Throws<ApiException>(() => registry.Get("pathfinder"));
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void All_ExposesDescriptorsForTheClient()
    {
        var registry = new RulesetRegistry([new Dnd5eRuleset()]);

        var ruleset = Assert.Single(registry.All);
        Assert.Equal(
            new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" },
            ruleset.AttributeKeys);
        Assert.Equal(new[] { "hitPoints" }, ruleset.ResourceKeys);
    }
}
