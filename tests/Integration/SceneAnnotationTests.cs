using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Anotaciones compartidas de escena (ADR-043): estado inicial, publicación,
/// idempotencia por commandId, permisos y concurrencia optimista.
/// </summary>
public class SceneAnnotationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Get_DevuelveEstadoVacioParaMiembro()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp anotaciones vacías");
        var sceneId = await CreateSceneIdAsync(owner, campaignId, "Escena vacía");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // Tanto el Owner como un jugador de la campaña pueden leer el estado.
        foreach (var member in new[] { owner, player })
        {
            var state = await (await member.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
                .EnsureStatusAsync(HttpStatusCode.OK);
            Assert.Equal(sceneId, Guid.Parse(state.GetProperty("sceneId").GetString()!));
            Assert.Equal(0, state.GetProperty("version").GetInt64());
            Assert.Empty(state.GetProperty("items").EnumerateArray());
        }
    }

    [Fact]
    public async Task Put_GM_PublicaYElEstadoEsVisible()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp anotaciones");
        var sceneId = await CreateSceneIdAsync(owner, campaignId, "Bosque");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var published = await PutDrawAsync(owner, sceneId, Guid.NewGuid(), 0);
        Assert.Equal(1, published.GetProperty("version").GetInt64());
        var items = published.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal("draw", item.GetProperty("kind").GetString());
        var points = item.GetProperty("points").EnumerateArray().ToList();
        Assert.Equal(2, points.Count);
        Assert.Equal(10, points[0].GetProperty("x").GetDouble());
        Assert.Equal(20, points[0].GetProperty("y").GetDouble());
        Assert.Equal(30, points[1].GetProperty("x").GetDouble());
        Assert.Equal(40, points[1].GetProperty("y").GetDouble());

        var loaded = await (await player.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, loaded.GetProperty("version").GetInt64());
        var loadedItem = Assert.Single(loaded.GetProperty("items").EnumerateArray());
        Assert.Equal(item.GetProperty("id").GetString(), loadedItem.GetProperty("id").GetString());
        Assert.Equal(2, loadedItem.GetProperty("points").EnumerateArray().Count());
    }

    [Fact]
    public async Task Put_IdempotentePorCommandId()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp idempotencia");
        var sceneId = await CreateSceneIdAsync(owner, campaignId, "Escena idempotente");
        var commandId = Guid.NewGuid();

        var first = await PutDrawAsync(owner, sceneId, commandId, 0);
        Assert.Equal(1, first.GetProperty("version").GetInt64());
        var firstId = Assert.Single(first.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetString();

        // El reintento con el mismo commandId devuelve la misma respuesta y no duplica.
        var replay = await PutDrawAsync(owner, sceneId, commandId, 0);
        Assert.Equal(1, replay.GetProperty("version").GetInt64());
        var replayItem = Assert.Single(replay.GetProperty("items").EnumerateArray());
        Assert.Equal(firstId, replayItem.GetProperty("id").GetString());

        var state = await (await owner.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, state.GetProperty("version").GetInt64());
        Assert.Single(state.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Put_PlayerRecibeForbiddenYNoMiembroNotFound()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp permisos");
        var sceneId = await CreateSceneIdAsync(owner, campaignId, "Escena privada");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var stranger = await factory.CreateAuthenticatedClientAsync();

        await PutDrawAsync(player, sceneId, Guid.NewGuid(), 0, HttpStatusCode.Forbidden);
        await (await stranger.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);

        // El rechazo no modificó el estado.
        var state = await (await owner.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, state.GetProperty("version").GetInt64());
        Assert.Empty(state.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Put_ConflictoDeVersionYClear()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp versiones");
        var sceneId = await CreateSceneIdAsync(owner, campaignId, "Escena con conflictos");

        await PutDrawAsync(owner, sceneId, Guid.NewGuid(), 0);
        await PutDrawAsync(owner, sceneId, Guid.NewGuid(), 99, HttpStatusCode.Conflict);

        var cleared = await ClearAsync(owner, sceneId, Guid.NewGuid(), 1);
        Assert.Equal(0, cleared.GetProperty("version").GetInt64());
        Assert.Empty(cleared.GetProperty("items").EnumerateArray());

        var state = await (await owner.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, state.GetProperty("version").GetInt64());
        Assert.Empty(state.GetProperty("items").EnumerateArray());
    }

    private static async Task<Guid> CreateSceneIdAsync(HttpClient owner, string campaignId, string name)
    {
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, name);
        return Guid.Parse(scene.GetProperty("id").GetString()!);
    }

    private static Task<System.Text.Json.JsonElement> PutDrawAsync(
        HttpClient client, Guid sceneId, Guid commandId, long expectedVersion,
        HttpStatusCode expected = HttpStatusCode.OK) =>
        PutAsync(client, sceneId, expected, new
        {
            commandId,
            expectedVersion,
            annotationId = (Guid?)null,
            kind = "draw",
            points = new[] { new { x = 10.0, y = 20.0 }, new { x = 30.0, y = 40.0 } },
            text = (string?)null,
            clear = false,
        });

    private static Task<System.Text.Json.JsonElement> ClearAsync(
        HttpClient client, Guid sceneId, Guid commandId, long expectedVersion) =>
        PutAsync(client, sceneId, HttpStatusCode.OK, new
        {
            commandId,
            expectedVersion,
            annotationId = (Guid?)null,
            kind = (string?)null,
            points = (object?)null,
            text = (string?)null,
            clear = true,
        });

    private static async Task<System.Text.Json.JsonElement> PutAsync(
        HttpClient client, Guid sceneId, HttpStatusCode expected, object command)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/scenes/{sceneId}/annotations", command);
        return await response.EnsureStatusAsync(expected);
    }
}
