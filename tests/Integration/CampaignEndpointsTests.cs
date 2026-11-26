using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

public class CampaignEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Create_ReturnsOwnerCampaign_AndAppearsInListAndDetail()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/v1/campaigns",
            new { name = "La Mazmorra Perdida" });
        var created = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var campaignId = created.GetProperty("id").GetString();
        Assert.Equal("Owner", created.GetProperty("role").GetString());
        Assert.Equal(1, created.GetProperty("memberCount").GetInt32());

        var list = await client.GetAsync("/api/v1/campaigns");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(listJson.EnumerateArray(),
            item => item.GetProperty("id").GetString() == campaignId);

        var detail = await client.GetAsync($"/api/v1/campaigns/{campaignId}");
        var detailJson = await detail.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("La Mazmorra Perdida", detailJson.GetProperty("name").GetString());
        Assert.Equal("Owner", detailJson.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Get_NonMember_Returns404WithoutRevealingExistence()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var stranger = await factory.CreateAuthenticatedClientAsync();

        var create = await owner.PostAsJsonAsync("/api/v1/campaigns", new { name = "Privada" });
        var campaignId = (await create.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString();

        var response = await stranger.GetAsync($"/api/v1/campaigns/{campaignId}");
        await response.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rename_ByOwner_ReturnsNewName()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CreateCampaignAsync(client, "Nombre inicial");

        var rename = await client.PatchAsJsonAsync($"/api/v1/campaigns/{campaignId}",
            new { name = "Nombre nuevo" });
        var json = await rename.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Nombre nuevo", json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Rename_ByPlayer_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CreateCampaignAsync(owner, "Camp");
        var player = await JoinAsPlayerAsync(factory, owner, campaignId);

        var rename = await player.PatchAsJsonAsync($"/api/v1/campaigns/{campaignId}",
            new { name = "Intento hostil" });

        await rename.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rename_WithoutToken_Returns401()
    {
        var anonymous = factory.CreateClient();
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CreateCampaignAsync(owner, "Camp");

        var rename = await anonymous.PatchAsJsonAsync($"/api/v1/campaigns/{campaignId}",
            new { name = "Sin sesión" });

        await rename.EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Archive_ByOwner_SetsArchivedAt()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CreateCampaignAsync(client, "Para archivar");

        var archive = await client.PostAsync($"/api/v1/campaigns/{campaignId}/archive", null);
        var json = await archive.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(json.GetProperty("archivedAt").ValueKind == System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Archive_ByGM_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CreateCampaignAsync(owner, "Camp");
        var gm = await JoinAsAsync(factory, owner, campaignId, "GM");

        var archive = await gm.PostAsync($"/api/v1/campaigns/{campaignId}/archive", null);

        await archive.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/campaigns", new { name = "   " });

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>Crea una campaña como Owner y devuelve su id.</summary>
    internal static async Task<string> CreateCampaignAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/campaigns", new { name });
        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return json.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("La campaña no incluye id.");
    }

    /// <summary>Únete a una campaña mediante invitación y devuelve el cliente del nuevo miembro.</summary>
    internal static async Task<HttpClient> JoinAsPlayerAsync(
        ApiTestFactory factory, HttpClient owner, string campaignId) =>
        await JoinAsAsync(factory, owner, campaignId, null);

    /// <summary>Crea una invitación con el rol indicado, la canjea y devuelve el cliente del nuevo miembro.</summary>
    internal static async Task<HttpClient> JoinAsAsync(
        ApiTestFactory factory, HttpClient owner, string campaignId, string? role,
        HttpClient? joiner = null)
    {
        var invite = await owner.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/invites",
            new { role });
        var inviteJson = await invite.EnsureStatusAsync(HttpStatusCode.Created);
        var code = inviteJson.GetProperty("code").GetString()
            ?? throw new InvalidOperationException("La invitación no incluye code.");

        if (joiner is null)
        {
            joiner = factory.CreateClient();
            var token = await joiner.RegisterUserAsync(TestClientExtensions.NewUserName("joiner"));
            joiner.SetBearerToken(token);
        }

        var redeem = await joiner.PostAsync($"/api/v1/invites/{code}/redeem", null);
        await redeem.EnsureStatusAsync(HttpStatusCode.OK);
        return joiner;
    }
}
