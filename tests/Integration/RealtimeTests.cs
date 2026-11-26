using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Pruebas de tiempo real con dos clientes SignalR conectados a la vez
/// (vertical slice: movimiento sincronizado, permisos, idempotencia,
/// concurrencia y resincronización tras reconexión).
/// </summary>
public class RealtimeTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TwoClients_OwnerMovesToken_PlayerReceivesEventAndPersistedState()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, tokenId) = await CreateSceneWithTokenAsync(owner);
        var player = await factory.CreateAuthenticatedClientAsync();
        await JoinCampaignAsync(owner, campaignId, player);

        var ownerHub = await ConnectHubAsync(owner);
        var playerHub = await ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var received = new TaskCompletionSource<TokenMovedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            playerHub.On<TokenMovedEvent>("TokenMoved", evt => received.TrySetResult(evt));

            var result = await ownerHub.InvokeAsync<MoveTokenResult>("MoveToken",
                new MoveTokenCommand(Guid.NewGuid(), sceneId, tokenId, ExpectedVersion: 0, X: 5, Y: 7));

            Assert.False(result.Duplicate);
            Assert.Equal(1, result.Version);

            var evt = await received.Task.WaitAsync(EventTimeout);
            Assert.Equal(5, evt.X);
            Assert.Equal(7, evt.Y);
            Assert.Equal(1, evt.AggregateVersion);
            Assert.Equal(1, evt.SchemaVersion);
            Assert.Equal(sceneId, evt.SceneId);

            // El movimiento quedó persistido (visible por HTTP para cualquier miembro).
            var tokens = await player.GetAsync($"/api/v1/scenes/{sceneId}/tokens");
            var json = await tokens.EnsureStatusAsync(System.Net.HttpStatusCode.OK);
            var persisted = json.EnumerateArray().Single();
            Assert.Equal(5, persisted.GetProperty("x").GetDouble());
            Assert.Equal(7, persisted.GetProperty("y").GetDouble());
            Assert.Equal(1, persisted.GetProperty("version").GetInt64());
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    [Fact]
    public async Task PlayerCanMoveToken_OwnerReceivesEvent()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, tokenId) = await CreateSceneWithTokenAsync(owner);
        var player = await factory.CreateAuthenticatedClientAsync();
        await JoinCampaignAsync(owner, campaignId, player);

        var ownerHub = await ConnectHubAsync(owner);
        var playerHub = await ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var received = new TaskCompletionSource<TokenMovedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            ownerHub.On<TokenMovedEvent>("TokenMoved", evt => received.TrySetResult(evt));

            await playerHub.InvokeAsync<MoveTokenResult>("MoveToken",
                new MoveTokenCommand(Guid.NewGuid(), sceneId, tokenId, ExpectedVersion: 0, X: 2, Y: 2));

            var evt = await received.Task.WaitAsync(EventTimeout);
            Assert.Equal(2, evt.X);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    [Fact]
    public async Task NonMember_CannotJoinCampaignNorMoveToken()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, tokenId) = await CreateSceneWithTokenAsync(owner);
        var stranger = await factory.CreateAuthenticatedClientAsync();
        var hub = await ConnectHubAsync(stranger);
        try
        {
            var join = await Assert.ThrowsAnyAsync<Exception>(
                () => hub.InvokeAsync("JoinCampaign", campaignId));
            Assert.Contains("campaña", join.Message, StringComparison.OrdinalIgnoreCase);

            var move = await Assert.ThrowsAnyAsync<Exception>(
                () => hub.InvokeAsync<MoveTokenResult>("MoveToken",
                    new MoveTokenCommand(Guid.NewGuid(), sceneId, tokenId, 0, 1, 1)));
            Assert.Contains("acceso", move.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task HubWithoutToken_IsRejected()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/campaign"), options =>
            {
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task DuplicateCommand_IsNotAppliedTwice_AndBroadcastsOnce()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, tokenId) = await CreateSceneWithTokenAsync(owner);
        var player = await factory.CreateAuthenticatedClientAsync();
        await JoinCampaignAsync(owner, campaignId, player);

        var ownerHub = await ConnectHubAsync(owner);
        var playerHub = await ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var events = new ConcurrentQueue<TokenMovedEvent>();
            playerHub.On<TokenMovedEvent>("TokenMoved", evt => events.Enqueue(evt));

            var commandId = Guid.NewGuid();
            var first = await ownerHub.InvokeAsync<MoveTokenResult>(
                "MoveToken", new MoveTokenCommand(commandId, sceneId, tokenId, 0, 9, 9));
            var second = await ownerHub.InvokeAsync<MoveTokenResult>(
                "MoveToken", new MoveTokenCommand(commandId, sceneId, tokenId, 0, 9, 9));

            Assert.False(first.Duplicate);
            Assert.True(second.Duplicate);
            Assert.Equal(first.Version, second.Version);
            Assert.Equal(1, second.Version);

            await Task.Delay(500);
            Assert.Single(events);

            // La versión solo se incrementó una vez: el estado quedó intacto.
            var tokens = await owner.GetAsync($"/api/v1/scenes/{sceneId}/tokens");
            var json = await tokens.EnsureStatusAsync(System.Net.HttpStatusCode.OK);
            Assert.Equal(1, json.EnumerateArray().Single().GetProperty("version").GetInt64());
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    [Fact]
    public async Task StaleExpectedVersion_ReturnsConflict()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, tokenId) = await CreateSceneWithTokenAsync(owner);
        var hub = await ConnectHubAsync(owner);
        try
        {
            await hub.InvokeAsync("JoinCampaign", campaignId);

            await hub.InvokeAsync<MoveTokenResult>("MoveToken",
                new MoveTokenCommand(Guid.NewGuid(), sceneId, tokenId, 0, 1, 1));

            var stale = await Assert.ThrowsAnyAsync<Exception>(
                () => hub.InvokeAsync<MoveTokenResult>("MoveToken",
                    new MoveTokenCommand(Guid.NewGuid(), sceneId, tokenId, 0, 2, 2)));
            Assert.Contains("versión", stale.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Reconnect_ResyncsAuthoritativeState()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, tokenId) = await CreateSceneWithTokenAsync(owner);
        var player = await factory.CreateAuthenticatedClientAsync();
        await JoinCampaignAsync(owner, campaignId, player);

        var playerHub = await ConnectHubAsync(player);
        await playerHub.InvokeAsync("JoinCampaign", campaignId);
        var before = await playerHub.InvokeAsync<SceneStateDto>("SyncScene", sceneId);
        Assert.Equal(0, before.Tokens.Single().Version);
        await playerHub.DisposeAsync(); // el cliente se desconecta

        // Mientras tanto, otro miembro mueve el token.
        var ownerHub = await ConnectHubAsync(owner);
        await ownerHub.InvokeAsync("JoinCampaign", campaignId);
        await ownerHub.InvokeAsync<MoveTokenResult>("MoveToken",
            new MoveTokenCommand(Guid.NewGuid(), sceneId, tokenId, 0, 4, 4));
        await ownerHub.DisposeAsync();

        // Reconexión: el estado autoritativo reemplaza al obsoleto.
        var reconnected = await ConnectHubAsync(player);
        try
        {
            await reconnected.InvokeAsync("JoinCampaign", campaignId);
            var after = await reconnected.InvokeAsync<SceneStateDto>("SyncScene", sceneId);
            var token = after.Tokens.Single();
            Assert.Equal(1, token.Version);
            Assert.Equal(4, token.X);
            Assert.Equal(4, token.Y);
        }
        finally
        {
            await reconnected.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateToken_ByOwner_BroadcastsTokenCreatedToMembers()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var (campaignId, sceneId, _) = await CreateSceneWithTokenAsync(owner);
        var player = await factory.CreateAuthenticatedClientAsync();
        await JoinCampaignAsync(owner, campaignId, player);

        var playerHub = await ConnectHubAsync(player);
        try
        {
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var received = new TaskCompletionSource<TokenCreatedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            playerHub.On<TokenCreatedEvent>("TokenCreated", evt => received.TrySetResult(evt));

            // Creación por HTTP (no pasa por el hub): debe llegar igualmente.
            await SceneEndpointsTests.CreateTokenAsync(owner, sceneId.ToString(), "Refuerzo", 1, 1);

            var evt = await received.Task.WaitAsync(EventTimeout);
            Assert.Equal("Refuerzo", evt.Token.Name);
            Assert.Equal(sceneId, evt.SceneId);
            Assert.Equal(1, evt.SchemaVersion);
        }
        finally
        {
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -----------------------------------------------------

    /// <summary>Crea campaña + escena + token (0,0 v0) y devuelve sus ids.</summary>
    private static async Task<(Guid CampaignId, Guid SceneId, Guid TokenId)> CreateSceneWithTokenAsync(
        HttpClient owner)
    {
        var campaignId = Guid.Parse(
            await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp en vivo"));
        var scene = await SceneEndpointsTests.CreateSceneAsync(
            owner, campaignId.ToString(), "Mazmorra");
        var sceneId = Guid.Parse(scene.GetProperty("id").GetString()!);
        var token = await SceneEndpointsTests.CreateTokenAsync(
            owner, sceneId.ToString(), "Gnomo", 0, 0);
        var tokenId = Guid.Parse(token.GetProperty("id").GetString()!);
        return (campaignId, sceneId, tokenId);
    }

    /// <summary>Invita al usuario del cliente a la campaña con el rol de jugador.</summary>
    private async Task JoinCampaignAsync(HttpClient owner, Guid campaignId, HttpClient joiner)
    {
        await CampaignEndpointsTests.JoinAsAsync(
            factory, owner, campaignId.ToString(), null, joiner);
    }

    /// <summary>
    /// Conecta un cliente al hub de campaña (helper compartido en
    /// `TestClientExtensions.ConnectHubAsync`).
    /// </summary>
    private Task<HubConnection> ConnectHubAsync(HttpClient client) =>
        factory.ConnectHubAsync(client);
}
