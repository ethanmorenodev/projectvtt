using Godot;
using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Hub mínimo de actores de la campaña (rebanada 3 de ADR-048): consume
/// `GET /api/v1/campaigns/{id}/actors` y, cuando el actor tiene retrato,
/// `GET /api/v1/images/{id}/file` a través de `ApiClient.GetImageFileAsync`.
/// Sin retrato (o si no se puede decodificar) muestra la inicial: la misma
/// alternativa que las portadas de campaña. Solo lectura.
/// </summary>
public partial class ActorPanel : Control
{
    private readonly ApiClient _api;
    private readonly CampaignSummary _campaign;
    private readonly Action _onBack;
    private VBoxContainer _list = null!;
    private Label _status = null!;
    private int _generation;

    public ActorPanel(ApiClient api, CampaignSummary campaign, Action onBack)
    {
        _api = api;
        _campaign = campaign;
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

        var back = new Button { Text = "← Escenas" };
        back.Pressed += () => _onBack();
        header.AddChild(back);
        header.AddChild(DesktopTheme.Title($"Personajes · {_campaign.Name}", 26));
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var refresh = new Button { Text = "Actualizar" };
        refresh.Pressed += () => _ = LoadAsync();
        header.AddChild(refresh);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        _status = new Label { Text = "Cargando personajes…" };
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_status);

        _ = LoadAsync();
    }

    public override void _ExitTree() => _generation++;

    private async Task LoadAsync()
    {
        var generation = ++_generation;
        try
        {
            var actors = await _api.ListActorsAsync(_campaign.Id);
            var portraits = await Task.WhenAll(actors.Select(LoadPortraitAsync));
            var byActor = portraits.ToDictionary(p => p.ActorId, p => p.Bytes);
            MainThread.Run(() =>
            {
                if (generation != _generation)
                {
                    return;
                }

                Fill(actors, byActor);
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

    /// <summary>Un retrato ausente o roto no debe impedir ver la ficha.</summary>
    private async Task<(Guid ActorId, byte[]? Bytes)> LoadPortraitAsync(ActorDto actor)
    {
        if (actor.PortraitId is not { } imageId)
        {
            return (actor.Id, null);
        }

        try
        {
            var image = await _api.GetImageFileAsync(imageId);
            return (actor.Id, image?.Bytes);
        }
        catch (Exception)
        {
            return (actor.Id, null);
        }
    }

    private void Fill(List<ActorDto> actors, Dictionary<Guid, byte[]?> portraits)
    {
        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var actor in actors)
        {
            portraits.TryGetValue(actor.Id, out var portrait);
            _list.AddChild(BuildRow(actor, portrait));
        }

        _status.Text = actors.Count == 0
            ? "Todavía no hay personajes en esta campaña."
            : $"{actors.Count} personaje(s).";
    }

    private static Control BuildRow(ActorDto actor, byte[]? portrait)
    {
        var panel = new PanelContainer();
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        panel.AddChild(row);

        var thumb = new Control { CustomMinimumSize = new Vector2(64, 64) };
        row.AddChild(thumb);

        var initial = new ColorRect { Color = DesktopTheme.AmberSoft };
        initial.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        thumb.AddChild(initial);

        var letter = new Label
        {
            Text = actor.Name.Length == 0 ? "?" : char.ToUpperInvariant(actor.Name[0]).ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = "Title",
        };
        letter.AddThemeFontSizeOverride("font_size", 30);
        letter.AddThemeColorOverride("font_color", DesktopTheme.Amber);
        letter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        thumb.AddChild(letter);

        if (portrait is not null && ImageBytes.TryDecode(portrait, out var image))
        {
            var texture = new TextureRect
            {
                Texture = ImageTexture.CreateFromImage(image),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            };
            texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            thumb.AddChild(texture);
            initial.Hide();
            letter.Hide();
        }

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);
        text.AddChild(DesktopTheme.Title(actor.Name, 16));
        text.AddChild(DesktopTheme.Secondary(
            $"{actor.Kind} · nivel {actor.Level} · {actor.RulesetKey}"));

        var attributes = string.Join(", ", actor.Attributes
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key} {pair.Value}"));
        if (attributes.Length > 0)
        {
            text.AddChild(DesktopTheme.Secondary(attributes));
        }

        var hitPoints = actor.Resources.TryGetValue("hitPoints", out var hp)
            ? $" · PV {hp.Current}/{hp.Max}"
            : string.Empty;
        text.AddChild(DesktopTheme.Secondary($"v{actor.Version}{hitPoints}"));
        if (!string.IsNullOrWhiteSpace(actor.Biography))
        {
            var biography = actor.Biography.Replace('\n', ' ');
            text.AddChild(DesktopTheme.Secondary(
                biography.Length <= 140 ? biography : $"{biography[..140]}…"));
        }

        return panel;
    }

    private static string Describe(Exception exception) =>
        exception is ApiException apiException
            ? apiException.Message
            : "No se pudo contactar con el servidor.";
}
