using System.Text.Json;

namespace ProjectVTT.Desktop.Api;

/// <summary>
/// Error de la API: conserva el `detail` del ProblemDetails del backend
/// para mostrar el mismo mensaje que vería el cliente web.
/// </summary>
public sealed class ApiException(string detail, int status) : Exception(detail)
{
    public int Status { get; } = status;

    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var detail = $"Error HTTP {(int)response.StatusCode}";
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("detail", out var value))
            {
                detail = value.GetString() ?? detail;
            }
        }
        catch (Exception)
        {
            // Sin cuerpo JSON legible: se mantiene el mensaje genérico.
        }

        return new ApiException(detail, (int)response.StatusCode);
    }
}
