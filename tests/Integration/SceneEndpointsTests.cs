using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

public class SceneEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    // --- Escenas --------------------------------------------------------

    [Fact]
    public async Task OwnerCreatesScene_AndAppearsInList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");

        var create = await CreateSceneAsync(owner, campaignId, "Cueva inicial");
        var sceneId = create.GetProperty("id").GetString();

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/scenes");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(json.EnumerateArray(),
            s => s.GetProperty("id").GetString() == sceneId);
    }

    [Fact]
    public async Task PlayerCannotCreateScene_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var response = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/scenes", new { name = "Escena no autorizada" });

        await response.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonMemberCannotAccessScene_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var scene = await CreateSceneAsync(owner, campaignId, "Privada");
        var sceneId = scene.GetProperty("id").GetString()!;
        var stranger = await factory.CreateAuthenticatedClientAsync();

        var detail = await stranger.GetAsync($"/api/v1/scenes/{sceneId}");
        await detail.EnsureStatusAsync(HttpStatusCode.NotFound);

        var tokens = await stranger.GetAsync($"/api/v1/scenes/{sceneId}/tokens");
        await tokens.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OwnerUpdatesGridCellSize_AndPlayerSeesItOnReload()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var scene = await CreateSceneAsync(owner, campaignId, "Plano");
        var sceneId = scene.GetProperty("id").GetString()!;
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var patch = await player.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 48 });
        await patch.EnsureStatusAsync(HttpStatusCode.Forbidden);

        var update = await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 48 });
        var json = await update.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(48, json.GetProperty("gridCellSize").GetInt32());

        var detail = await player.GetAsync($"/api/v1/scenes/{sceneId}");
        var detailJson = await detail.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(48, detailJson.GetProperty("gridCellSize").GetInt32());
    }

    [Fact]
    public async Task UpdateScene_InvalidGridSize_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var scene = await CreateSceneAsync(owner, campaignId, "Plano");
        var sceneId = scene.GetProperty("id").GetString()!;

        var response = await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 4 });

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Tokens ---------------------------------------------------------

    [Fact]
    public async Task OwnerCreatesToken_ThenMemberListsIt()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var scene = await CreateSceneAsync(owner, campaignId, "Plano");
        var sceneId = scene.GetProperty("id").GetString()!;
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var token = await CreateTokenAsync(owner, sceneId, "Héroe", 3, 4);
        Assert.Equal(0, token.GetProperty("version").GetInt64());

        var list = await player.GetAsync($"/api/v1/scenes/{sceneId}/tokens");
        var json = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Single(json.EnumerateArray());
    }

    [Fact]
    public async Task PlayerCannotCreateToken_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var scene = await CreateSceneAsync(owner, campaignId, "Plano");
        var sceneId = scene.GetProperty("id").GetString()!;
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var response = await player.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/tokens", new { name = "Tramposo", x = 0, y = 0 });

        await response.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateToken_OutOfRangeCoordinate_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var scene = await CreateSceneAsync(owner, campaignId, "Plano");
        var sceneId = scene.GetProperty("id").GetString()!;

        // Límite de 1000000 definido en el servidor (NaN no es JSON válido
        // por definición, así que el rango es la vía de validación real).
        var response = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/tokens", new { name = "Inválido", x = 1e9, y = 0 });

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Utilidades -----------------------------------------------------

    internal static async Task<JsonElement> CreateSceneAsync(
        HttpClient client, string campaignId, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/scenes", new { name });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    internal static async Task<JsonElement> CreateTokenAsync(
        HttpClient client, string sceneId, string name, double x, double y)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/tokens", new { name, x, y });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }
}
