using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Log estructurado de peticiones (ADR-022): registra método, ruta,
/// estado, duración y usuario, y NUNCA filtra query string (el hub
/// acepta `?access_token=`) ni cabeceras de autorización.
/// </summary>
public class RequestLoggingMiddlewareTests
{
    private sealed class CapturingLogger : ILogger<RequestLoggingMiddleware>
    {
        public readonly List<string> Messages = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private static async Task<CapturingLogger> RunAsync(
        RequestDelegate next, Action<HttpContext>? configure = null)
    {
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(next, logger, new MetricsCollector());
        var context = new DefaultHttpContext();
        configure?.Invoke(context);
        try
        {
            await middleware.InvokeAsync(context);
        }
        catch
        {
            // El middleware relanza las excepciones (solo loguea); el
            // test de fallos interiores depende de ello.
        }

        return logger;
    }

    [Fact]
    public async Task LogsMethodPathStatusAndUser()
    {
        var logger = await RunAsync(
            next: context =>
            {
                context.Response.StatusCode = 201;
                return Task.CompletedTask;
            },
            configure: context =>
            {
                context.Request.Method = "POST";
                context.Request.Path = "/api/v1/campaigns/abc/messages";
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "user-123"),
                ]));
            });

        var message = Assert.Single(logger.Messages);
        Assert.Contains("POST", message);
        Assert.Contains("/api/v1/campaigns/abc/messages", message);
        Assert.Contains("201", message);
        Assert.Contains("user-123", message);
        Assert.Contains("ms", message);
    }

    [Fact]
    public async Task AnonymousRequestLogsAnonimo()
    {
        var logger = await RunAsync(
            _ => Task.CompletedTask,
            context => context.Request.Path = "/health");

        var message = Assert.Single(logger.Messages);
        Assert.Contains("anonimo", message);
    }

    [Fact]
    public async Task QueryStringIsNeverLogged()
    {
        // El hub acepta `?access_token=`; jamás debe llegar a los logs.
        var logger = await RunAsync(
            _ => Task.CompletedTask,
            context =>
            {
                context.Request.Path = "/hubs/campaign";
                context.Request.QueryString = new QueryString("?access_token=TOKEN-SECRETO");
            });

        var message = Assert.Single(logger.Messages);
        Assert.DoesNotContain("TOKEN-SECRETO", message);
        Assert.DoesNotContain("access_token", message);
    }

    [Fact]
    public async Task AuthorizationHeaderIsNeverLogged()
    {
        var logger = await RunAsync(
            _ => Task.CompletedTask,
            context =>
            {
                context.Request.Path = "/api/v1/campaigns";
                context.Request.Headers.Authorization = "Bearer OTRO-SECRETO";
            });

        var message = Assert.Single(logger.Messages);
        Assert.DoesNotContain("OTRO-SECRETO", message);
    }

    [Fact]
    public async Task LogsEvenWhenTheRequestFails()
    {
        var logger = await RunAsync(
            _ => throw new InvalidOperationException("fallo interno"),
            context => context.Request.Path = "/api/v1/que-falla");

        var message = Assert.Single(logger.Messages);
        Assert.Contains("/api/v1/que-falla", message);
    }
}
