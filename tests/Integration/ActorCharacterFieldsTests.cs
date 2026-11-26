using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Ampliación de personaje (PMB-3.3, ADR-034): `kind`, retrato, biografía,
/// personalidad, nivel, `systemData` y borrador `build` — todo aditivo,
/// con la misma validación en servidor y el mismo testigo de concurrencia.
/// </summary>
public class ActorCharacterFieldsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task CrearYActualizar_PersisteLosCamposDePersonaje()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(client, "Campaña ficha");
        var actorId = await ActorEndpointsTests.CreateActorAsync(client, campaignId);

        var created = await client.GetAsync($"/api/v1/actors/{actorId}");
        var before = await created.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("pc", before.GetProperty("kind").GetString());
        Assert.Equal(1, before.GetProperty("level").GetInt32());
        var version = before.GetProperty("version").GetInt64();

        var update = await client.PatchAsJsonAsync($"/api/v1/actors/{actorId}", new
        {
            version,
            kind = "npc",
            level = 5,
            biography = "Nació en Puerto de Scaldar.",
            personality = "Desconfiado pero leal.",
            systemData = new { race = "race/dwarf", speed = 25 },
            build = new { step = "review", selections = new { raceId = "abc" } },
        });
        var updated = await update.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("npc", updated.GetProperty("kind").GetString());
        Assert.Equal(5, updated.GetProperty("level").GetInt32());
        Assert.Contains("Scaldar", updated.GetProperty("biography").GetString());
        Assert.Equal("race/dwarf",
            updated.GetProperty("systemData").GetProperty("race").GetString());
        Assert.Equal("review",
            updated.GetProperty("build").GetProperty("step").GetString());
    }

    [Fact]
    public async Task CamposDePersonaje_SeValidanEnServidor()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(client, "Campaña validación");
        var actorId = await ActorEndpointsTests.CreateActorAsync(client, campaignId);
        var created = await client.GetAsync($"/api/v1/actors/{actorId}");
        var version = (await created.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("version").GetInt64();

        var badKind = await client.PatchAsJsonAsync($"/api/v1/actors/{actorId}", new
        {
            version,
            kind = "dragon",
        });
        await badKind.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var badLevel = await client.PatchAsJsonAsync($"/api/v1/actors/{actorId}", new
        {
            version,
            level = 99,
        });
        await badLevel.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var badBuild = await client.PatchAsJsonAsync($"/api/v1/actors/{actorId}", new
        {
            version,
            build = "no soy un objeto",
        });
        await badBuild.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }
}
