using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Performance;

/// <summary>
/// Entorno de pruebas de rendimiento (Épica H, ADR-027): arranca la API
/// real (SQLite temporal) sin navegador y expone helpers de preparación
/// y medición. Los umbrales de latencia son deliberadamente holgados: lo
/// importante es detectar regresiones de órdenes de magnitud, no el ruido
/// de máquinas distintas.
/// </summary>
public sealed class PerfApiFixture : IAsyncLifetime
{
    public const string ApiUrl = "http://127.0.0.1:5098";

    private Process _api = null!;
    private string _databasePath = null!;

    public HttpClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(), $"projectvtt-perf-{Guid.NewGuid():N}.db");
        Api = new HttpClient { BaseAddress = new Uri(ApiUrl) };

        var startInfo = new ProcessStartInfo
        {
            // En Windows `dotnet` no necesita wrapper; el árbol se mata al
            // terminar y las salidas se drenan para no bloquear la API.
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "dotnet",
            Arguments = OperatingSystem.IsWindows()
                ? "/c dotnet run --project backend/Api --no-launch-profile"
                : "run --project backend/Api --no-launch-profile",
            WorkingDirectory = FindRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["Database__Provider"] = "sqlite";
        startInfo.Environment["ConnectionStrings__Default"] = $"Data Source={_databasePath}";
        startInfo.Environment["ASPNETCORE_URLS"] = ApiUrl;
        // Igual que ApiTestFactory: las pruebas necesitan crear tipos por HTTP
        // (en producción la allowlist de operadores está vacía por defecto).
        startInfo.Environment["Content__OperatorUsers"] = "*";
        _api = Process.Start(startInfo)
            ?? throw new InvalidOperationException("No se pudo arrancar la API.");
        _api.OutputDataReceived += (_, _) => { };
        _api.ErrorDataReceived += (_, _) => { };
        _api.BeginOutputReadLine();
        _api.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await Api.GetAsync("/health")).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // La API puede estar aún arrancando.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("La API de rendimiento no arrancó.");
    }

    public Task DisposeAsync()
    {
        if (_api is { HasExited: false })
        {
            _api.Kill(entireProcessTree: true);
        }

        _api.Dispose();
        Api.Dispose();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // El archivo puede seguir liberándose: no es crítico.
        }

        return Task.CompletedTask;
    }

    // --- Preparación por API ---------------------------------------------

    /// <summary>
    /// Registra un usuario y devuelve su token; si otra prueba de la
    /// clase ya lo registró (409), hace login con las mismas credenciales.
    /// </summary>
    public async Task<string> RegisterAsync(string userName, string password)
    {
        using var response = await Api.PostAsJsonAsync(
            "/api/v1/auth/register", new { userName, password });
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            using var login = await Api.PostAsJsonAsync(
                "/api/v1/auth/login", new { userName, password });
            login.EnsureSuccessStatusCode();
            var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();
            return loginJson.GetProperty("token").GetString()!;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

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

    public async Task<string> CreateSceneAsync(string token, string campaignId, string name)
    {
        using var response = await SendAsync(
            $"/api/v1/campaigns/{campaignId}/scenes", token, HttpMethod.Post, new { name });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    public async Task<string> CreateActorAsync(string token, string campaignId, string name)
    {
        using var response = await SendAsync(
            $"/api/v1/campaigns/{campaignId}/actors", token, HttpMethod.Post, new
            {
                name,
                rulesetKey = "dnd5e",
                attributes = new Dictionary<string, int>
                {
                    ["strength"] = 10,
                    ["dexterity"] = 10,
                    ["constitution"] = 10,
                    ["intelligence"] = 10,
                    ["wisdom"] = 10,
                    ["charisma"] = 10,
                },
                resources = new Dictionary<string, object>
                {
                    ["hitPoints"] = new { current = 10, max = 10 },
                },
            });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

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

    // --- Medición ---------------------------------------------------------

    /// <summary>
    /// Ejecuta <paramref name="total"/> operaciones con el paralelismo
    /// indicado y devuelve las duraciones en milisegundos; lanza si alguna
    /// operación no fue exitosa. Antes se calienta la ruta medida: sin
    /// calentamiento, el JIT y el primer acceso a EF contaminarían el p95.
    /// </summary>
    public static async Task<List<double>> MeasureAsync(
        int total, int parallelism, Func<int, Task<HttpResponseMessage>> operation)
    {
        // Calentamiento (fuera de medición).
        for (var i = 0; i < Math.Min(10, total); i++)
        {
            using var warmup = await operation(i);
            if (!warmup.IsSuccessStatusCode)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Calentamiento fallido: {(int)warmup.StatusCode} en " +
                    $"{warmup.RequestMessage?.RequestUri} → {await warmup.Content.ReadAsStringAsync()}");
            }
        }

        var durations = new double[total];
        using var gate = new SemaphoreSlim(parallelism);
        var failures = new List<string>();
        var tasks = Enumerable.Range(0, total).Select(async index =>
        {
            await gate.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using var response = await operation(index);
                stopwatch.Stop();
                durations[index] = stopwatch.Elapsed.TotalMilliseconds;
                if (!response.IsSuccessStatusCode)
                {
                    lock (failures)
                    {
                        failures.Add($"#{index}: {(int)response.StatusCode}");
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);

        if (failures.Count > 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"Operaciones fallidas ({failures.Count}): {string.Join(", ", failures.Take(5))}");
        }

        return durations.ToList();
    }

    /// <summary>Percentil (0..1) de una lista de duraciones en ms.</summary>
    public static double Percentile(IReadOnlyList<double> durations, double percentile)
    {
        var sorted = durations.OrderBy(value => value).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
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
