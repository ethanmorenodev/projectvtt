using System.Text.RegularExpressions;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Validación de entradas en el servidor. Todo dato que llega por HTTP
/// pasa por aquí antes de tocar la base de datos.
/// </summary>
public static partial class InputGuard
{
    [GeneratedRegex("^[a-zA-Z0-9_.-]+$")]
    private static partial Regex UserNamePattern();

    /// <summary>Texto obligatorio recortado, de 1 a <paramref name="maxLength"/>.</summary>
    public static string RequiredText(string? value, string field, int maxLength, int minLength = 1)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length < minLength || text.Length > maxLength)
        {
            throw ApiException.BadRequest(
                $"El campo '{field}' debe tener entre {minLength} y {maxLength} caracteres.",
                new Dictionary<string, object?> { ["field"] = field });
        }

        return text;
    }

    /// <summary>Nombre de usuario: 3-32 caracteres ASCII, sin espacios.</summary>
    public static string UserName(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 3 or > 32 || !UserNamePattern().IsMatch(text))
        {
            throw ApiException.BadRequest(
                "El nombre de usuario debe tener entre 3 y 32 caracteres y solo puede contener letras, números, punto, guion bajo o guion.",
                new Dictionary<string, object?> { ["field"] = "userName" });
        }

        return text;
    }

    /// <summary>
    /// Contraseña: 8-128 caracteres. No se recorta: los espacios son
    /// caracteres válidos y forman parte de la contraseña elegida.
    /// </summary>
    public static string Password(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length is < 8 or > 128)
        {
            throw ApiException.BadRequest(
                "La contraseña debe tener entre 8 y 128 caracteres.",
                new Dictionary<string, object?> { ["field"] = "password" });
        }

        return value;
    }
}
