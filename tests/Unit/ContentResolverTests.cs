using System.Text.Json.Nodes;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Unit;

public class ContentResolverTests
{
    [Fact]
    public void ThreeLevels_ResolveModes_WithoutMutatingSources()
    {
        var root = JsonNode.Parse("""{"speed":30,"traits":["base"],"size":"medium"}""")!.AsObject();
        var child = JsonNode.Parse("""{"speed":5,"traits":["child"],"size":"small"}""")!.AsObject();
        var leaf = JsonNode.Parse("""{"speed":99,"traits":["leaf"]}""")!.AsObject();
        var result = ContentResolver.ResolveEffective(new[] {
            (root, new Dictionary<string,string>()),
            (child, new Dictionary<string,string> { ["speed"] = "extend", ["traits"] = "extend", ["size"] = "override" }),
            (leaf, new Dictionary<string,string> { ["speed"] = "inherit", ["traits"] = "extend" })
        });
        Assert.Equal(35, result["speed"]!.GetValue<double>());
        Assert.Equal("small", result["size"]!.GetValue<string>());
        Assert.Equal(3, result["traits"]!.AsArray().Count);
        result["traits"]!.AsArray().Add("later");
        Assert.Single(root["traits"]!.AsArray());
        Assert.Single(child["traits"]!.AsArray());
        Assert.Single(leaf["traits"]!.AsArray());
    }

    [Fact]
    public async Task CycleThroughDescendant_IsRejected()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        await Assert.ThrowsAsync<ApiException>(() => ContentResolver.EnsureNoCycleAsync(
            root, child, (_, _) => Task.FromResult<Guid?>(root), CancellationToken.None));
    }

    [Fact]
    public async Task ExistingAncestorCycle_IsRejectedImmediately()
    {
        var ancestor = Guid.NewGuid();
        var reads = 0;
        await Assert.ThrowsAsync<ApiException>(() => ContentResolver.EnsureNoCycleAsync(
            Guid.NewGuid(), ancestor, (_, _) => { reads++; return Task.FromResult<Guid?>(ancestor); }, CancellationToken.None));
        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public async Task DepthLimit_CountsTheChild(int ancestorCount, bool accepted)
    {
        var ancestors = Enumerable.Range(0, ancestorCount).Select(_ => Guid.NewGuid()).ToArray();
        var parents = ancestors.Select((id, index) => (id, parent: index + 1 < ancestors.Length
            ? (Guid?)ancestors[index + 1] : null)).ToDictionary(x => x.id, x => x.parent);
        var validation = ContentResolver.EnsureNoCycleAsync(Guid.NewGuid(), ancestors[0],
            (id, _) => Task.FromResult(parents[id]), CancellationToken.None);
        if (accepted)
        {
            await validation;
        }
        else
        {
            await Assert.ThrowsAsync<ApiException>(() => validation);
        }
    }}
