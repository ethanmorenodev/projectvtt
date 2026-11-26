using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Lore (PMB-7.3): grafo de facciones/localizaciones con relaciones
/// tipadas y cronología de eventos; los secretos del DM no se filtran
/// a jugadores (ADR-031 §6).
/// </summary>
public class LoreTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Grafo_IncluyeNodosYRelacionesDelSistema()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña lore");
        var factionId = await CreateContentAsync(owner, campaignId, "faction", "La Corona Rota",
            new JsonObject { ["allegiance"] = "El rey" });
        var locationId = await CreateContentAsync(owner, campaignId, "location", "Scaldar",
            new JsonObject { ["region"] = "Norte" });

        using var relation = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{factionId}/relations", new
            {
                kind = "locatedIn", toItemId = Guid.Parse(locationId), label = "sede",
            });
        await relation.EnsureStatusAsync(HttpStatusCode.Created);

        using var graph = await owner.GetAsync($"/api/v1/content/lore/graph?campaignId={campaignId}");
        var result = await graph.EnsureStatusAsync(HttpStatusCode.OK);
        var nodes = result.GetProperty("nodes").EnumerateArray()
            .Select(node => node.GetProperty("id").GetString()).ToHashSet();
        Assert.Contains(factionId, nodes);
        Assert.Contains(locationId, nodes);
        var edge = Assert.Single(result.GetProperty("edges").EnumerateArray());
        Assert.Equal("locatedIn", edge.GetProperty("kind").GetString());
        Assert.Equal("sede", edge.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Cronologia_OrdenaLosEventosPorSuCampo()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña cronología");
        await CreateContentAsync(owner, campaignId, "event", "Segundo hecho",
            new JsonObject { ["order"] = 2, ["happenedAt"] = "1492-11-16" });
        await CreateContentAsync(owner, campaignId, "event", "Primer hecho",
            new JsonObject { ["order"] = 1, ["happenedAt"] = "1492-01-01" });

        using var timeline = await owner.GetAsync(
            $"/api/v1/content/lore/timeline?campaignId={campaignId}");
        var events = (await timeline.EnsureStatusAsync(HttpStatusCode.OK))
            .EnumerateArray().ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal("Primer hecho", events[0].GetProperty("name").GetString());
        Assert.Equal("Segundo hecho", events[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task SecretosDelDm_NoAparecenParaJugadores()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña secretos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        await CreateContentAsync(owner, campaignId, "faction", "Sociedad Secreta",
            new JsonObject { ["allegiance"] = "clandestina" }, scope: "dm");
        var publicId = await CreateContentAsync(owner, campaignId, "faction", "Gremio Público",
            new JsonObject { ["allegiance"] = "abierta" });
        using var publish = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{publicId}/publish", new { });
        await publish.EnsureStatusAsync(HttpStatusCode.OK);

        using var ownerGraph = await owner.GetAsync(
            $"/api/v1/content/lore/graph?campaignId={campaignId}");
        var ownerNodes = (await ownerGraph.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Equal(2, ownerNodes.Length);

        using var playerGraph = await player.GetAsync(
            $"/api/v1/content/lore/graph?campaignId={campaignId}");
        var playerNodes = (await playerGraph.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Single(playerNodes);
        Assert.Equal("Gremio Público", playerNodes[0].GetProperty("name").GetString());
    }

    private static async Task<string> CreateContentAsync(
        HttpClient client, string campaignId, string typeKey, string name,
        JsonObject properties, string scope = "campaign")
    {
        using var types = await client.GetAsync("/api/v1/content/types");
        var typeId = (await types.EnsureStatusAsync(HttpStatusCode.OK))
            .EnumerateArray().Single(type => type.GetProperty("key").GetString() == typeKey)
            .GetProperty("id").GetString()!;
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId), name, properties,
            scope, campaignId = Guid.Parse(campaignId),
        });
        var created = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return created.GetProperty("id").GetString()!;
    }
}
