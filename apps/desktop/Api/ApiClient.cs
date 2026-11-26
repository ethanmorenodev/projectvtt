using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ProjectVTT.Desktop.Api;

/// <summary>
/// Cliente HTTP de la API común (los mismos endpoints que consume la web).
/// Lleva el token de sesión como `Authorization: Bearer` y usa el JSON
/// camelCase/insensible a mayúsculas del backend. La URL base es
/// `http://localhost:5000` por defecto (variable de entorno
/// `PROJECTVTT_API` para cambiarla).
/// </summary>
public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public string BaseUrl { get; }

    /// <summary>Token de sesión actual (lo fija la vista de acceso).</summary>
    public string? Token { get; set; }

    public ApiClient(string? baseUrl = null)
    {
        BaseUrl = (baseUrl
            ?? Environment.GetEnvironmentVariable("PROJECTVTT_API")
            ?? "http://localhost:5000").TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl + "/") };
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // --- Identidad -------------------------------------------------------

    public Task<AuthResponse> RegisterAsync(string userName, string password) =>
        SendAsync<AuthResponse>(Request(HttpMethod.Post, "api/v1/auth/register",
            new RegisterRequest(userName, password)));

    public Task<AuthResponse> LoginAsync(string userName, string password) =>
        SendAsync<AuthResponse>(Request(HttpMethod.Post, "api/v1/auth/login",
            new LoginRequest(userName, password)));

    // --- Campañas y escenas ---------------------------------------------

    public Task<List<CampaignSummary>> ListCampaignsAsync() =>
        SendAsync<List<CampaignSummary>>(Request(HttpMethod.Get, "api/v1/campaigns"));

    public Task<CampaignSummary> CreateCampaignAsync(string name) =>
        SendAsync<CampaignSummary>(Request(HttpMethod.Post, "api/v1/campaigns",
            new CreateCampaignRequest(name)));

    public Task<List<SceneSummary>> ListScenesAsync(Guid campaignId) =>
        SendAsync<List<SceneSummary>>(Request(HttpMethod.Get,
            $"api/v1/campaigns/{campaignId}/scenes"));

    public Task<SceneDetail> CreateSceneAsync(Guid campaignId, string name) =>
        SendAsync<SceneDetail>(Request(HttpMethod.Post, $"api/v1/campaigns/{campaignId}/scenes",
            new CreateSceneRequest(name)));

    public Task<SceneDetail> GetSceneAsync(Guid sceneId) =>
        SendAsync<SceneDetail>(Request(HttpMethod.Get, $"api/v1/scenes/{sceneId}"));

    // --- Tokens y mapa ---------------------------------------------------

    public Task<TokenDto> CreateTokenAsync(Guid sceneId, string name, double x, double y) =>
        SendAsync<TokenDto>(Request(HttpMethod.Post, $"api/v1/scenes/{sceneId}/tokens",
            new CreateTokenRequest(name, x, y, null)));

    /// <summary>Mapa de la escena; null si no tiene (404).</summary>
    public Task<(byte[] Bytes, string ContentType)?> GetMapAsync(Guid sceneId) =>
        GetBinaryOrNullAsync($"api/v1/scenes/{sceneId}/map");

    /// <summary>Portada de campaña; null si no tiene (404).</summary>
    public Task<(byte[] Bytes, string ContentType)?> GetCampaignCoverAsync(Guid campaignId) =>
        GetBinaryOrNullAsync($"api/v1/campaigns/{campaignId}/cover");

    /// <summary>
    /// Archivo de una imagen de la biblioteca; null si no existe (404).
    /// Lo usa la ficha de actor para el retrato (rebanada 3).
    /// </summary>
    public Task<(byte[] Bytes, string ContentType)?> GetImageFileAsync(Guid imageId) =>
        GetBinaryOrNullAsync($"api/v1/images/{imageId}/file");

    // --- Biblioteca y actores (rebanadas 2 y 3) --------------------------

    /// <summary>Catálogo global de tipos; `system` es filtro opcional.</summary>
    public Task<List<ContentTypeDto>> ListContentTypesAsync(string? system = null) =>
        SendAsync<List<ContentTypeDto>>(Request(
            HttpMethod.Get, Query("api/v1/content/types", ("system", system))));

    /// <summary>Ítems visibles para el usuario; el servidor aplica permisos y secretaría.</summary>
    public Task<List<ContentItemDto>> ListContentItemsAsync(
        string? system = null, string? typeKey = null) =>
        SendAsync<List<ContentItemDto>>(Request(HttpMethod.Get,
            Query("api/v1/content/items", ("system", system), ("typeKey", typeKey))));

    /// <summary>Actores de una campaña en la que el usuario es miembro.</summary>
    public Task<List<ActorDto>> ListActorsAsync(Guid campaignId) =>
        SendAsync<List<ActorDto>>(Request(
            HttpMethod.Get, $"api/v1/campaigns/{campaignId}/actors"));

    /// <summary>Estado completo de las anotaciones de una escena (ADR-043).</summary>
    public Task<SceneAnnotationsStateDto> GetSceneAnnotationsAsync(Guid sceneId) =>
        SendAsync<SceneAnnotationsStateDto>(Request(
            HttpMethod.Get, $"api/v1/scenes/{sceneId}/annotations"));

    /// <summary>Descarga binaria con Bearer; el 404 es un estado normal.</summary>
    private async Task<(byte[] Bytes, string ContentType)?> GetBinaryOrNullAsync(string path)
    {
        using var response = await _http.SendAsync(Request(HttpMethod.Get, path));
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response);
        }

        return (await response.Content.ReadAsByteArrayAsync(),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    // --- Infraestructura -------------------------------------------------

    /// <summary>Compone la query de filtros opcionales sin tocar el contrato.</summary>
    private static string Query(string path, params (string Key, string? Value)[] filters)
    {
        var query = string.Join('&', filters
            .Where(filter => !string.IsNullOrWhiteSpace(filter.Value))
            .Select(filter => $"{filter.Key}={Uri.EscapeDataString(filter.Value!)}"));
        return query.Length == 0 ? path : $"{path}?{query}";
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrEmpty(Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        if (body is not null)
        {
            // Serialización explícita (sin System.Net.Http.Json): evita
            // mezclar versiones de ensamblados del framework en runtime.
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, body.GetType(), Json),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response);
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<T>(stream, Json))!;
    }

    public void Dispose() => _http.Dispose();
}
