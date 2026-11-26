using Godot;
using ProjectVTT.Desktop.Api;
using ProjectVTT.Desktop.Ui;

/// <summary>
/// Escena raíz del cliente de escritorio (Épica G, ADR-006): navegación
/// entre acceso, campañas, escenas y la escena en juego. Con `--smoke`
/// ejecuta además el recorrido end-to-end contra el backend real.
/// </summary>
public partial class Main : Node2D
{
    private ApiClient _api = null!;
    private SceneConnection? _connection;
    private UserDto? _user;
    private CampaignSummary? _campaign;
    private Control _host = null!;

    public override void _Ready()
    {
        _api = new ApiClient();
        _host = new Control();
        _host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _host.Theme = DesktopTheme.Build(); // «Tinta y ámbar», rebanada 1.
        AddChild(_host);

        GD.Print($"ProjectVTT Desktop listo → {_api.BaseUrl}");
        ShowLogin();

        if (System.Environment.GetCommandLineArgs().Contains("--smoke"))
        {
            GD.Print("SMOKE: ejecutando el recorrido end-to-end…");
            _ = ProjectVTT.Desktop.Smoke.RunAsync(Quit, MountViewsForSmokeAsync);
        }
    }

    public override void _Process(double delta) => MainThread.Drain();

    public override void _ExitTree() => _ = DisposeConnectionAsync();

    // --- Navegación -----------------------------------------------------

    private void ShowLogin()
    {
        Swap(new LoginView(_api, OnSignedIn));
    }

    private void ShowCampaigns()
    {
        _ = LeaveCampaignAsync();
        _campaign = null;
        Swap(new CampaignsView(
            _api, _user?.UserName ?? string.Empty, OpenCampaign, SignOut, ShowLibrary));
    }

    /// <summary>Biblioteca global de contenido: no depende de la campaña abierta.</summary>
    private void ShowLibrary() => Swap(new LibraryView(_api, ShowCampaigns));

    private void ShowActors()
    {
        if (_campaign is null)
        {
            ShowCampaigns();
            return;
        }

        Swap(new ActorPanel(_api, _campaign, ShowScenes));
    }

    private void ShowScenes()
    {
        if (_campaign is null)
        {
            ShowCampaigns();
            return;
        }

        Swap(new ScenesView(_api, _campaign, ShowCampaigns, OpenScene, ShowActors));
    }

    private void OpenCampaign(CampaignSummary campaign)
    {
        _campaign = campaign;
        _ = JoinCampaignAsync(campaign.Id);
        ShowScenes();
    }

    private void OpenScene(SceneSummary scene)
    {
        if (_connection is null)
        {
            GD.PushError("Sin conexión en vivo: vuelve a iniciar sesión.");
            ShowLogin();
            return;
        }

        Swap(new SceneView(_api, _connection, scene.Id, scene.Name, ShowScenes));
    }

    // --- Sesión y conexión ----------------------------------------------

    private async void OnSignedIn(AuthResponse response)
    {
        _api.Token = response.Token;
        _user = response.User;
        _connection = new SceneConnection(_api.BaseUrl, () => _api.Token);
        try
        {
            await _connection.StartAsync();
            GD.Print("Conexión en vivo establecida.");
        }
        catch (System.Exception exception)
        {
            GD.PrintErr($"No se pudo conectar el hub: {exception.Message}");
        }

        MainThread.Run(ShowCampaigns);
    }

    private void SignOut()
    {
        _api.Token = null;
        _user = null;
        _ = DisposeConnectionAsync();
        _connection = null;
        ShowLogin();
    }

    private async Task JoinCampaignAsync(System.Guid campaignId)
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await _connection.JoinCampaignAsync(campaignId);
        }
        catch (System.Exception exception)
        {
            GD.PrintErr($"No se pudo unir a la campaña: {exception.Message}");
        }
    }

    private async Task LeaveCampaignAsync()
    {
        if (_connection is null || _campaign is null)
        {
            return;
        }

        try
        {
            await _connection.LeaveCampaignAsync(_campaign.Id);
        }
        catch (System.Exception exception)
        {
            GD.PrintErr($"No se pudo salir de la campaña: {exception.Message}");
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_connection is null)
        {
            return;
        }

        var connection = _connection;
        _connection = null;
        try
        {
            await connection.DisposeAsync();
        }
        catch (System.Exception exception)
        {
            GD.PrintErr($"Error al cerrar la conexión: {exception.Message}");
        }
    }

    // --- Utilidades -----------------------------------------------------

    private void Swap(Control view)
    {
        foreach (var child in _host.GetChildren())
        {
            child.QueueFree();
        }

        _host.AddChild(view);
    }

    private void Quit(int code) => MainThread.Run(() =>
    {
        _ = DisposeConnectionAsync();
        GetTree().Quit(code);
    });

    /// <summary>
    /// Monta las vistas reales en modo smoke (cada `Swap` ejecuta su
    /// `_Ready` y deja correr sus cargas async antes de la siguiente).
    /// </summary>
    private async Task MountViewsForSmokeAsync(
        ApiClient api, CampaignSummary campaign, SceneDetail scene, SceneConnection connection)
    {
        MainThread.Run(() => Swap(new CampaignsView(api, "smoke", OpenCampaign, SignOut)));
        await Task.Delay(1500);
        MainThread.Run(() => Swap(new ScenesView(api, campaign, ShowCampaigns, OpenScene)));
        await Task.Delay(1500);
        MainThread.Run(() => Swap(new SceneView(api, connection, scene.Id, scene.Name, ShowScenes)));
        await Task.Delay(3000);
        MainThread.Run(() => Swap(new LibraryView(api, ShowCampaigns)));
        await Task.Delay(1500);
        MainThread.Run(() => Swap(new ActorPanel(api, campaign, ShowScenes)));
        await Task.Delay(1500);
    }
}
