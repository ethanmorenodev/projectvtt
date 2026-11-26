using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ProjectVTT.Tests.Integration;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Portada opcional de escena: round-trip de bytes, `hasCover` en el DTO,
/// validación de firma y permisos (GM/Owner). El blob vive en IAssetStore;
/// la entidad solo guarda el tipo MIME.
/// </summary>
public class SceneCoverTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 5, 6, 7, 8];

    private static MultipartFormDataContent PngForm()
    {
        var file = new ByteArrayContent(PngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "escena.png" } };
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Cover_RoundTrip_HasCoverAndPermissions()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Con portadas");
        var sceneId = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneGuid = Guid.Parse(sceneId.GetProperty("id").GetString()!);
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.GetAsync($"/api/v1/scenes/{sceneGuid}/cover")).StatusCode);

        await (await owner.PutAsync($"/api/v1/scenes/{sceneGuid}/cover", PngForm()))
            .EnsureStatusAsync(HttpStatusCode.OK);

        var cover = await owner.GetAsync($"/api/v1/scenes/{sceneGuid}/cover");
        cover.EnsureSuccessStatusCode();
        Assert.Equal("image/png", cover.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes, await cover.Content.ReadAsByteArrayAsync());

        var detail = await ReadAsync(await owner.GetAsync($"/api/v1/scenes/{sceneGuid}"));
        Assert.True(detail.GetProperty("hasCover").GetBoolean());
        var list = await ReadAsync(await owner.GetAsync($"/api/v1/campaigns/{campaignId}/scenes"));
        Assert.True(list.EnumerateArray()
            .First(x => Guid.Parse(x.GetProperty("id").GetString()!) == sceneGuid)
            .GetProperty("hasCover").GetBoolean());

        // Firma falsificada y permisos insuficientes.
        var fake = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 9, 9, 9]);
        fake.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsync($"/api/v1/scenes/{sceneGuid}/cover",
                new MultipartFormDataContent { { fake, "file", "falso.png" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await player.PutAsync($"/api/v1/scenes/{sceneGuid}/cover", PngForm())).StatusCode);

        // Un jugador sí puede leer la portada de una escena de su campaña.
        Assert.Equal(HttpStatusCode.OK,
            (await player.GetAsync($"/api/v1/scenes/{sceneGuid}/cover")).StatusCode);

        await (await owner.DeleteAsync($"/api/v1/scenes/{sceneGuid}/cover"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.GetAsync($"/api/v1/scenes/{sceneGuid}/cover")).StatusCode);
    }
}
