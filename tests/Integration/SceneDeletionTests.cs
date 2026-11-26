using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Borrado de escena: el servidor elimina la escena y sus recursos asociados
/// (tokens, mapa, portada, anotaciones) y responde `{ deleted: true }`.
/// Solo GM/Owner; Player 403, ajeno 404; segunda llamada 404 (no 500).
/// </summary>
public class SceneDeletionTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static MultipartFormDataContent PngForm(byte[] png, string fileName)
    {
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    /// <summary>PNG CC0 licenciado en el repo (OpenGameArt, Drummyfish).</summary>
    private static byte[] LicensedPng()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md")))
        {
            root = root.Parent;
        }

        return File.ReadAllBytes(Path.Combine(root!.FullName, "apps", "web", "public",
            "art", "fantasy", "icon_dragon.png"));
    }

    [Fact]
    public async Task BorrarEscena_GM_EliminaYListaQuedaVacia()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp borrado GM");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Cueva temporal");
        var sceneId = scene.GetProperty("id").GetGuid();

        using var deletion = await owner.DeleteAsync($"/api/v1/scenes/{sceneId}");
        var deleted = await deletion.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(deleted.GetProperty("deleted").GetBoolean());

        using var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/scenes");
        var scenes = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Empty(scenes.EnumerateArray());

        using var detail = await owner.GetAsync($"/api/v1/scenes/{sceneId}");
        await detail.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BorrarEscena_PlayerRecibeForbiddenYNoMiembroNotFound()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp permisos borrado");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Cueva privada");
        var sceneId = scene.GetProperty("id").GetGuid();
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        using var stranger = await factory.CreateAuthenticatedClientAsync();

        using var forbidden = await player.DeleteAsync($"/api/v1/scenes/{sceneId}");
        await forbidden.EnsureStatusAsync(HttpStatusCode.Forbidden);

        using var missing = await stranger.DeleteAsync($"/api/v1/scenes/{sceneId}");
        await missing.EnsureStatusAsync(HttpStatusCode.NotFound);

        // Ninguno de los dos intentos ha eliminado la escena.
        using var detail = await owner.GetAsync($"/api/v1/scenes/{sceneId}");
        var stillThere = await detail.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(sceneId, stillThere.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BorrarEscena_EliminaTokensMapaYPortada()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp assets borrado");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque con todo");
        var sceneId = scene.GetProperty("id").GetGuid();

        await SceneEndpointsTests.CreateTokenAsync(owner, sceneId.ToString(), "Héroe", 3, 4);
        var png = LicensedPng();
        using var mapUpload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm(png, "mapa.png"));
        await mapUpload.EnsureStatusAsync(HttpStatusCode.OK);
        using var coverUpload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/cover", PngForm(png, "portada.png"));
        await coverUpload.EnsureStatusAsync(HttpStatusCode.OK);

        using var tokensBefore = await owner.GetAsync($"/api/v1/scenes/{sceneId}/tokens");
        Assert.Single((await tokensBefore.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());
        using var mapBefore = await owner.GetAsync($"/api/v1/scenes/{sceneId}/map");
        Assert.Equal(HttpStatusCode.OK, mapBefore.StatusCode);
        using var coverBefore = await owner.GetAsync($"/api/v1/scenes/{sceneId}/cover");
        Assert.Equal(HttpStatusCode.OK, coverBefore.StatusCode);

        using var deletion = await owner.DeleteAsync($"/api/v1/scenes/{sceneId}");
        Assert.True((await deletion.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("deleted").GetBoolean());

        using var tokensAfter = await owner.GetAsync($"/api/v1/scenes/{sceneId}/tokens");
        await tokensAfter.EnsureStatusAsync(HttpStatusCode.NotFound);
        using var mapAfter = await owner.GetAsync($"/api/v1/scenes/{sceneId}/map");
        Assert.Equal(HttpStatusCode.NotFound, mapAfter.StatusCode);
        using var coverAfter = await owner.GetAsync($"/api/v1/scenes/{sceneId}/cover");
        Assert.Equal(HttpStatusCode.NotFound, coverAfter.StatusCode);
    }

    [Fact]
    public async Task BorrarEscena_SegundaLlamadaEs404()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp segunda llamada");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Escena fugaz");
        var sceneId = scene.GetProperty("id").GetGuid();
        var nuncaCreada = Guid.NewGuid();

        using var unknown = await owner.DeleteAsync($"/api/v1/scenes/{nuncaCreada}");
        await unknown.EnsureStatusAsync(HttpStatusCode.NotFound);

        using var first = await owner.DeleteAsync($"/api/v1/scenes/{sceneId}");
        Assert.True((await first.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("deleted").GetBoolean());

        using var second = await owner.DeleteAsync($"/api/v1/scenes/{sceneId}");
        await second.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BorrarEscena_GM_EmiteSceneDeletedAlGrupo()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp borrado evento");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Cueva efímera");
        var sceneId = scene.GetProperty("id").GetGuid();
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var received = new TaskCompletionSource<SceneDeletedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            playerHub.On<SceneDeletedEvent>("SceneDeleted", evt => received.TrySetResult(evt));

            using var deletion = await owner.DeleteAsync($"/api/v1/scenes/{sceneId}");
            await deletion.EnsureStatusAsync(HttpStatusCode.OK);

            var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(sceneId, evt.SceneId);
            Assert.Equal(Guid.Parse(campaignId), evt.CampaignId);
            Assert.Equal(1, evt.SchemaVersion);
            Assert.NotEqual(Guid.Empty, evt.EventId);
        }
        finally
        {
            await playerHub.DisposeAsync();
        }
    }
}