using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Forma del grid de la escena (ADR-023): por defecto cuadrada, cambio
/// a hexagonal por GM+ con validación de valor, y visible en el detalle
/// (la niebla y el snap del cliente se adaptan a la forma).
/// </summary>
public class GridShapeEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task NewScene_DefaultsToSquareGrid()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp grid");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");

        Assert.Equal("square", scene.GetProperty("gridShape").GetString());
    }

    [Fact]
    public async Task PatchGridShape_ToHex_Returns200()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp grid");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 48, gridShape = "hex" });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("hex", json.GetProperty("gridShape").GetString());
        Assert.Equal(48, json.GetProperty("gridCellSize").GetInt32());

        var detail = await owner.GetAsync($"/api/v1/scenes/{sceneId}");
        var detailJson = await detail.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("hex", detailJson.GetProperty("gridShape").GetString());
    }

    [Fact]
    public async Task PatchGridShape_WithoutIt_KeepsCurrentShape()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp grid");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();
        await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 48, gridShape = "hex" });

        // Solo cambia el tamaño: la forma se conserva.
        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 64 });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("hex", json.GetProperty("gridShape").GetString());
        Assert.Equal(64, json.GetProperty("gridCellSize").GetInt32());
    }

    [Fact]
    public async Task PatchGridShape_InvalidValue_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp grid");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 32, gridShape = "triangular" });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("gridShape", json.GetProperty("field").GetString());
    }

    [Fact]
    public async Task PatchGridShape_Player_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp grid");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var patch = await player.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}", new { gridCellSize = 32, gridShape = "hex" });
        await patch.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }
}
