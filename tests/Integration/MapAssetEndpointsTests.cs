using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Mapa persistido de la escena (ADR-028): subida con validación de
/// tipo, tamaño y firma binaria, lectura para miembros, reemplazo,
/// borrado solo GM+, evento `MapUpdated` y sin IDOR entre escenas.
/// </summary>
public class MapAssetEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    /// <summary>PNG mínimo válido por firma: cabecera + relleno.</summary>
    private static byte[] TinyPng()
    {
        var bytes = new byte[64];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private static MultipartFormDataContent PngForm(string fileName = "mapa.png")
    {
        var file = new ByteArrayContent(TinyPng());
        file.Headers.ContentType = new("image/png");
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return form;
    }

    // --- Subida y lectura -------------------------------------------------

    [Fact]
    public async Task UploadMap_RoundTripsThroughDownload()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var upload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());
        var json = await upload.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("mapa.png", json.GetProperty("fileName").GetString());
        Assert.Equal("image/png", json.GetProperty("contentType").GetString());
        Assert.Equal(64, json.GetProperty("sizeBytes").GetInt64());

        var download = await owner.GetAsync($"/api/v1/scenes/{sceneId}/map");
        download.EnsureSuccessStatusCode();
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TinyPng(), await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ReplaceMap_KeepsSingleMap()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();
        await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());

        var replaced = new ByteArrayContent(TinyPng());
        replaced.Headers.ContentType = new("image/png");
        var form = new MultipartFormDataContent { { replaced, "file", "nuevo.png" } };
        var upload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", form);
        var json = await upload.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("nuevo.png", json.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Download_WithoutMap_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");

        var download = await owner.GetAsync(
            $"/api/v1/scenes/{scene.GetProperty("id").GetString()}/map");
        await download.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación -------------------------------------------------------

    [Fact]
    public async Task Upload_InvalidType_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var file = new ByteArrayContent(TinyPng());
        file.Headers.ContentType = new("application/zip");
        var form = new MultipartFormDataContent { { file, "file", "mapa.zip" } };
        var upload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", form);
        var json = await upload.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("file", json.GetProperty("field").GetString());
    }

    [Fact]
    public async Task Upload_ForgedSignature_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        // Dice ser PNG pero el contenido no lo es.
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('a', 64)));
        file.Headers.ContentType = new("image/png");
        var form = new MultipartFormDataContent { { file, "file", "falso.png" } };
        var upload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", form);
        await upload.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_TooBig_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var bytes = new byte[10 * 1024 * 1024 + 1];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new("image/png");
        var form = new MultipartFormDataContent { { file, "file", "gigante.png" } };
        var upload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", form);
        await upload.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Permisos ---------------------------------------------------------

    [Fact]
    public async Task PlayerCanViewButNotUploadOrDelete()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();
        await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());

        // Ve el mapa…
        var download = await player.GetAsync($"/api/v1/scenes/{sceneId}/map");
        download.EnsureSuccessStatusCode();

        // …pero no lo sube ni lo borra.
        var upload = await player.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());
        await upload.EnsureStatusAsync(HttpStatusCode.Forbidden);
        var delete = await player.DeleteAsync($"/api/v1/scenes/{sceneId}/map");
        await delete.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonMember_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp mapas");
        var outsider = await factory.CreateAuthenticatedClientAsync();
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();
        await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());

        var download = await outsider.GetAsync($"/api/v1/scenes/{sceneId}/map");
        await download.EnsureStatusAsync(HttpStatusCode.NotFound);

        var upload = await outsider.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());
        await upload.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Borrado y evento -------------------------------------------------

    [Fact]
    public async Task DeleteMap_RemovesItAndEmitsMapUpdated()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = scene.GetProperty("id").GetString();

        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await playerHub.InvokeAsync("JoinCampaign", campaignId);
            var events = Channel.CreateUnbounded<MapUpdatedEvent>();
            playerHub.On<MapUpdatedEvent>(
                "MapUpdated", evt => events.Writer.TryWrite(evt));

            await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", PngForm());
            var uploaded = await ReadEventAsync(events);
            Assert.Equal(sceneId, uploaded.SceneId.ToString());
            Assert.NotNull(uploaded.Map);
            Assert.Equal("mapa.png", uploaded.Map!.FileName);

            var delete = await owner.DeleteAsync($"/api/v1/scenes/{sceneId}/map");
            await delete.EnsureStatusAsync(HttpStatusCode.OK);
            var removed = await ReadEventAsync(events);
            Assert.Null(removed.Map);

            var download = await owner.GetAsync($"/api/v1/scenes/{sceneId}/map");
            await download.EnsureStatusAsync(HttpStatusCode.NotFound);
        }
        finally
        {
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<MapUpdatedEvent> ReadEventAsync(
        Channel<MapUpdatedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);
}
