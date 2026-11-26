using Godot;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Identidad visual «Tinta y ámbar» del escritorio (rebanada 1 de la
/// reapertura de ADR-046). Los valores son los mismos que la referencia
/// web (`apps/web/src/theme/tokens.css`, variables `--pv-*`), trasladados
/// a un `Theme` de Godot construido en código: la UI del escritorio es
/// programática y así no hay un archivo `.tres` que se pueda desincronizar.
///
/// Tipografía: no hay assets `.ttf`/`.otf` en el repositorio, así que se
/// usan fuentes del sistema (`SystemFont`) con el mismo orden de fallback
/// que la web. Si el sistema no las tiene, Godot usa su fuente por defecto.
/// </summary>
public static class DesktopTheme
{
    public static readonly Color Ink0 = Color.FromHtml("#0f1113");
    public static readonly Color Ink1 = Color.FromHtml("#191c20");
    public static readonly Color Ink2 = Color.FromHtml("#22262b");
    public static readonly Color Ink3 = Color.FromHtml("#2b3036");
    public static readonly Color Border = Color.FromHtml("#363b42");
    public static readonly Color BorderStrong = Color.FromHtml("#50565e");
    public static readonly Color Parchment = Color.FromHtml("#f5f1e8");
    public static readonly Color ParchmentDim = Color.FromHtml("#cdc6b8");
    public static readonly Color Muted = Color.FromHtml("#b0a89b");
    public static readonly Color Amber = Color.FromHtml("#d9a566");
    public static readonly Color AmberHover = Color.FromHtml("#e2b67e");
    public static readonly Color AmberActive = Color.FromHtml("#c28f4e");
    public static readonly Color AmberSoft = Color.FromHtml("#3a2c1c");
    public static readonly Color AmberOn = Color.FromHtml("#1a140d");
    public static readonly Color AmberBorder = Color.FromHtml("#8a6738");

    /// <summary>Tema completo; se aplica una vez a la raíz de las vistas.</summary>
    public static Theme Build()
    {
        var theme = new Theme();
        theme.DefaultFont = FontFrom(UiFonts());
        theme.DefaultFontSize = 13;

        theme.SetColor("font_color", "Label", Parchment);
        theme.SetFont("font", "Title", FontFrom(DisplayFonts()));
        theme.SetFontSize("font_size", "Title", 28);
        theme.SetColor("font_color", "Title", Parchment);
        theme.SetTypeVariation("Title", "Label");

        theme.SetColor("font_color", "Button", Parchment);
        theme.SetColor("font_hover_color", "Button", AmberHover);
        theme.SetColor("font_pressed_color", "Button", AmberOn);
        theme.SetColor("font_disabled_color", "Button", Muted);
        theme.SetStylebox("normal", "Button", Box(Ink2, Border));
        theme.SetStylebox("hover", "Button", Box(AmberSoft, AmberBorder));
        theme.SetStylebox("pressed", "Button", Box(Amber, Amber));
        theme.SetStylebox("disabled", "Button", Box(Ink1, Border));
        theme.SetStylebox("focus", "Button", Outline(Amber));

        theme.SetColor("font_color", "LineEdit", Parchment);
        theme.SetColor("caret_color", "LineEdit", Amber);
        theme.SetColor("selection_color", "LineEdit", AmberSoft);
        theme.SetStylebox("normal", "LineEdit", Box(Ink0, Border, 6, 1, 10, 6));
        theme.SetStylebox("focus", "LineEdit", Outline(Amber));

        var panel = Box(Ink1, Border, 8, 1, 10, 8);
        theme.SetStylebox("panel", "Panel", panel);
        theme.SetStylebox("panel", "PanelContainer", panel);

        theme.SetColor("font_color", "CheckBox", Parchment);
        theme.SetStylebox("panel", "ItemList", Box(Ink0, Border, 6, 1, 4, 4));
        theme.SetStylebox("selected", "ItemList", Box(AmberSoft, AmberBorder, 4, 1, 4, 2));
        theme.SetStylebox("hovered", "ItemList", Box(Ink3, Border, 4, 1, 4, 2));
        theme.SetColor("font_color", "ItemList", Parchment);
        theme.SetColor("font_selected_color", "ItemList", Amber);
        theme.SetColor("font_hovered_color", "ItemList", AmberHover);

        return theme;
    }

    /// <summary>Título con tipografía de display (serif del sistema).</summary>
    public static Label Title(string text, int size = 28)
    {
        var label = new Label { Text = text, ThemeTypeVariation = "Title" };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    /// <summary>Texto secundario, en pergamino apagado.</summary>
    public static Label Secondary(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", Muted);
        return label;
    }

    private static string[] UiFonts() => ["Segoe UI", "Arial", "sans-serif"];

    private static string[] DisplayFonts() => ["Georgia", "Times New Roman", "serif"];

    private static SystemFont FontFrom(string[] names) => new() { FontNames = [.. names] };

    private static StyleBoxFlat Box(
        Color background, Color border, int radius = 6, int borderWidth = 1,
        int marginX = 10, int marginY = 6) => new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = marginX,
            ContentMarginRight = marginX,
            ContentMarginTop = marginY,
            ContentMarginBottom = marginY,
        };

    /// <summary>Anillo de foco ámbar sin ocultar el estado normal del control.</summary>
    private static StyleBoxFlat Outline(Color border) => new()
    {
        BgColor = Colors.Transparent,
        BorderColor = border,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };
}
