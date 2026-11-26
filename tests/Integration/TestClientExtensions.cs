using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ProjectVTT.Tests.Integration;

/// <summary>Utilidades para las pruebas de integración de la API.</summary>
public static class TestClientExtensions
{
    public const string DefaultPassword = "ValidPass!123";

    /// <summary>Nombre de usuario único para cada prueba.</summary>
    public static string NewUserName(string prefix = "user") =>
        $"{prefix}{Guid.NewGuid():N}"[..20];

    /// <summary>Registra un usuario y devuelve su token de sesión.</summary>
    public static async Task<string> RegisterUserAsync(
        this HttpClient client, string userName, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { userName, password });
        var json = await ReadAsync(response);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"Registro falló con {(int)response.StatusCode}: {json}");
        }

        return json.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("La respuesta no incluye token.");
    }

    /// <summary>Crea un cliente con el token de un usuario ya registrado.</summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        this ApiTestFactory factory, string? userName = null)
    {
        var client = factory.CreateClient();
        var token = await client.RegisterUserAsync(userName ?? NewUserName());
        client.SetBearerToken(token);
        return client;
    }

    public static void SetBearerToken(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static Task<JsonElement> ReadAsync(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>
    /// Comprueba un código de estado y devuelve el cuerpo JSON (las
    /// respuestas sin cuerpo, p. ej. 204, devuelven JSON indefinido).
    /// </summary>
    public static async Task<JsonElement> EnsureStatusAsync(
        this HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(text);
        if (response.StatusCode != expected)
        {
            throw new InvalidOperationException(
                $"Se esperaba {(int)expected} pero llegó {(int)response.StatusCode}: {text}");
        }

        return json;
    }

    /// <summary>
    /// Conecta un cliente SignalR al TestServer en memoria: se reutiliza el
    /// handler del servidor de pruebas (por defecto el cliente abriría una
    /// conexión TCP real a localhost) y se usa LongPolling, porque el
    /// handshake WebSocket no puede enrutarse por el handler en memoria
    /// (ver ADR-010).
    /// </summary>
    public static async Task<HubConnection> ConnectHubAsync(
        this ApiTestFactory factory, HttpClient client)
    {
        var token = client.DefaultRequestHeaders.Authorization?.Parameter
            ?? throw new InvalidOperationException("El cliente no tiene token Bearer.");

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/campaign"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
