using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Flujo del DM (PMB-4.8, aditivo aprobado 2026-11-16): proponer, listar
/// propuestas con diff, aprobar (crea revisión) o rechazar (vuelve a borrador)
/// y permisos explícitos, con autoridad siempre en servidor.
/// </summary>
public class DmFlowTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task ProponerYListar_ConDiff_AprobarPublicaRevision()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        var item = await CreateItemAsync(owner, typeId, "Humano", "user");
        var id = item.GetProperty("id").GetGuid();

        using var propose = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/propose", new { });
        var proposed = await propose.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("proposed", proposed.GetProperty("workflowState").GetString());

        using var list = await owner.GetAsync("/api/v1/content/proposals");
        var rows = (await list.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        var row = Assert.Single(rows, x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal("Humano", row.GetProperty("name").GetString());
        Assert.True(row.TryGetProperty("authorName", out _));
        Assert.True(row.GetProperty("changes").GetArrayLength() > 0);

        using var approve = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/approve", new { });
        var published = await approve.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("published", published.GetProperty("workflowState").GetString());
        Assert.Equal(1, published.GetProperty("currentRevision").GetInt64());

        using var after = await owner.GetAsync("/api/v1/content/proposals");
        Assert.Empty((await after.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());
    }

    [Fact]
    public async Task Rechazar_DevuelveABorrador_SinPublicarNiRevision()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        var item = await CreateItemAsync(owner, typeId, "Humano", "user");
        var id = item.GetProperty("id").GetGuid();

        using var propose = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/propose", new { });
        await propose.EnsureStatusAsync(HttpStatusCode.OK);

        using var reject = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/reject", new { reason = "Falta documentar el cambio" });
        var draft = await reject.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("draft", draft.GetProperty("workflowState").GetString());
        Assert.Equal(0, draft.GetProperty("currentRevision").GetInt64());

        using var secondReject = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/reject", new { });
        await secondReject.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Aprobacion_EnCampaña_SoloElDmPuedeAprobar()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña DM");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var typeId = await CreateTypeAsync(player);
        var item = await CreateItemAsync(player, typeId, "Propuesta del jugador", "campaign",
            Guid.Parse(campaignId));
        var id = item.GetProperty("id").GetGuid();

        using var propose = await player.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/propose", new { });
        await propose.EnsureStatusAsync(HttpStatusCode.OK);

        using var playerApprove = await player.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/approve", new { });
        await playerApprove.EnsureStatusAsync(HttpStatusCode.Forbidden);

        using var dmApprove = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/approve", new { });
        var published = await dmApprove.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("published", published.GetProperty("workflowState").GetString());

        // El jugador ve la propuesta aperturada mientras estaba pendiente.
        using var proposals = await player.GetAsync("/api/v1/content/proposals?campaignId=" + campaignId);
        Assert.Empty((await proposals.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());
    }

    [Fact]
    public async Task Grants_AltaDuplicadoYBorrado_ConAutorizacion()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        var item = await CreateItemAsync(owner, typeId, "Humano", "user");
        var id = item.GetProperty("id").GetGuid();

        using var stranger = factory.CreateClient();
        var token = await stranger.RegisterUserAsync(TestClientExtensions.NewUserName("extra"));
        stranger.SetBearerToken(token);
        using var strangerGrants = await stranger.GetAsync($"/api/v1/content/items/{id}/grants");
        await strangerGrants.EnsureStatusAsync(HttpStatusCode.Forbidden);

        using var create = await owner.PostAsJsonAsync($"/api/v1/content/items/{id}/grants", new
        {
            subjectKind = "role", subjectId = "Player", permission = "view",
        });
        var grant = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var grantId = grant.GetProperty("id").GetGuid();

        using var list = await owner.GetAsync($"/api/v1/content/items/{id}/grants");
        var rows = (await list.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal("Player", rows[0].GetProperty("subjectId").GetString());

        using var duplicate = await owner.PostAsJsonAsync($"/api/v1/content/items/{id}/grants", new
        {
            subjectKind = "role", subjectId = "Player", permission = "view",
        });
        await duplicate.EnsureStatusAsync(HttpStatusCode.Conflict);

        using var badPermission = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/grants",
            new { subjectKind = "user", subjectId = "x", permission = "admin" });
        await badPermission.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var delete = await owner.DeleteAsync($"/api/v1/content/items/{id}/grants/{grantId}");
        await delete.EnsureStatusAsync(HttpStatusCode.NoContent);
        using var after = await owner.GetAsync($"/api/v1/content/items/{id}/grants");
        Assert.Empty((await after.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> CreateTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
        });
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> CreateItemAsync(
        HttpClient client, string typeId, string name, string scope, Guid? campaignId = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name,
            properties = new { speed = 30 },
            scope,
            campaignId,
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }
}
