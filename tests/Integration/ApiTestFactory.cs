using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVTT.Api.Data;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Arranca la API completa con SQLite en lugar de PostgreSQL para que las
/// pruebas de integración no requieran Docker. El esquema se crea con
/// EnsureCreated al iniciar cada factoria (base de datos por clase de prueba).
/// </summary>
public class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"projectvtt-tests-{Guid.NewGuid():N}.db");

    /// <summary>Límite alto para no disparar el rate limit de auth en pruebas.</summary>
    protected virtual int AuthPermitLimit => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath}");
        builder.UseSetting("RateLimit:AuthPermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("Auth:SessionMinutes", "60");
        // Exclusivo de tests: habilita el claim de operador para no romper
        // las pruebas existentes que crean tipos por HTTP. En producción
        // la allowlist está vacía por defecto (P0.1).
        builder.UseSetting("Content:OperatorUsers", "*");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // El archivo puede seguir bloqueado por el proveedor SQLite; se
            // eliminará del directorio temporal del sistema en otra ocasión.
        }
    }
}
