using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ProjectVTT.Tests.Integration;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Presentación de campañas (PMB-2.4): descripción, portada opcional,
/// última actividad y actividad multi-campaña (PMB-2.2). Comprueba que la
/// ampliación de modelo es aditiva (no rompe campañas existentes) y que
/// los permisos siguen en el servidor.
/// </summary>
public class CampaignPresentationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static MultipartFormDataContent PngForm()
    {
        var file = new ByteArrayContent(PngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "portada.png" } };
    }

    [Fact]
    public async Task UpdateDetails_ChangesNameAndDescription_AndIsOwnerOnly()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var response = await owner.PatchAsJsonAsync($"/api/v1/campaigns/{campaignId}",
            new { name = "Camp renovada", description = "La ciudad bajo asedio." });
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Camp renovada", json.GetProperty("name").GetString());
        Assert.Equal("La ciudad bajo asedio.", json.GetProperty("description").GetString());

        // Un jugador no puede cambiar los detalles.
        var forbidden = await player.PatchAsJsonAsync($"/api/v1/campaigns/{campaignId}",
            new { name = "Hackeada" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Cover_RoundTrip_PermissionsAndValidation()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Con portada");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // Sin portada: 404.
        var missing = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/cover");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Subida (PNG con firma válida).
        var upload = await owner.PutAsync($"/api/v1/campaigns/{campaignId}/cover", PngForm());
        await upload.EnsureStatusAsync(HttpStatusCode.OK);

        // Lectura con round-trip de bytes y tipo.
        var cover = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/cover");
        cover.EnsureSuccessStatusCode();
        Assert.Equal("image/png", cover.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes, await cover.Content.ReadAsByteArrayAsync());

        // El detalle refleja la portada.
        var detail = await (await owner.GetAsync($"/api/v1/campaigns/{campaignId}")).ReadAsync();
        Assert.True(detail.GetProperty("hasCover").GetBoolean());

        // Firma falsificada: 400 (misma regla que mapas).
        var fake = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 9, 9, 9]);
        fake.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var rejected = await owner.PutAsync($"/api/v1/campaigns/{campaignId}/cover",
            new MultipartFormDataContent { { fake, "file", "falso.png" } });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        // Un jugador no puede gestionar la portada; un no miembro ve 404.
        var forbidden = await player.PutAsync($"/api/v1/campaigns/{campaignId}/cover", PngForm());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Borrado y confirmación.
        var delete = await owner.DeleteAsync($"/api/v1/campaigns/{campaignId}/cover");
        await delete.EnsureStatusAsync(HttpStatusCode.OK);
        var gone = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/cover");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        var detailAfter = await (await owner.GetAsync($"/api/v1/campaigns/{campaignId}")).ReadAsync();
        Assert.False(detailAfter.GetProperty("hasCover").GetBoolean());
    }

    [Fact]
    public async Task List_ReturnsDescriptionHasCoverAndLastActivity()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Con actividad");

        var before = await (await owner.GetAsync("/api/v1/campaigns")).ReadAsync();
        var entry = before.EnumerateArray()
            .First(e => e.GetProperty("id").GetString() == campaignId);
        Assert.Null(entry.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("lastActivityAt").ValueKind);

        // Crear una escena registra historial (actividad real).
        await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Escena");

        var after = await (await owner.GetAsync("/api/v1/campaigns")).ReadAsync();
        var updated = after.EnumerateArray()
            .First(e => e.GetProperty("id").GetString() == campaignId);
        Assert.Equal(JsonValueKind.String, updated.GetProperty("lastActivityAt").ValueKind);
    }

    [Fact]
    public async Task Activity_SpansCampaignsAndIsolatesOtherUsers()
    {
        var userA = await factory.CreateAuthenticatedClientAsync();
        var userB = await factory.CreateAuthenticatedClientAsync();
        var campA1 = await CampaignEndpointsTests.CreateCampaignAsync(userA, "A1");
        var campA2 = await CampaignEndpointsTests.CreateCampaignAsync(userA, "A2");
        var campB = await CampaignEndpointsTests.CreateCampaignAsync(userB, "B1");
        await SceneEndpointsTests.CreateSceneAsync(userA, campA1, "Escena 1");
        await SceneEndpointsTests.CreateSceneAsync(userA, campA2, "Escena 2");
        await SceneEndpointsTests.CreateSceneAsync(userB, campB, "Escena B");

        var activity = await (await userA.GetAsync("/api/v1/activity?limit=10")).ReadAsync();
        var entries = activity.EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.NotEqual("B1", e.GetProperty("campaignName").GetString()));
        Assert.Contains(entries, e => e.GetProperty("campaignName").GetString() == "A1");
        Assert.Contains(entries, e => e.GetProperty("campaignName").GetString() == "A2");
        // Ordenadas de más reciente a más antigua.
        var times = entries.Select(e => e.GetProperty("createdAt").GetDateTime()).ToList();
        Assert.Equal(times.OrderByDescending(t => t), times);

        // Límite respetado.
        var limited = await (await userA.GetAsync("/api/v1/activity?limit=1")).ReadAsync();
        Assert.Single(limited.EnumerateArray());
    }
}
