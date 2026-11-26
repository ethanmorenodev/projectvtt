using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Efectos y condiciones por HTTP: alta/baja con permisos ADR-012 (leer
/// es de miembro; aplicar y retirar es de propietario o GM+), unicidad
/// por actor (409), validación de nombre, sin IDOR y evento
/// `EffectsUpdated`.
/// </summary>
public class ActorEffectEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- Alta y baja -----------------------------------------------------

    [Fact]
    public async Task ApplyEffect_AppearsInList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var apply = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "Envenenado" });
        var json = await apply.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(actorId, json.GetProperty("actorId").GetString());
        var effects = json.GetProperty("effects");
        Assert.Equal(1, effects.GetArrayLength());
        Assert.Equal("Envenenado", effects[0].GetProperty("name").GetString());

        var list = await owner.GetAsync($"/api/v1/actors/{actorId}/effects");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, listJson.GetProperty("effects").GetArrayLength());
    }

    [Fact]
    public async Task ApplySameEffectTwice_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var first = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "Aturdido" });
        await first.EnsureStatusAsync(HttpStatusCode.Created);

        var second = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "Aturdido" });
        await second.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveEffect_ReturnsEmptyList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var effectId = await ApplyEffectAsync(owner, actorId, "Invisible");

        var remove = await owner.DeleteAsync(
            $"/api/v1/actors/{actorId}/effects/{effectId}");
        var json = await remove.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, json.GetProperty("effects").GetArrayLength());

        var list = await owner.GetAsync($"/api/v1/actors/{actorId}/effects");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, listJson.GetProperty("effects").GetArrayLength());
    }

    [Fact]
    public async Task RemoveUnknownEffect_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var remove = await owner.DeleteAsync(
            $"/api/v1/actors/{actorId}/effects/{Guid.NewGuid()}");
        await remove.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Permisos (ADR-012) ----------------------------------------------

    [Fact]
    public async Task PlayerAppliesEffectToOwnActor_Returns201()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);

        var apply = await player.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "Concentrado" });
        await apply.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PlayerAppliesEffectToForeignActor_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var apply = await player.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "Robo" });
        await apply.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GmRemovesEffectFromPlayerActor_Returns200()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        var effectId = await ApplyEffectAsync(player, actorId, "Cansado");

        var remove = await owner.DeleteAsync(
            $"/api/v1/actors/{actorId}/effects/{effectId}");
        await remove.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonMemberListsEffects_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var list = await outsider.GetAsync($"/api/v1/actors/{actorId}/effects");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveEffectOfAnotherActor_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorA = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var actorB = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var effectId = await ApplyEffectAsync(owner, actorA, "Marcado");

        // El efecto existe, pero no pertenece a actorB: 404, sin IDOR.
        var remove = await owner.DeleteAsync(
            $"/api/v1/actors/{actorB}/effects/{effectId}");
        await remove.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación -------------------------------------------------------

    [Fact]
    public async Task ApplyEffect_InvalidName_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var empty = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = "   " });
        var emptyJson = await empty.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("name", emptyJson.GetProperty("field").GetString());

        var tooLong = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name = new string('x', 101) });
        await tooLong.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Eventos en vivo --------------------------------------------------

    [Fact]
    public async Task TwoClients_ApplyAndRemove_EmitEffectsUpdated()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);

        var ownerHub = await factory.ConnectHubAsync(owner);
        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var events = Channel.CreateUnbounded<EffectsUpdatedEvent>();
            ownerHub.On<EffectsUpdatedEvent>(
                "EffectsUpdated", evt => events.Writer.TryWrite(evt));

            // El jugador aplica un efecto a SU actor (ADR-012)…
            var apply = await player.PostAsJsonAsync(
                $"/api/v1/actors/{actorId}/effects", new { name = "Envenenado" });
            await apply.EnsureStatusAsync(HttpStatusCode.Created);

            var applyEvt = await ReadEventAsync(events);
            Assert.Equal(Guid.Parse(campaignId), applyEvt.CampaignId);
            Assert.Equal(Guid.Parse(actorId), applyEvt.ActorId);
            Assert.Equal(1, applyEvt.SchemaVersion);
            Assert.Single(applyEvt.Effects);
            Assert.Equal("Envenenado", applyEvt.Effects[0].Name);

            // …y el GM lo retira: el evento llega con la lista vacía.
            var remove = await owner.DeleteAsync(
                $"/api/v1/actors/{actorId}/effects/{applyEvt.Effects[0].Id}");
            await remove.EnsureStatusAsync(HttpStatusCode.OK);

            var removeEvt = await ReadEventAsync(events);
            Assert.Empty(removeEvt.Effects);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<EffectsUpdatedEvent> ReadEventAsync(
        Channel<EffectsUpdatedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    /// <summary>Aplica un efecto y devuelve su id.</summary>
    private static async Task<string> ApplyEffectAsync(
        HttpClient client, string actorId, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/effects", new { name });
        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return json.GetProperty("effects")[0].GetProperty("id").GetString()
            ?? throw new InvalidOperationException("El efecto no incluye id.");
    }
}
