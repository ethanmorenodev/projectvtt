using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Data;

namespace ProjectVTT.Tests.Integration;

public class MembershipAndInviteTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    // --- Membresías -----------------------------------------------------

    [Fact]
    public async Task ListMembers_ReturnsOwnerAndPlayer()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var response = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/members");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);

        var roles = json.EnumerateArray().Select(m => m.GetProperty("role").GetString()).ToList();
        Assert.Equal(2, roles.Count);
        Assert.Contains("Owner", roles);
        Assert.Contains("Player", roles);
    }

    [Fact]
    public async Task OwnerChangesRole_PlayerToGM()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerId = await GetUserIdAsync(player);

        var patch = await player.PatchAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/members/{playerId}", new { role = "GM" });
        // Un jugador no puede cambiarse el rol a sí mismo: solo el Owner.
        await patch.EnsureStatusAsync(HttpStatusCode.Forbidden);

        var ownerPatch = await owner.PatchAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/members/{playerId}", new { role = "GM" });
        var json = await ownerPatch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("GM", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task PlayerCannotChangeRoles_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var ownerId = await GetUserIdAsync(owner);

        var patch = await player.PatchAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/members/{ownerId}", new { role = "Player" });

        await patch.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OwnerRemovesMember_Returns204_AndRemovedUserLosesAccess()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerId = await GetUserIdAsync(player);

        var remove = await owner.DeleteAsync($"/api/v1/campaigns/{campaignId}/members/{playerId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var access = await player.GetAsync($"/api/v1/campaigns/{campaignId}");
        await access.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovingLastOwner_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var ownerId = await GetUserIdAsync(owner);

        var remove = await owner.DeleteAsync($"/api/v1/campaigns/{campaignId}/members/{ownerId}");

        await remove.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DemotingLastOwner_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var ownerId = await GetUserIdAsync(owner);

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/members/{ownerId}", new { role = "Player" });

        await patch.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NonMemberCannotListMembers_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var stranger = await factory.CreateAuthenticatedClientAsync();

        var response = await stranger.GetAsync($"/api/v1/campaigns/{campaignId}/members");

        await response.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Invitaciones ---------------------------------------------------

    [Fact]
    public async Task PlayerCannotCreateInvite_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var invite = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/invites", new { role = "Player" });

        await invite.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateInvite_WithOwnerRole_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");

        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/invites", new { role = "Owner" });

        await invite.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RedeemInvite_AddsMemberWithInviteRole()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var gm = await CampaignEndpointsTests.JoinAsAsync(factory, owner, campaignId, "GM");

        var members = await gm.GetAsync($"/api/v1/campaigns/{campaignId}/members");
        var json = await members.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(json.EnumerateArray(),
            m => m.GetProperty("role").GetString() == "GM");

        // El nuevo miembro ya tiene acceso a la campaña.
        var detail = await gm.GetAsync($"/api/v1/campaigns/{campaignId}");
        await detail.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RedeemUnknownCode_Returns404()
    {
        var joiner = await factory.CreateAuthenticatedClientAsync();

        var redeem = await joiner.PostAsync("/api/v1/invites/no-existe/redeem", null);

        await redeem.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeInvite_ThenRedeem_Returns410()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var invite = await CreateInviteAsync(owner, campaignId, null);
        var inviteId = invite.GetProperty("id").GetString()!;
        var code = invite.GetProperty("code").GetString()!;

        var revoke = await owner.DeleteAsync($"/api/v1/campaigns/{campaignId}/invites/{inviteId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var joiner = await factory.CreateAuthenticatedClientAsync();
        var redeem = await joiner.PostAsync($"/api/v1/invites/{code}/redeem", null);
        await redeem.EnsureStatusAsync(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task ExpiredInvite_Redeem_Returns410()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var invite = await CreateInviteAsync(owner, campaignId, null);
        var code = invite.GetProperty("code").GetString()!;

        // Se simula la caducidad manipulando la base de datos de la prueba.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.CampaignInvites.SingleAsync(i => i.Code == code);
            entity.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var joiner = await factory.CreateAuthenticatedClientAsync();
        var redeem = await joiner.PostAsync($"/api/v1/invites/{code}/redeem", null);
        await redeem.EnsureStatusAsync(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task RedeemTwice_IsIdempotent_AndKeepsSingleMembership()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var invite = await CreateInviteAsync(owner, campaignId, null);
        var code = invite.GetProperty("code").GetString()!;

        var joiner = await factory.CreateAuthenticatedClientAsync();
        var first = await joiner.PostAsync($"/api/v1/invites/{code}/redeem", null);
        var firstJson = await first.EnsureStatusAsync(HttpStatusCode.OK);
        var second = await joiner.PostAsync($"/api/v1/invites/{code}/redeem", null);
        var secondJson = await second.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.Equal(firstJson.GetProperty("campaignId").GetString(),
            secondJson.GetProperty("campaignId").GetString());
        Assert.Equal("Player", secondJson.GetProperty("role").GetString());

        var members = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/members");
        var membersJson = await members.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, membersJson.GetArrayLength());
    }

    // --- Utilidades -----------------------------------------------------

    private static async Task<JsonElement> CreateInviteAsync(
        HttpClient owner, string campaignId, string? role)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/invites", new { role });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<string> GetUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/me");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return json.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("/auth/me no incluye id.");
    }
}
