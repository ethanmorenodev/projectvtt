using Godot;
using ProjectVTT.Desktop.Api;
using ProjectVTT.Desktop.State;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Escena en juego: mapa compartido (ADR-028), tokens arrastrables y
/// movimiento en vivo por el hub (`MoveToken`/`TokenMoved`, ADR-010) con
/// predicción local, snap al grid (ADR-023) y resincronización ante
/// conflictos. Misma semántica que `apps/web/src/views/SceneView.tsx`.
/// </summary>
public partial class SceneView : Control
{
    private const float HeaderHeight = 110f;

    private readonly ApiClient _api;
    private readonly SceneConnection _connection;
    private readonly Guid _sceneId;
    private readonly string _sceneName;
    private readonly Action _onBack;
    private readonly TokenStore _store = new();
    private readonly Dictionary<Guid, TokenChip> _chips = new();

    private Label _status = null!;
    private ItemList _annotationList = null!;
    private Label _annotationStatus = null!;
    private CheckBox _snap = null!;
    private Control _boardArea = null!;
    private Control _board = null!;
    private TextureRect? _mapRect;
    private ColorRect? _backgroundRect;

    /// <summary>Datos del grid de la escena (tras la carga).</summary>
    private int _gridCellSize = 64;
    private string _gridShape = GridSnap.Square;
    private float _scale = 1f;
    private Vector2 _boardSize = new(800, 600);
    private int _tokenCounter;

    public SceneView(
        ApiClient api,
        SceneConnection connection,
        Guid sceneId,
        string sceneName,
        Action onBack)
    {
        _api = api;
        _connection = connection;
        _sceneId = sceneId;
        _sceneName = sceneName;
        _onBack = onBack;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 6);
        AddChild(box);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        box.AddChild(header);

        var back = new Button { Text = "← Escenas" };
        back.Pressed += () => _onBack();
        header.AddChild(back);

        header.AddChild(DesktopTheme.Title(_sceneName, 24));

        _snap = new CheckBox { Text = "Ajustar al grid", ButtonPressed = true };
        header.AddChild(_snap);

        var createToken = new Button { Text = "Crear token" };
        createToken.Pressed += () => _ = CreateTokenAsync();
        header.AddChild(createToken);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        _boardArea = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(_boardArea);

        // Anotaciones compartidas (ADR-043): solo lectura. Se cargan al abrir
        // la escena y se refrescan con el evento del hub cuando llega.
        var notes = new PanelContainer { CustomMinimumSize = new Vector2(0, 120) };
        var notesBox = new VBoxContainer();
        notesBox.AddThemeConstantOverride("separation", 4);
        notes.AddChild(notesBox);
        notesBox.AddChild(DesktopTheme.Secondary("Anotaciones de escena (solo lectura)"));
        _annotationList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        notesBox.AddChild(_annotationList);
        _annotationStatus = new Label { Text = "Cargando anotaciones…" };
        notesBox.AddChild(_annotationStatus);
        box.AddChild(notes);

        _status = new Label { Text = "Cargando escena…" };
        box.AddChild(_status);

        _board = new Control();
        _boardArea.AddChild(_board);

        _connection.TokenMoved += OnTokenMoved;
        _connection.TokenCreated += OnTokenCreated;
        _connection.MapUpdated += OnMapUpdated;
        _connection.SceneAnnotationsUpdated += OnSceneAnnotationsUpdated;
        _connection.StatusChanged += OnConnectionStatus;

        _ = LoadAsync();
        _ = LoadAnnotationsAsync();
    }

    public override void _ExitTree()
    {
        _connection.TokenMoved -= OnTokenMoved;
        _connection.TokenCreated -= OnTokenCreated;
        _connection.MapUpdated -= OnMapUpdated;
        _connection.SceneAnnotationsUpdated -= OnSceneAnnotationsUpdated;
        _connection.StatusChanged -= OnConnectionStatus;
    }

    // --- Carga y sincronización -----------------------------------------

    private async Task LoadAsync()
    {
        try
        {
            var scene = await _api.GetSceneAsync(_sceneId);
            var state = await _connection.SyncSceneAsync(_sceneId);
            var map = await _api.GetMapAsync(_sceneId);
            MainThread.Run(() => ApplyLoaded(scene, state, map));
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    /// <summary>
    /// HTTP es la fuente de verdad para la carga inicial y la reconexión;
    /// el evento del hub solo refresca la lista ya abierta (ADR-043).
    /// </summary>
    private async Task LoadAnnotationsAsync()
    {
        try
        {
            var state = await _api.GetSceneAnnotationsAsync(_sceneId);
            MainThread.Run(() => FillAnnotations(state));
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _annotationStatus.Text = $"Anotaciones: {Describe(exception)}");
        }
    }

    private void FillAnnotations(SceneAnnotationsStateDto state)
    {
        _annotationList.Clear();
        foreach (var annotation in state.Items)
        {
            var text = string.IsNullOrWhiteSpace(annotation.Text) ? "—" : annotation.Text;
            _annotationList.AddItem(
                $"{annotation.Kind} · {text.Replace('\n', ' ')} · {annotation.Points.Count} pts");
        }

        _annotationStatus.Text = $"{state.Items.Count} anotación(es) · v{state.Version}";
    }

    private async Task ResyncAsync()
    {
        try
        {
            var state = await _connection.SyncSceneAsync(_sceneId);
            MainThread.Run(() =>
            {
                _store.ApplySync(state.Tokens);
                ApplyStoreToChips();
                _status.Text = "Estado resincronizado con el servidor.";
            });
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    private void ApplyLoaded(
        SceneDetail scene,
        SceneStateDto state,
        (byte[] Bytes, string ContentType)? map)
    {
        _gridCellSize = scene.GridCellSize;
        _gridShape = scene.GridShape;

        var world = map is null ? new Vector2(800, 600) : MeasureImage(map.Value.Bytes);
        var area = GetViewportRect().Size - new Vector2(0, HeaderHeight);
        _scale = Mathf.Min(1f, Mathf.Min(area.X / world.X, area.Y / world.Y));
        _boardSize = world * _scale;
        _board.Size = _boardSize;

        if (map is not null)
        {
            ShowMap(map.Value.Bytes);
        }
        else
        {
            ShowEmptyBoard();
        }

        _store.ApplySync(state.Tokens);
        ApplyStoreToChips();
        _status.Text =
            $"Grid {_gridShape} de {_gridCellSize}px · {_store.Tokens.Count} tokens · " +
            (map is null ? "sin mapa (sube uno desde la web)" : "mapa cargado");
    }

    // --- Mapa -----------------------------------------------------------

    private void ShowMap(byte[] bytes)
    {
        if (!ImageBytes.TryDecode(bytes, out var image))
        {
            ShowEmptyBoard();
            _status.Text = "El mapa no se pudo decodificar (se admiten png, jpeg y webp).";
            return;
        }

        ClearBoardBackground();
        _mapRect?.QueueFree();
        _mapRect = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(image),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        _mapRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _board.AddChild(_mapRect);
        _board.MoveChild(_mapRect, 0);
    }

    private void ShowEmptyBoard()
    {
        ClearBoardBackground();
        _backgroundRect = new ColorRect { Color = DesktopTheme.Ink0 };
        _backgroundRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _board.AddChild(_backgroundRect);
        _board.MoveChild(_backgroundRect, 0);
    }

    /// <summary>Deja el tablero sin mapa ni fondo (los chips quedan).</summary>
    private void ClearBoardBackground()
    {
        _mapRect?.QueueFree();
        _mapRect = null;
        _backgroundRect?.QueueFree();
        _backgroundRect = null;
    }

    private async Task ReloadMapAsync()
    {
        try
        {
            var map = await _api.GetMapAsync(_sceneId);
            MainThread.Run(() =>
            {
                if (map is null)
                {
                    ShowEmptyBoard();
                }
                else
                {
                    ShowMap(map.Value.Bytes);
                }
            });
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    private static Vector2 MeasureImage(byte[] bytes) =>
        ImageBytes.TryDecode(bytes, out var image)
            ? new Vector2(image.GetWidth(), image.GetHeight())
            : new Vector2(800, 600);

    // --- Tokens ---------------------------------------------------------

    private async Task CreateTokenAsync()
    {
        var world = _boardSize / (2 * _scale);
        var point = Snap(world.X, world.Y);
        try
        {
            var token = await _api.CreateTokenAsync(
                _sceneId, $"Token {++_tokenCounter}", point.X, point.Y);
            MainThread.Run(() =>
            {
                _store.Upsert(token);
                ApplyStoreToChips();
                _status.Text = $"Token '{token.Name}' creado.";
            });
        }
        catch (Exception exception)
        {
            MainThread.Run(() => _status.Text = Describe(exception));
        }
    }

    private void OnChipDropped(TokenChip chip, Vector2 boardCenter)
    {
        var world = boardCenter / _scale;
        var point = _snap.ButtonPressed ? Snap(world.X, world.Y) : world;
        chip.Position = point * _scale - chip.Size / 2;

        var expectedVersion = chip.Token.Version;
        _store.ApplyLocalMove(chip.Token.Id, point.X, point.Y, expectedVersion);
        _ = MoveAsync(new MoveTokenCommand(
            Guid.NewGuid(), _sceneId, chip.Token.Id, expectedVersion, point.X, point.Y));
    }

    private async Task MoveAsync(MoveTokenCommand command)
    {
        try
        {
            var result = await _connection.MoveTokenAsync(command);
            MainThread.Run(() =>
            {
                _store.ApplyMoveResult(result);
                ApplyStoreToChips();
                _status.Text = "Movimiento confirmado.";
            });
        }
        catch (Exception exception)
        {
            // Conflicto de versión o rechazo: manda el estado del servidor.
            var message = exception.Message;
            MainThread.Run(() => _status.Text = message);
            await ResyncAsync();
        }
    }

    private void ApplyStoreToChips()
    {
        var live = new HashSet<Guid>();
        foreach (var token in _store.Tokens)
        {
            live.Add(token.Id);
            if (!_chips.TryGetValue(token.Id, out var chip))
            {
                chip = new TokenChip(token);
                chip.Dropped += OnChipDropped;
                _chips[token.Id] = chip;
                _board.AddChild(chip);
            }

            if (!chip.IsDragging)
            {
                chip.Position = new Vector2((float)(token.X * _scale), (float)(token.Y * _scale))
                    - chip.Size / 2;
            }
        }

        foreach (var (id, chip) in _chips.Where(pair => !live.Contains(pair.Key)).ToList())
        {
            chip.Dropped -= OnChipDropped;
            chip.QueueFree();
            _chips.Remove(id);
        }
    }

    // --- Eventos en vivo (llegan en hilos ajenos) ------------------------

    private void OnTokenMoved(TokenMovedEvent e) => MainThread.Run(() =>
    {
        if (e.SceneId == _sceneId && _store.ApplyTokenMoved(e))
        {
            ApplyStoreToChips();
        }
    });

    private void OnTokenCreated(TokenCreatedEvent e) => MainThread.Run(() =>
    {
        if (e.SceneId == _sceneId && _store.Upsert(e.Token))
        {
            ApplyStoreToChips();
        }
    });

    private void OnMapUpdated(MapUpdatedEvent e) => MainThread.Run(() =>
    {
        if (e.SceneId == _sceneId)
        {
            _ = ReloadMapAsync();
        }
    });

    private void OnSceneAnnotationsUpdated(SceneAnnotationsUpdatedEvent e) => MainThread.Run(() =>
    {
        if (e.Annotations.SceneId == _sceneId)
        {
            FillAnnotations(e.Annotations);
        }
    });

    private void OnConnectionStatus(string status) => MainThread.Run(() =>
    {
        _status.Text = status == "conectado"
            ? "Conexión en vivo restablecida."
            : "Conexión en vivo perdida; reintentando…";
        if (status == "conectado")
        {
            _ = ResyncAsync();
        }
    });

    // --- Utilidades -----------------------------------------------------

    private Vector2 Snap(double x, double y) =>
        GridSnap.Snap(x, y, _gridCellSize, _gridShape);

    private static string Describe(Exception exception) =>
        exception is ApiException apiException
            ? apiException.Message
            : "No se pudo contactar con el servidor.";
}
