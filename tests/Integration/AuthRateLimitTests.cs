using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Comprueba el límite de peticiones de los endpoints de autenticación.
/// Usa una factoria propia con un límite bajo para no depender del tiempo.
/// </summary>
public class AuthRateLimitTests
{
    private sealed class RateLimitedFactory : ApiTestFactory
    {
        protected override int AuthPermitLimit => 2;
    }

    [Fact]
    public async Task ExcessiveAuthRequests_Returns429ProblemDetails()
    {
        using var factory = new RateLimitedFactory();
        var client = factory.CreateClient();
        var payload = new
        {
            userName = TestClientExtensions.NewUserName(),
            password = TestClientExtensions.DefaultPassword,
        };

        // Los dos primeros intentos fallan con 401 (usuario inexistente),
        // pero consumen la cuota de la ventana.
        var first = await client.PostAsJsonAsync("/api/v1/auth/login", payload);
        var second = await client.PostAsJsonAsync("/api/v1/auth/login", payload);
        var third = await client.PostAsJsonAsync("/api/v1/auth/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        var json = await third.EnsureStatusAsync(HttpStatusCode.TooManyRequests);
        Assert.Equal("Demasiadas solicitudes.", json.GetProperty("title").GetString());
    }
}
