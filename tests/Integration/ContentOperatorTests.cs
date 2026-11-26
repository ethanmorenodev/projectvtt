using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Factory sin wildcard: allowlist de operadores vacía, como en producción.
/// La factoría base habilita '*' solo para las pruebas existentes.
/// </summary>
public sealed class NoOperatorApiTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Content:OperatorUsers", string.Empty);
    }
}

/// <summary>
/// P0.1: la mutación del catálogo global (tipos e importación de paquetes)
/// exige el claim de operador, concedido solo por la allowlist.
/// </summary>
public class ContentOperatorTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task UsuarioPermitido_PuedeCrearTipoGlobal()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
        });

        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal("dnd5e", json.GetProperty("system").GetString());
    }
}

/// <summary>Factory con un operador concreto en la allowlist.</summary>
public sealed class NamedOperatorApiTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Content:OperatorUsers", "operador-e2e");
    }
}

/// <summary>La allowlist por nombre es case-insensitive y no abre a otros usuarios.</summary>
public class NamedOperatorTests(NamedOperatorApiTestFactory factory)
    : IClassFixture<NamedOperatorApiTestFactory>
{
    private static object NewType() => new
    {
        system = "dnd5e",
        key = $"race-{Guid.NewGuid():N}",
        displayName = "Raza",
        schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
    };

    [Fact]
    public async Task NombreEnAllowlistConDistintaCapitalizacion_PuedeCrearTipo()
    {
        using var client = await factory.CreateAuthenticatedClientAsync("OPERADOR-E2E");
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", NewType());
        await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task OtroUsuario_NoPuedeCrearTipo()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", NewType());
        await response.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }
}

/// <summary>La configuración vacía no concede el claim a nadie.</summary>
public class ContentOperatorRestrictedTests(NoOperatorApiTestFactory factory)
    : IClassFixture<NoOperatorApiTestFactory>
{
    [Fact]
    public async Task UsuarioNormal_NoPuedeCrearTipoGlobal()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
        });

        await response.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UsuarioNormal_NoPuedeImportarPaquetes()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Keep"), "strategy" },
        };

        // Sin archivo: si la política se evalúa antes del handler, debe
        // llegar 403 y no el 400 del cuerpo incompleto.
        using var response = await client.PostAsync("/api/v1/content/packs/import", form);

        await response.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UsuarioNormal_PuedeListarTiposYVerElSeed()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/v1/content/types?system=dnd5e");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var keys = json.EnumerateArray()
            .Select(x => x.GetProperty("key").GetString())
            .ToArray();

        // ContentSeedLoader importa al arrancar, sin pasar por endpoints.
        Assert.Contains("race", keys);
        Assert.Contains("class", keys);
    }
}
