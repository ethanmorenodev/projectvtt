using Godot;
using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Fila de campaña con miniatura: usa la portada real cuando responde
/// `GET /api/v1/campaigns/{id}/cover` y, si no hay portada (404, SVG que
/// Godot no rasteriza o error de decodificación), muestra la inicial del
/// nombre sobre pergamino ámbar. Es una vista: no decide permisos ni
/// contenido, solo presenta lo que ya validó el servidor.
/// </summary>
public partial class CampaignCard : Button
{
    private const float ThumbSize = 64f;

    private readonly TextureRect _cover;
    private readonly Panel _initialPanel;

    public CampaignCard(CampaignSummary campaign)
    {
        CustomMinimumSize = new Vector2(0, ThumbSize + 16);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("normal", Frame(DesktopTheme.Ink2, DesktopTheme.Border));
        AddThemeStyleboxOverride("hover", Frame(DesktopTheme.Ink3, DesktopTheme.AmberBorder));
        AddThemeStyleboxOverride("pressed", Frame(DesktopTheme.AmberSoft, DesktopTheme.Amber));
        AddThemeColorOverride("font_color", DesktopTheme.Parchment);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(row);

        var thumbSlot = new Control { CustomMinimumSize = new Vector2(ThumbSize, ThumbSize) };
        row.AddChild(thumbSlot);

        _initialPanel = new Panel();
        _initialPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _initialPanel.AddThemeStyleboxOverride("panel",
            Frame(DesktopTheme.AmberSoft, DesktopTheme.AmberBorder, 6));
        thumbSlot.AddChild(_initialPanel);

        var initial = new Label
        {
            Text = Initial(campaign.Name),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = "Title",
        };
        initial.AddThemeFontSizeOverride("font_size", 30);
        initial.AddThemeColorOverride("font_color", DesktopTheme.Amber);
        initial.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _initialPanel.AddChild(initial);

        _cover = new TextureRect
        {
            Visible = false,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        _cover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        thumbSlot.AddChild(_cover);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);
        text.AddChild(DesktopTheme.Title(campaign.Name, 16));
        text.AddChild(DesktopTheme.Secondary($"· {campaign.Role}"));

        // El Button debe recibir el clic aunque haya nodos por encima.
        IgnoreMouse(row);
    }

    /// <summary>Sustituye la inicial por la portada si se puede decodificar.</summary>
    public void SetCover(byte[] bytes)
    {
        if (!ImageBytes.TryDecode(bytes, out var image))
        {
            return;
        }

        _cover.Texture = ImageTexture.CreateFromImage(image);
        _cover.Show();
        _initialPanel.Hide();
    }

    private static string Initial(string name) =>
        name.Length == 0 ? "?" : char.ToUpperInvariant(name[0]).ToString();

    private static StyleBoxFlat Frame(Color background, Color border, int radius = 8) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 6,
        ContentMarginBottom = 6,
    };

    private static void IgnoreMouse(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
        }

        foreach (var child in node.GetChildren())
        {
            IgnoreMouse(child);
        }
    }
}
