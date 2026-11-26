using Godot;
using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Escenas de una campaña: abrir (doble clic/Enter) y crear nueva. Al
/// entrar en la campaña el cliente se une a su grupo en vivo (se hace en
/// `Main`, que es quien gestiona la conexión).
/// </summary>
public partial class ScenesView : Control
{
    private readonly ApiClient _api;
    private readonly CampaignSummary _campaign;
    private readonly Action _onBack;
    private readonly Action<SceneSummary> _onOpenScene;
    private readonly Action? _onActors;

    private Label _status = null!;
    private LineEdit _newName = null!;
    private ItemList _list = null!;
    private List<SceneSummary> _scenes = new();

    public ScenesView(
        ApiClient api,
        CampaignSummary campaign,
        Action onBack,
        Action<SceneSummary> onOpenScene,
        Action? onActors = null)
    {
        _api = api;
        _campaign = campaign;
        _onBack = onBack;
        _onOpenScene = onOpenScene;
        _onActors = onActors;
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

        var back = new Button { Text = "← Campañas" };
        back.Pressed += () => _onBack();
        header.AddChild(back);

        header.AddChild(DesktopTheme.Title(_campaign.Name, 26));

        if (_onActors is not null)
        {
            var actors = new Button { Text = "Personajes" };
            actors.Pressed += () => _onActors();
            header.AddChild(actors);
        }

        var create = new HBoxContainer();
        create.AddThemeConstantOverride("separation", 8);
        box.AddChild(create);

        _newName = new LineEdit { PlaceholderText = "Nombre de la nueva escena" };
        _newName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        create.AddChild(_newName);

        var createButton = new Button { Text = "Crear escena" };
        createButton.Pressed += () => _ = CreateAsync();
        create.AddChild(createButton);

        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemActivated += index => OpenAt((int)index);
        box.AddChild(_list);

        _status = new Label { Text = "Cargando escenas…" };
        box.AddChild(_status);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var scenes = await _api.ListScenesAsync(_campaign.Id);
            MainThread.Run(() => Fill(scenes));
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
            _status.Text = "Escribe un nombre para la escena.";
            return;
        }

        try
        {
            var created = await _api.CreateSceneAsync(_campaign.Id, name);
            MainThread.Run(() =>
            {
                _newName.Text = string.Empty;
                _onOpenScene(new SceneSummary(
                    created.Id, created.CampaignId, created.Name, created.GridCellSize,
                    created.GridShape, created.CreatedAt, 0));
            });
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    private void Fill(List<SceneSummary> scenes)
    {
        _scenes = scenes;
        _list.Clear();
        foreach (var scene in scenes)
        {
            _list.AddItem($"{scene.Name}   ·   grid {scene.GridShape} {scene.GridCellSize}px   ·   {scene.TokenCount} tokens");
        }

        _status.Text = scenes.Count == 0
            ? "Todavía no hay escenas: crea la primera."
            : $"{scenes.Count} escena(s). Doble clic para abrir.";
    }

    private void OpenAt(int index)
    {
        if (index >= 0 && index < _scenes.Count)
        {
            _onOpenScene(_scenes[index]);
        }
    }

    private static string Describe(Exception exception) =>
        exception is ApiException apiException
            ? apiException.Message
            : "No se pudo contactar con el servidor.";
}
