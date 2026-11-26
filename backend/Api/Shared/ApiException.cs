namespace ProjectVTT.Api.Shared;

/// <summary>
/// Error controlado de la API. Se convierte en un ProblemDetails
/// consistente por <see cref="ApiExceptionHandler"/>.
/// </summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IReadOnlyDictionary<string, object?>? Extensions { get; }

    private ApiException(int statusCode, string title, string detail,
        IReadOnlyDictionary<string, object?>? extensions = null)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Extensions = extensions;
    }

    public string Detail => Message;

    public static ApiException BadRequest(string detail,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(StatusCodes.Status400BadRequest, "Solicitud inválida.", detail, extensions);

    public static ApiException Unauthorized(string detail = "Se requiere una sesión válida.") =>
        new(StatusCodes.Status401Unauthorized, "No autenticado.", detail);

    public static ApiException Forbidden(string detail =
        "No tienes permisos para realizar esta operación.") =>
        new(StatusCodes.Status403Forbidden, "Acceso denegado.", detail);

    public static ApiException NotFound(string detail = "Recurso no encontrado.") =>
        new(StatusCodes.Status404NotFound, "No encontrado.", detail);

    public static ApiException Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, "Conflicto.", detail);

    public static ApiException Gone(string detail) =>
        new(StatusCodes.Status410Gone, "Recurso no disponible.", detail);
}
