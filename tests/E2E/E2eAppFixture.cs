using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Entorno E2E (Épica H, ADR-025): arranca la API real (SQLite temporal),
/// la web construida (`vite preview` con proxy a la API de las pruebas) y
/// Chromium con Playwright. El objetivo es el vertical slice completo con
/// usuarios reales: dos clientes moviendo un token en una escena
/// compartida. Los datos de preparación (membresías, escena) se crean por
/// API; toda la interacción probada es por UI.
/// </summary>
public sealed class E2eAppFixture : IAsyncLifetime
{
    public const string WebUrl = "http://127.0.0.1:4173";
    public const string ApiUrl = "http://127.0.0.1:5099";

    private Process _api = null!;
    private Process _web = null!;
    private string _databasePath = null!;
    private string _repoRoot = null!;

    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public HttpClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _repoRoot = FindRepoRoot();
        _databasePath = Path.Combine(
            Path.GetTempPath(), $"projectvtt-e2e-{Guid.NewGuid():N}.db");

        // Navegadores de Playwright (descarga solo la primera vez).
        var installExit = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (installExit != 0)
        {
            throw new InvalidOperationException(
                "No se pudieron instalar los navegadores de Playwright.");
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new() { Headless = true });
        Api = new HttpClient { BaseAddress = new Uri(ApiUrl) };

        // Web: build actual y `vite preview` apuntando el proxy a la API.
        await RunAsync("npm", "run build", Path.Combine(_repoRoot, "apps", "web"));
        _web = StartProcess(
            "node",
            "node_modules/vite/bin/vite.js preview --host 127.0.0.1 --port 4173 --strictPort",
            Path.Combine(_repoRoot, "apps", "web"),
            new() { ["VITE_API_TARGET"] = ApiUrl });

        // API real sobre SQLite temporal (la misma del vertical slice).
        _api = StartProcess(
            "dotnet",
            "run --project backend/Api --no-launch-profile",
            _repoRoot,
            new()
            {
                ["Database__Provider"] = "sqlite",
                ["ConnectionStrings__Default"] = $"Data Source={_databasePath}",
                ["ASPNETCORE_URLS"] = ApiUrl,
            });

        await WaitUntilAsync(
            async () => (await Api.GetAsync("/health")).IsSuccessStatusCode,
            "la API de las pruebas");
        await WaitUntilAsync(
            async () => (await Api.GetAsync(WebUrl + "/")).IsSuccessStatusCode,
            "la web de las pruebas");
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
        Api.Dispose();
        Stop(_web);
        Stop(_api);
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // El archivo puede seguir liberándose: no es crítico.
        }
    }

    // --- Preparación por API ---------------------------------------------

    /// <summary>Registra un usuario y devuelve su token de sesión.</summary>
    public async Task<string> RegisterAsync(string userName, string password)
    {
        using var response = await Api.PostAsJsonAsync(
            "/api/v1/auth/register", new { userName, password });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("El registro no devolvió token.");
    }

    /// <summary>Crea una campaña y devuelve su id.</summary>
    public async Task<string> CreateCampaignAsync(string token, string name)
    {
        using var response = await SendAsync(
            "/api/v1/campaigns", token, HttpMethod.Post, new { name });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    /// <summary>Une a <paramref name="joinerToken"/> como Player vía invitación.</summary>
    public async Task JoinAsync(string ownerToken, string joinerToken, string campaignId)
    {
        using var invite = await SendAsync(
            $"/api/v1/campaigns/{campaignId}/invites", ownerToken, HttpMethod.Post,
            new { role = (string?)null });
        invite.EnsureSuccessStatusCode();
        var inviteJson = await invite.Content.ReadFromJsonAsync<JsonElement>();
        var code = inviteJson.GetProperty("code").GetString()!;

        using var redeem = await SendAsync(
            $"/api/v1/invites/{code}/redeem", joinerToken, HttpMethod.Post);
        redeem.EnsureSuccessStatusCode();
    }

    /// <summary>Crea una escena y devuelve su id.</summary>
    public async Task<string> CreateSceneAsync(string token, string campaignId, string name)
    {
        using var response = await SendAsync(
            $"/api/v1/campaigns/{campaignId}/scenes", token, HttpMethod.Post, new { name });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    /// <summary>Crea un token en una posición dada y devuelve su id.</summary>
    public async Task<string> CreateTokenAsync(
        string token, string sceneId, string name, double x, double y)
    {
        using var response = await SendAsync(
            $"/api/v1/scenes/{sceneId}/tokens", token, HttpMethod.Post, new { name, x, y });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    /// <summary>Lista los tokens de una escena (para aserciones del servidor).</summary>
    public async Task<JsonElement> ListTokensAsync(string token, string sceneId)
    {
        using var response = await SendAsync(
            $"/api/v1/scenes/{sceneId}/tokens", token, HttpMethod.Get);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Petición autenticada por token de sesión.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        string path, string token, HttpMethod method, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new("Bearer", token);
        return await Api.SendAsync(request);
    }

    // --- UI --------------------------------------------------------------

    /// <summary>Abre la app y hace login por el formulario.</summary>
    public async Task<IPage> LoginAsync(string userName, string password)
    {
        var page = await Browser.NewPageAsync();
        // El tour guiado es una ayuda de primer ingreso: en E2E se marca como
        // visto ANTES de cargar la app para que su overlay no interpete clics.
        // El tour se cubre por sus propias pruebas unitarias y capturas.
        await page.AddInitScriptAsync(
            "(function () { try { localStorage.setItem('pv-tour-done', '1') } catch (e) {} })()");
        await page.GotoAsync(WebUrl);
        await page.GetByLabel("Nombre de usuario").FillAsync(userName);
        await page.GetByLabel("Contraseña").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Entrar" }).ClickAsync();
        // La lista de campañas (PMB-2.4) se identifica por su encabezado.
        // Exact=true: el tour guiado añade un h2 "Tus campañas" que, sin
        // coincidencia exacta, haría ambigua esta espera (strict mode).
        await page.GetByRole(AriaRole.Heading, new() { Name = "Campañas", Exact = true }).WaitForAsync();
        return page;
    }

    /// <summary>Revela el raíl oculto antes de pulsar uno de sus destinos.</summary>
    public async Task OpenRailAsync(IPage page)
    {
        var edge = page.GetByRole(AriaRole.Button, new() { Name = "Mostrar navegación" });
        if (await edge.CountAsync() > 0)
        {
            await edge.ClickAsync();
        }
    }

    /// <summary>Desde la lista de campañas, abre una campaña y su escena.</summary>
    public async Task OpenSceneAsync(IPage page, string campaignLabel, string sceneName)
    {
        // Fila de campaña (div con rol botón): su texto incluye el nombre.
        await page.GetByRole(AriaRole.Button).Filter(new() { HasText = campaignLabel })
            .First.ClickAsync();
        await page.GetByRole(AriaRole.Navigation, new() { Name = "Secciones de la campaña" })
            .GetByRole(AriaRole.Button, new() { Name = "Mesa de juego" }).ClickAsync();
        await page.Locator("button", new() { HasText = sceneName }).First.ClickAsync();
        // El canvas aparece al terminar de inicializar PixiJS y lo ven
        // todos los roles (el botón "Añadir token" es solo GM+).
        await page.Locator("canvas").WaitForAsync();
    }

    /// <summary>Espera a que una condición asíncrona se cumpla (o falla).</summary>
    public async Task WaitUntilAsync(Func<Task<bool>> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                {
                    return;
                }
            }
            catch
            {
                // El servicio puede estar aún arrancando.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Se agotó la espera de {description}.");
    }

    /// <summary>
    /// Guarda captura y HTML de la página para diagnosticar fallos;
    /// devuelve la ruta para añadirla al mensaje de error.
    /// </summary>
    public async Task<string> CaptureAsync(IPage page, string label)
    {
        var directory = Path.Combine(Path.GetTempPath(), "projectvtt-e2e");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{label}-{DateTime.UtcNow:HHmmss}");
        await page.ScreenshotAsync(new() { Path = path + ".png", FullPage = true });
        await File.WriteAllTextAsync(path + ".html", await page.ContentAsync());
        return path;
    }

    // --- Utilidades ------------------------------------------------------

    private static Process StartProcess(
        string fileName, string arguments, string workingDirectory,
        Dictionary<string, string> environment)
    {
        // En Windows `npm` es un .cmd: se ejecuta vía cmd /c (y el árbol
        // completo se mata al terminar).
        if (OperatingSystem.IsWindows())
        {
            arguments = $"/c {fileName} {arguments}";
            fileName = "cmd.exe";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"No se pudo arrancar {fileName}.");

        // Drenar las salidas siempre: sin lector, el relleno del buffer
        // bloquearía procesos verbosos como la API con logs JSON.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task RunAsync(string fileName, string arguments, string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            arguments = $"/c {fileName} {arguments}";
            fileName = "cmd.exe";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"No se pudo arrancar {fileName}.");
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, args) => output.AppendLine(args.Data);
        process.ErrorDataReceived += (_, args) => output.AppendLine(args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Falló '{fileName} {arguments}' (exit {process.ExitCode}): {output}");
        }
    }

    private static void Stop(Process? process)
    {
        if (process is { HasExited: false })
        {
            process.Kill(entireProcessTree: true);
        }

        process?.Dispose();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(
                Path.Combine(directory.FullName, "PROJECT_SPEC.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
