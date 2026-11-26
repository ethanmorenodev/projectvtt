using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Rules;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Tiradas HTTP (Épica E): el servidor lanza el dado, el Ruleset resuelve,
/// la petición es idempotente por commandId y el evento llega a la campaña.
/// </summary>
public class RollEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task OwnerRollsAbilityCheck_ReturnsAuditableBreakdown()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp tiradas");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            NewRoll(actorId, "strength"));
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.False(json.GetProperty("duplicate").GetBoolean());
        var roll = json.GetProperty("roll");
        Assert.Equal(actorId, roll.GetProperty("actorId").GetString());
        Assert.Equal("strength", roll.GetProperty("attribute").GetString());

        var breakdown = roll.GetProperty("breakdown");
        Assert.Equal("dnd5e", breakdown.GetProperty("rulesetKey").GetString());
        Assert.Equal("ability-check", breakdown.GetProperty("kind").GetString());
        Assert.Equal(16, breakdown.GetProperty("attributeValue").GetInt32());
        Assert.Equal(3, breakdown.GetProperty("modifier").GetInt32()); // floor((16-10)/2)
        var die = breakdown.GetProperty("die").GetInt32();
        Assert.InRange(die, 1, 20);
        // Sin efectos reconocidos: modalidad normal, un dado, cero efectos.
        Assert.Equal(RollModes.Normal, breakdown.GetProperty("rollMode").GetString());
        Assert.Equal(1, breakdown.GetProperty("dice").GetArrayLength());
        Assert.Equal(die, breakdown.GetProperty("dice")[0].GetInt32());
        Assert.Equal(0, breakdown.GetProperty("effectModifier").GetInt32());
        Assert.Equal(0, breakdown.GetProperty("effects").GetArrayLength());
        Assert.Equal(die + 3, breakdown.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, breakdown.GetProperty("dc").ValueKind);
        Assert.Contains("1d20", breakdown.GetProperty("formula").GetString());
    }

    [Fact]
    public async Task Roll_WithRecognizedEffect_UsesTwoDiceAndCatalogEntry()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        // Efecto reconocido por el catálogo del Ruleset (ADR-015).
        var apply = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "Envenenado" });
        await apply.EnsureStatusAsync(HttpStatusCode.Created);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            NewRoll(actorId, "strength"));
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var breakdown = json.GetProperty("roll").GetProperty("breakdown");

        Assert.Equal(RollModes.Disadvantage, breakdown.GetProperty("rollMode").GetString());
        var dice = breakdown.GetProperty("dice").EnumerateArray()
            .Select(value => value.GetInt32()).ToList();
        Assert.Equal(2, dice.Count);
        // Con desventaja se conserva el menor de los dos dados.
        var die = breakdown.GetProperty("die").GetInt32();
        Assert.Equal(dice.Min(), die);
        Assert.Equal(3, breakdown.GetProperty("modifier").GetInt32()); // fuerza 16

        var effects = breakdown.GetProperty("effects");
        Assert.Equal(1, effects.GetArrayLength());
        Assert.Equal("Envenenado", effects[0].GetProperty("name").GetString());
        Assert.Equal("disadvantage", effects[0].GetProperty("kind").GetString());
        Assert.Equal(0, effects[0].GetProperty("value").GetInt32());

        Assert.Equal(die + 3, breakdown.GetProperty("total").GetInt32());
        var formula = breakdown.GetProperty("formula").GetString();
        Assert.Contains("desventaja", formula);
        Assert.Contains("Envenenado", formula);
    }

    [Fact]
    public async Task PlayerCanRoll_AndHistoryListsIt()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        // El jugador tira con SU actor (lo crea él mismo; ADR-012).
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);

        var response = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            NewRoll(actorId, "dexterity", extra: 2, dc: 10));
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var rollId = json.GetProperty("roll").GetProperty("rollId").GetString();
        var breakdown = json.GetProperty("roll").GetProperty("breakdown");
        Assert.Equal(2, breakdown.GetProperty("extra").GetInt32());
        Assert.Equal(10, breakdown.GetProperty("dc").GetInt32());

        // Aritmética auditable: destreza 14 → +2; total = dado + mod + extra.
        var die = breakdown.GetProperty("die").GetInt32();
        Assert.Equal(2, breakdown.GetProperty("modifier").GetInt32());
        Assert.Equal(die + 2 + 2, breakdown.GetProperty("total").GetInt32());
        Assert.Equal(die + 4 >= 10, breakdown.GetProperty("success").GetBoolean());

        // Segunda tirada para comprobar el orden del historial.
        var second = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            NewRoll(actorId, "wisdom"));
        await second.EnsureStatusAsync(HttpStatusCode.OK);

        var history = await player.GetAsync($"/api/v1/campaigns/{campaignId}/rolls");
        var list = await history.EnsureStatusAsync(HttpStatusCode.OK);
        var rolls = list.EnumerateArray().ToList();
        Assert.Equal(2, rolls.Count);
        Assert.Contains(rolls, r => r.GetProperty("rollId").GetString() == rollId);
        // Más reciente primero: la API de historial lo garantiza.
        Assert.True(
            rolls[0].GetProperty("occurredAt").GetString()!
            .CompareTo(rolls[1].GetProperty("occurredAt").GetString()) >= 0);
    }

    [Fact]
    public async Task SameCommandId_ReplaysWithoutRerolling()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var commandId = Guid.NewGuid();
        var body = NewRoll(actorId, "wisdom", commandId);

        var first = await owner.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/rolls", body);
        var firstJson = await first.EnsureStatusAsync(HttpStatusCode.OK);
        var die = firstJson.GetProperty("roll").GetProperty("breakdown").GetProperty("die").GetInt32();
        Assert.False(firstJson.GetProperty("duplicate").GetBoolean());

        var second = await owner.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/rolls", body);
        var secondJson = await second.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(secondJson.GetProperty("duplicate").GetBoolean());
        Assert.Equal(die, secondJson.GetProperty("roll").GetProperty("breakdown").GetProperty("die").GetInt32());
        Assert.Equal(
            firstJson.GetProperty("roll").GetProperty("rollId").GetString(),
            secondJson.GetProperty("roll").GetProperty("rollId").GetString());
    }

    [Fact]
    public async Task UnknownAttribute_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            NewRoll(actorId, "luck"));
        var json = await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("attribute", json.GetProperty("field").GetString());
    }

    [Fact]
    public async Task PlayerCannotRollOthersActor_ButGmCanRollAny()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var gmActor = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // El actor pertenece al GM: el jugador no puede tirar con él.
        var denied = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls", NewRoll(gmActor, "strength"));
        await denied.EnsureStatusAsync(HttpStatusCode.Forbidden);

        // El GM sí puede tirar con el actor de un jugador.
        var playerActor = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        var allowed = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls", NewRoll(playerActor, "wisdom"));
        await allowed.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MissingCommandId_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        // Sin commandId no hay idempotencia posible: el servidor lo rechaza.
        var response = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            new { actorId, attribute = "strength" });
        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonMemberCannotRoll_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var response = await outsider.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/rolls",
            NewRoll(actorId, "strength"));
        await response.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TwoClients_PlayerRolls_OwnerReceivesRollResolved()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        // El jugador tira con SU propio actor (ADR-012).
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);

        var ownerHub = await factory.ConnectHubAsync(owner);
        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var received = new TaskCompletionSource<RollResolvedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            ownerHub.On<RollResolvedEvent>("RollResolved", evt => received.TrySetResult(evt));

            var response = await player.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/rolls",
                NewRoll(actorId, "charisma"));
            var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
            var rollId = json.GetProperty("roll").GetProperty("rollId").GetString();

            var evt = await received.Task.WaitAsync(EventTimeout);
            Assert.Equal(1, evt.SchemaVersion);
            Assert.Equal(Guid.Parse(campaignId), evt.CampaignId);
            Assert.Equal(Guid.Parse(actorId), evt.Roll.ActorId);
            Assert.Equal(Guid.Parse(rollId!), evt.Roll.RollId);
            Assert.Equal("charisma", evt.Roll.Attribute);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    [Fact]
    public async Task ListRulesets_IncludesDnd5e()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/v1/rulesets");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var dnd = json.EnumerateArray().Single(r => r.GetProperty("key").GetString() == "dnd5e");
        Assert.Contains(dnd.GetProperty("attributeKeys").EnumerateArray(),
            a => a.GetString() == "strength");
        Assert.Contains(dnd.GetProperty("resourceKeys").EnumerateArray(),
            a => a.GetString() == "hitPoints");
    }

    private static object NewRoll(string actorId, string attribute, Guid? commandId = null,
        int extra = 0, int? dc = null) => new
    {
        commandId = commandId ?? Guid.NewGuid(),
        actorId,
        attribute,
        extra,
        dc,
    };
}
