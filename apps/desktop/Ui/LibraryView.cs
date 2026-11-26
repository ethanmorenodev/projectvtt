using System.Text.Json.Nodes;
using Godot;
using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Biblioteca de contenido de **solo lectura** (rebanada 2 de ADR-048):
/// `GET /api/v1/content/items` y `GET /api/v1/content/types` a través de
/// `ApiClient`, con los filtros opcionales `system` y `typeKey`. Los permisos,
/// ámbitos y la secretaría del DM ya los resolvió el servidor: esta vista
/// no crea, edita ni publica nada.
/// </summary>
public partial class LibraryView : Control
{
    private readonly ApiClient _api;
    private readonly Action _onBack;
    private readonly List<ContentItemDto> _items = new();
    private readonly List<ContentTypeDto> _types = new();

    private LineEdit _system = null!;
    private OptionButton _typeFilter = null!;
    private ItemList _list = null!;
    private VBoxContainer _detail = null!;
    private Label _status = null!;
    private int _generation;

    public LibraryView(ApiClient api, Action onBack)
    {
        _api = api;
        _onBack = onBack;
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
        header.AddChild(DesktopTheme.Title("Biblioteca de contenido", 26));

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        var refresh = new Button { Text = "Actualizar" };
        refresh.Pressed += () => _ = LoadAsync();
        header.AddChild(refresh);

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 8);
        box.AddChild(filters);

        _system = new LineEdit { PlaceholderText = "Sistema (p. ej. dnd5e)" };
        _system.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        filters.AddChild(_system);

        _typeFilter = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
        _typeFilter.AddItem("Todos los tipos");
        _typeFilter.Selected = 0;
        filters.AddChild(_typeFilter);

        var apply = new Button { Text = "Filtrar" };
        apply.Pressed += () => _ = LoadAsync();
        filters.AddChild(apply);

        var split = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 10);
        box.AddChild(split);

        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _list.SizeFlagsStretchRatio = 1.2f;
        _list.ItemSelected += index => ShowDetail((int)index);
        split.AddChild(_list);

        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1f,
        };
        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 6);
        _detail.AddChild(DesktopTheme.Title("Detalle", 18));
        panel.AddChild(_detail);
        split.AddChild(panel);

        _status = new Label { Text = "Cargando biblioteca…" };
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_status);

        _ = LoadAsync();
    }

    public override void _ExitTree() => _generation++;

    private async Task LoadAsync()
    {
        // Captura de controles en el hilo principal antes del primer await.
        var generation = ++_generation;
        var system = _system.Text.Trim();
        var typeKey = SelectedTypeKey();
        try
        {
            var typesTask = _api.ListContentTypesAsync();
            var items = await _api.ListContentItemsAsync(system, typeKey);
            var types = await typesTask;
            MainThread.Run(() =>
            {
                if (generation != _generation)
                {
                    return;
                }

                FillTypes(types);
                FillItems(items);
            });
        }
        catch (Exception exception)
        {
            MainThread.Run(() =>
            {
                if (generation == _generation)
                {
                    _status.Text = Describe(exception);
                }
            });
        }
    }

    private string? SelectedTypeKey()
    {
        var index = _typeFilter.Selected;
        return index > 0 && index < _typeFilter.ItemCount
            ? _typeFilter.GetItemMetadata(index).AsString()
            : null;
    }

    private void FillTypes(List<ContentTypeDto> types)
    {
        _types.Clear();
        _types.AddRange(types);
        if (_typeFilter.ItemCount > 1)
        {
            // Una recarga no debe perder el filtro ya elegido.
            return;
        }

        foreach (var type in types.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            _typeFilter.AddItem($"{type.DisplayName} ({type.Key})");
            _typeFilter.SetItemMetadata(_typeFilter.ItemCount - 1, type.Key);
        }
    }

    private void FillItems(List<ContentItemDto> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _list.Clear();
        foreach (var item in items)
        {
            _list.AddItem(
                $"{item.Name}   ·   {TypeName(item.TypeKey)}   ·   {item.Scope}   ·   {Short(item.Summary)}");
        }

        _status.Text = items.Count == 0
            ? "No hay ítems visibles con estos filtros."
            : $"{items.Count} ítem(s). Selecciona uno para ver el detalle.";
    }

    private void ShowDetail(int index)
    {
        foreach (var child in _detail.GetChildren().Skip(1))
        {
            child.QueueFree();
        }

        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        var item = _items[index];
        _detail.AddChild(DesktopTheme.Title(item.Name, 20));
        _detail.AddChild(DesktopTheme.Secondary(
            $"{TypeName(item.TypeKey)} · {item.System} · {item.Scope}"));
        AddLine($"Resumen: {Short(item.Summary, 240)}");
        AddLine($"Descripción: {Short(item.Description, 300)}");
        AddLine($"Etiquetas: {(item.Tags.Length == 0 ? "—" : string.Join(", ", item.Tags))}");
        AddLine($"Estado: {item.WorkflowState} · revisión {item.CurrentRevision}");

        if (item.Properties is JsonObject properties)
        {
            _detail.AddChild(DesktopTheme.Secondary("Propiedades"));
            var shown = 0;
            foreach (var (key, value) in properties)
            {
                if (shown++ >= 8)
                {
                    AddLine("…");
                    break;
                }

                AddLine($"{key}: {Short(value?.ToJsonString(), 120)}");
            }
        }
    }

    private void AddLine(string text)
    {
        var label = new Label { Text = text };
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detail.AddChild(label);
    }

    private string TypeName(string key) =>
        _types.FirstOrDefault(t => t.Key == key)?.DisplayName ?? key;

    private static string Short(string? text, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "—";
        }

        var value = text.Replace('\n', ' ');
        return value.Length <= max ? value : $"{value[..max]}…";
    }

    private static string Describe(Exception exception) =>
        exception is ApiException apiException
            ? apiException.Message
            : "No se pudo contactar con el servidor.";
}
