using Godot;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Chip de token arrastrable dentro del tablero. El arrastre es en
/// coordenadas de píxel (el tablero escala posiciones, no nodos) y al
/// soltar se avisa con el centro del chip en coordenadas de tablero.
/// </summary>
public partial class TokenChip : Panel
{
    public const float ChipSize = 44f;

    private bool _dragging;

    /// <summary>Token representado (posiciones en unidades de mundo).</summary>
    public State.TokenView Token { get; }

    /// <summary>Soltó el token tras arrastrarlo (centro en coords de tablero).</summary>
    public event Action<TokenChip, Vector2>? Dropped;

    /// <summary>Se está arrastrando (para no repintarlo con eventos ajenos).</summary>
    public bool IsDragging => _dragging;

    public TokenChip(State.TokenView token)
    {
        Token = token;
        CustomMinimumSize = new Vector2(ChipSize, ChipSize);
        Size = new Vector2(ChipSize, ChipSize);
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = ColorFor(token.Id),
            CornerRadiusTopLeft = 22,
            CornerRadiusTopRight = 22,
            CornerRadiusBottomLeft = 22,
            CornerRadiusBottomRight = 22,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderColor = DesktopTheme.Ink0,
        };
        AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = token.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // El tema usa pergamino claro: sobre el chip pastel hay que volver
        // a tinta para que el nombre siga siendo legible.
        label.AddThemeColorOverride("font_color", DesktopTheme.Ink0);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(label);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _dragging = button.Pressed;
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseMotion motion && _dragging)
        {
            Position += motion.Relative;
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // El soltar puede llegar fuera del chip si el ratón sale de su rect.
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } && _dragging)
        {
            _dragging = false;
            Dropped?.Invoke(this, Position + Size / 2);
        }
    }

    /// <summary>Colorea el chip a partir del id (estable entre sesiones).</summary>
    private static Color ColorFor(Guid id)
    {
        var hue = (Math.Abs(id.GetHashCode()) % 360) / 360f;
        return Color.FromHsv(hue, 0.5f, 0.85f);
    }
}
