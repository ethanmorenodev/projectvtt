using Microsoft.AspNetCore.SignalR.Client;

namespace ProjectVTT.Desktop.Api;

/// <summary>
/// Conexión en vivo al hub de campaña (`/hubs/campaign`): el mismo
/// contrato de comando/evento que consume la web (ADR-010). Los eventos
/// llegan en hilos del pool: se reparten como eventos C# y el consumidor
/// debe llevarlos al hilo principal (ver `Ui/MainThread`).
/// </summary>
public sealed class SceneConnection : IAsyncDisposable
{
    private readonly HubConnection _connection;

    /// <summary>Evento `TokenMoved` (movimiento confirmado de otro cliente o propio).</summary>
    public event Action<TokenMovedEvent>? TokenMoved;

    /// <summary>Evento `TokenCreated` (token nuevo visible sin recargar).</summary>
    public event Action<TokenCreatedEvent>? TokenCreated;

    /// <summary>Evento `MapUpdated` (mapa subido, reemplazado o borrado).</summary>
    public event Action<MapUpdatedEvent>? MapUpdated;

    /// <summary>
    /// Evento `SceneAnnotationsUpdated` (ADR-043): estado completo tras una
    /// escritura persistida. La HTTP sigue siendo la carga inicial/resync.
    /// </summary>
    public event Action<SceneAnnotationsUpdatedEvent>? SceneAnnotationsUpdated;

    /// <summary>Cambio de estado de la conexión ("conectado"/"desconectado").</summary>
    public event Action<string>? StatusChanged;

    public SceneConnection(string baseUrl, Func<string?> tokenProvider)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl.TrimEnd('/')}/hubs/campaign", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(tokenProvider());
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<TokenMovedEvent>("TokenMoved", e => TokenMoved?.Invoke(e));
        _connection.On<TokenCreatedEvent>("TokenCreated", e => TokenCreated?.Invoke(e));
        _connection.On<MapUpdatedEvent>("MapUpdated", e => MapUpdated?.Invoke(e));
        _connection.On<SceneAnnotationsUpdatedEvent>(
            "SceneAnnotationsUpdated", e => SceneAnnotationsUpdated?.Invoke(e));
        _connection.Reconnected += _ =>
        {
            StatusChanged?.Invoke("conectado");
            return Task.CompletedTask;
        };
        _connection.Closed += _ =>
        {
            StatusChanged?.Invoke("desconectado");
            return Task.CompletedTask;
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _connection.StartAsync(cancellationToken);

    /// <summary>Alta en el grupo de la campaña (verifica membresía en el servidor).</summary>
    public Task JoinCampaignAsync(Guid campaignId) =>
        _connection.InvokeAsync("JoinCampaign", campaignId.ToString());

    public Task LeaveCampaignAsync(Guid campaignId) =>
        _connection.InvokeAsync("LeaveCampaign", campaignId.ToString());

    /// <summary>Estado autoritativo de la escena (carga inicial y resincronización).</summary>
    public Task<SceneStateDto> SyncSceneAsync(Guid sceneId) =>
        _connection.InvokeAsync<SceneStateDto>("SyncScene", sceneId.ToString());

    /// <summary>Comando `MoveToken`; si falla (conflicto) lanza HubException.</summary>
    public Task<MoveTokenResult> MoveTokenAsync(MoveTokenCommand command) =>
        _connection.InvokeAsync<MoveTokenResult>("MoveToken", command);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
