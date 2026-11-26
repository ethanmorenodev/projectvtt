using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Notas privadas del GM por HTTP (ADR-019): CRUD completo solo para GM
/// u Owner (un jugador recibe 403 y un no miembro 404), validación de
/// título y cuerpo, sin IDOR entre campañas y sin evento en vivo.
/// </summary>
public class NoteEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    // --- CRUD -------------------------------------------------------------

    [Fact]
    public async Task CreateNote_AppearsInList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes",
            new { title = "Sesión 3", body = "El dragón espera en la sala del trono." });
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal("Sesión 3", json.GetProperty("title").GetString());
        Assert.Equal(
            "El dragón espera en la sala del trono.", json.GetProperty("body").GetString());

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/notes");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, listJson.GetArrayLength());
        Assert.Equal("Sesión 3", listJson[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GmCanManageNotes_Returns200()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var gm = await CampaignEndpointsTests.JoinAsAsync(factory, owner, campaignId, "GM");

        var create = await gm.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes", new { title = "Del GM" });
        await create.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateNote_ChangesFieldsIndividually()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var noteId = await CreateNoteAsync(owner, campaignId, "Borrador", "Texto viejo");

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/notes/{noteId}", new { body = "Texto nuevo" });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Borrador", json.GetProperty("title").GetString());
        Assert.Equal("Texto nuevo", json.GetProperty("body").GetString());
    }

    [Fact]
    public async Task DeleteNote_RemovesFromList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var noteId = await CreateNoteAsync(owner, campaignId, "Temporal", "Se borra");

        var delete = await owner.DeleteAsync($"/api/v1/notes/{noteId}");
        await delete.EnsureStatusAsync(HttpStatusCode.OK);

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/notes");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, listJson.GetArrayLength());
    }

    [Fact]
    public async Task UpdateOrDeleteUnknownNote_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/notes/{Guid.NewGuid()}", new { title = "Nadie" });
        await patch.EnsureStatusAsync(HttpStatusCode.NotFound);

        var delete = await owner.DeleteAsync($"/api/v1/notes/{Guid.NewGuid()}");
        await delete.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Permisos ---------------------------------------------------------

    [Fact]
    public async Task PlayerCannotReadNotes_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        await CreateNoteAsync(owner, campaignId, "Secreta", "El tesoro está bajo el altar");

        var list = await player.GetAsync($"/api/v1/campaigns/{campaignId}/notes");
        await list.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlayerCannotCreateOrUpdate_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var noteId = await CreateNoteAsync(owner, campaignId, "Secreta", "Solo GM");

        var create = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes", new { title = "Tramposo" });
        await create.EnsureStatusAsync(HttpStatusCode.Forbidden);

        var patch = await player.PatchAsJsonAsync(
            $"/api/v1/notes/{noteId}", new { body = "Editada" });
        await patch.EnsureStatusAsync(HttpStatusCode.Forbidden);

        var delete = await player.DeleteAsync($"/api/v1/notes/{noteId}");
        await delete.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonMember_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var outsider = await factory.CreateAuthenticatedClientAsync();
        var noteId = await CreateNoteAsync(owner, campaignId, "Secreta", "Privada");

        var list = await outsider.GetAsync($"/api/v1/campaigns/{campaignId}/notes");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);

        var patch = await outsider.PatchAsJsonAsync(
            $"/api/v1/notes/{noteId}", new { title = "Hackeada" });
        await patch.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NoteOfAnotherCampaign_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");
        var noteId = await CreateNoteAsync(owner, campaignId, "Secreta", "De otra campaña");

        // GM de OTRA campaña: la nota existe, pero no es de su campaña (404, sin IDOR).
        var other = await factory.CreateAuthenticatedClientAsync();
        await CampaignEndpointsTests.CreateCampaignAsync(other, "Camp ajena");
        var patch = await other.PatchAsJsonAsync(
            $"/api/v1/notes/{noteId}", new { title = "Robada" });
        await patch.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación -------------------------------------------------------

    [Fact]
    public async Task InvalidTitleOrBody_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp notas");

        var emptyTitle = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes", new { title = "   ", body = "" });
        var emptyTitleJson = await emptyTitle.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("title", emptyTitleJson.GetProperty("field").GetString());

        var tooLongBody = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes",
            new { title = "Larga", body = new string('x', 5001) });
        var tooLongBodyJson = await tooLongBody.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("body", tooLongBodyJson.GetProperty("field").GetString());
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<string> CreateNoteAsync(
        HttpClient client, string campaignId, string title, string body)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/notes", new { title, body });
        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return json.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("La nota no incluye id.");
    }
}
