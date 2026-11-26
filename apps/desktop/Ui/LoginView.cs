using Godot;
using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Acceso al servidor: entrar con cuenta existente o crear una nueva
/// (mismos endpoints que la web: `/auth/login` y `/auth/register`).
/// </summary>
public partial class LoginView : Control
{
    private readonly ApiClient _api;
    private readonly Action<AuthResponse> _onSignedIn;

    private LineEdit _user = null!;
    private LineEdit _password = null!;
    private Label _status = null!;
    private Button _loginButton = null!;
    private Button _registerButton = null!;

    public LoginView(ApiClient api, Action<AuthResponse> onSignedIn)
    {
        _api = api;
        _onSignedIn = onSignedIn;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        center.AddChild(box);

        var title = DesktopTheme.Title("ProjectVTT", 34);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        var subtitle = DesktopTheme.Secondary("Cliente de escritorio (vertical slice)");
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(subtitle);

        box.AddChild(new Label { Text = $"Servidor: {_api.BaseUrl}" });

        _user = new LineEdit { PlaceholderText = "Usuario (3-32 caracteres)", MaxLength = 32 };
        _user.CustomMinimumSize = new Vector2(320, 0);
        box.AddChild(_user);

        _password = new LineEdit { PlaceholderText = "Contraseña (8-128 caracteres)", Secret = true };
        _password.CustomMinimumSize = new Vector2(320, 0);
        box.AddChild(_password);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        box.AddChild(buttons);

        _loginButton = new Button { Text = "Entrar" };
        _loginButton.Pressed += () => _ = SubmitAsync(login: true);
        buttons.AddChild(_loginButton);

        _registerButton = new Button { Text = "Crear cuenta" };
        _registerButton.Pressed += () => _ = SubmitAsync(login: false);
        buttons.AddChild(_registerButton);

        _status = new Label { Text = string.Empty };
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.CustomMinimumSize = new Vector2(320, 0);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_status);
    }

    private async Task SubmitAsync(bool login)
    {
        var userName = _user.Text.Trim();
        var password = _password.Text;
        if (userName.Length < 3 || password.Length < 8)
        {
            _status.Text = "Usuario de 3+ caracteres y contraseña de 8+ caracteres.";
            return;
        }

        SetBusy(true);
        _status.Text = "Conectando…";
        try
        {
            var response = login
                ? await _api.LoginAsync(userName, password)
                : await _api.RegisterAsync(userName, password);
            MainThread.Run(() =>
            {
                _status.Text = string.Empty;
                _onSignedIn(response);
            });
        }
        catch (Exception exception)
        {
            var message = exception is ApiException apiException
                ? apiException.Message
                : "No se pudo contactar con el servidor.";
            MainThread.Run(() =>
            {
                SetBusy(false);
                _status.Text = message;
            });
        }
    }

    private void SetBusy(bool busy)
    {
        _loginButton.Disabled = busy;
        _registerButton.Disabled = busy;
    }
}
