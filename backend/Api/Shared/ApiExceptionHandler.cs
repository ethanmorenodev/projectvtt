using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Manejador global de excepciones: las <see cref="ApiException"/> se
/// devuelven con su código y cualquier otro error se responde como 500
/// genérico sin filtrar detalles internos.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problems)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ApiException apiException)
        {
            httpContext.Response.StatusCode = apiException.StatusCode;
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = apiException.StatusCode,
                    Title = apiException.Title,
                    Detail = apiException.Detail,
                    Type = $"https://httpstatuses.com/{apiException.StatusCode}",
                    Extensions = apiException.Extensions is null
                        ? []
                        : new Dictionary<string, object?>(apiException.Extensions),
                },
            });
            return true;
        }

        logger.LogError(exception, "Excepción no controlada en {Path}", httpContext.Request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno.",
                Detail = "Se produjo un error inesperado. Inténtalo de nuevo.",
                Type = ReasonPhrases.GetReasonPhrase(StatusCodes.Status500InternalServerError),
            },
        });
        return true;
    }
}
