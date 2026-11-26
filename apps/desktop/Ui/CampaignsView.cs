using Godot;
using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Lista de campañas del usuario con portada real (o inicial como
/// alternativa): abrir (clic/Enter), crear nueva y cerrar sesión.
/// </summary>
public partial class CampaignsView : Control
{
    private readonly ApiClient _api;
    private readonly string _userName;
    private readonly Action<CampaignSummary> _onOpen;
    private readonly Action _onSignOut;
    private readonly Action? _onLibrary;

    private Label _status = null!;
    private LineEdit _newName = null!;
    private VBoxContainer _list = null!;
    private int _loadGeneration;

    public CampaignsView(
        ApiClient api,
        string userName,
        Action<CampaignSummary> onOpen,
        Action onSignOut,
        Action? onLibrary = null)
    {
        _api = api;
        _userName = userName;
        _onOpen = onOpen;
        _onSignOut = onSignOut;
        _onLibrary = onLibrary;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 8);
        AddChild(box);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        box.AddChild(header);

        header.AddChild(DesktopTheme.Title("Campañas", 26));
        header.AddChild(DesktopTheme.Secondary($"— {_userName}"));

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        var refresh = new Button { Text = "Actualizar" };
        refresh.Pressed += () => _ = LoadAsync();
        header.AddChild(refresh);

        if (_onLibrary is not null)
        {
            var library = new Button { Text = "Biblioteca" };
            library.Pressed += () => _onLibrary();
            header.AddChild(library);
        }

        var signOut = new Button { Text = "Cerrar sesión" };
        signOut.Pressed += () => _onSignOut();
        header.AddChild(signOut);

        var create = new HBoxContainer();
        create.AddThemeConstantOverride("separation", 8);
        box.AddChild(create);

        _newName = new LineEdit { PlaceholderText = "Nombre de la nueva campaña" };
        _newName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        create.AddChild(_newName);

        var createButton = new Button { Text = "Crear campaña" };
        createButton.Pressed += () => _ = CreateAsync();
        create.AddChild(createButton);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        _status = new Label { Text = "Cargando campañas…" };
        box.AddChild(_status);

        _ = LoadAsync();
    }

    public override void _ExitTree() => _loadGeneration++;

    private async Task LoadAsync()
    {
        try
        {
            var campaigns = await _api.ListCampaignsAsync();
            MainThread.Run(() => Fill(campaigns));
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    private async Task CreateAsync()
    {
        var name = _newName.Text.Trim();
        if (name.Length == 0)
        {
            _status.Text = "Escribe un nombre para la campaña.";
            return;
        }

        try
        {
            var created = await _api.CreateCampaignAsync(name);
            MainThread.Run(() =>
            {
                _newName.Text = string.Empty;
                Open(created);
            });
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    /// <summary>
    /// Carga la portada en segundo plano. La inicial ya está visible, así
    /// que un 404 (sin portada) o un formato no rasterizable no es error.
    /// </summary>
    private async Task LoadCoverAsync(CampaignSummary campaign, CampaignCard card, int generation)
    {
        try
        {
            var cover = await _api.GetCampaignCoverAsync(campaign.Id);
            if (cover is null)
            {
                return;
            }

            MainThread.Run(() =>
            {
                if (generation != _loadGeneration || !GodotObject.IsInstanceValid(card))
                {
                    return;
                }

                card.SetCover(cover.Value.Bytes);
            });
        }
        catch (Exception)
        {
            // Se mantiene la inicial: la portada es aditiva y no bloquea la lista.
        }
    }

    private void Fill(List<CampaignSummary> campaigns)
    {
        _loadGeneration++;
        var generation = _loadGeneration;
        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var campaign in campaigns)
        {
            var card = new CampaignCard(campaign);
            card.Pressed += () => Open(campaign);
            _list.AddChild(card);
            _ = LoadCoverAsync(campaign, card, generation);
        }

        _status.Text = campaigns.Count == 0
            ? "Todavía no hay campañas: crea la primera."
            : $"{campaigns.Count} campaña(s). Pulsa una para abrir.";
    }

    private void Open(CampaignSummary campaign)
    {
        _status.Text = $"Abriendo {campaign.Name}…";
        _onOpen(campaign);
    }

    private static string Describe(Exception exception) =>
        exception is ApiException apiException
            ? apiException.Message
            : "No se pudo contactar con el servidor.";
}
