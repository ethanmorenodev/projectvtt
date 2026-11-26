using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Historial de cambios por HTTP (ADR-021): las acciones sobre el estado
/// compartido (escenas, tokens, fichas, inventario, efectos, tiradas y
/// combate) quedan registradas y se leen de más reciente a más antigua;
/// las notas del GM y los mensajes privados NO se registran (privacidad);
/// leer es de miembro; el historial es de cada campaña.
/// </summary>
public class HistoryEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    // --- Registro ---------------------------------------------------------

    [Fact]
    public async Task MutationsAppearInHistoryNewestFirst()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");

        await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/actors/{actorId}", new { version = 0, name = "Renombrado" });
        await patch.EnsureStatusAsync(HttpStatusCode.OK);

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/history");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(json.GetArrayLength() >= 3);
        // De más reciente a más antiguo: update, create actor, create scene.
        Assert.Contains("Renombrado", json[0].GetProperty("summary").GetString());
        Assert.Equal("actor-updated", json[0].GetProperty("kind").GetString());
        Assert.Equal("actor-created", json[1].GetProperty("kind").GetString());
        Assert.Equal("scene-created", json[2].GetProperty("kind").GetString());
        Assert.False(string.IsNullOrEmpty(json[0].GetProperty("userName").GetString()));
    }

    [Fact]
    public async Task RollAndCombatAreLogged()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var roll = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            new { commandId = Guid.NewGuid(), actorId, attribute = "strength" });
        await roll.EnsureStatusAsync(HttpStatusCode.OK);
        var start = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        await start.EnsureStatusAsync(HttpStatusCode.Created);
        var end = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/end", null);
        await end.EnsureStatusAsync(HttpStatusCode.OK);

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/history");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        var kinds = new List<string>();
        for (var i = 0; i < json.GetArrayLength(); i++)
        {
            kinds.Add(json[i].GetProperty("kind").GetString()!);
        }

        Assert.Contains("roll-resolved", kinds);
        Assert.Contains("combat-started", kinds);
        Assert.Contains("combat-ended", kinds);
    }

    [Fact]
    public async Task NotesAndDirectMessagesAreNotLogged()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // Nota privada y mensaje privado: ninguno debe aparecer.
        var note = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes",
            new { title = "Secreta", body = "El tesoro está bajo el altar" });
        await note.EnsureStatusAsync(HttpStatusCode.Created);
        var playerId = await player.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/me");
        var dm = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = playerId.GetProperty("id").GetString(), body = "Susurro secreto" });
        await dm.EnsureStatusAsync(HttpStatusCode.Created);

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/history");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, json.GetArrayLength());
    }

    [Fact]
    public async Task HistoryIsLimitedTo100()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");

        for (var i = 0; i < 105; i++)
        {
            await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, $"Escena {i}");
        }

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/history");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(100, json.GetArrayLength());
        // Las más recientes sobreviven al recorte.
        Assert.Contains("Escena 104", json[0].GetProperty("summary").GetString());
    }

    // --- Permisos y aislamiento -------------------------------------------

    [Fact]
    public async Task PlayerCanReadHistory()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");

        var list = await player.GetAsync($"/api/v1/campaigns/{campaignId}/history");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(json.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task NonMember_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var list = await outsider.GetAsync($"/api/v1/campaigns/{campaignId}/history");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HistoryIsPerCampaign()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp historia");
        var otherId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp otra");
        await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");

        var list = await owner.GetAsync($"/api/v1/campaigns/{otherId}/history");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, json.GetArrayLength());
    }
}
