using Microsoft.Extensions.Configuration;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Validación de configuración al arrancar (ADR-024): fail-fast con
/// mensajes que nunca imprimen valores de la configuración (podrían ser
/// credenciales).
/// </summary>
public class StartupGuardTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] entries)
    {
        var values = entries.ToDictionary(entry => entry.Key, entry => entry.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(values!).Build();
    }

    [Fact]
    public void ValidConfiguration_ReturnsProviderAndConnectionString()
    {
        var configuration = Configuration(
            ("Database:Provider", "sqlite"),
            ("ConnectionStrings:Default", "Data Source=prueba.db"));

        var (provider, connectionString) = StartupGuard.Validate(configuration);

        Assert.Equal("sqlite", provider);
        Assert.Equal("Data Source=prueba.db", connectionString);
    }

    [Fact]
    public void ProviderDefaultsToPostgresAndIsCaseInsensitive()
    {
        var withDefault = Configuration(
            ("ConnectionStrings:Default", "Host=localhost"));
        Assert.Equal("postgres", StartupGuard.Validate(withDefault).Provider);

        var upper = Configuration(
            ("Database:Provider", "SQLite"),
            ("ConnectionStrings:Default", "Data Source=x"));
        Assert.Equal("SQLite", StartupGuard.Validate(upper).Provider);
    }

    [Fact]
    public void MissingConnectionString_ThrowsWithoutPrintingValues()
    {
        var configuration = Configuration(
            ("Database:Provider", "postgres"),
            ("UnAjuste:Sensible", "ESTO-NO-DEBE-APARECER"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => StartupGuard.Validate(configuration));

        Assert.Contains("ConnectionStrings", exception.Message);
        // El mensaje jamás debe filtrar valores de la configuración.
        Assert.DoesNotContain("ESTO-NO-DEBE-APARECER", exception.Message);
    }

    [Fact]
    public void UnsupportedProvider_Throws()
    {
        var configuration = Configuration(
            ("Database:Provider", "oracle"),
            ("ConnectionStrings:Default", "Data Source=x"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => StartupGuard.Validate(configuration));

        Assert.Contains("Database:Provider", exception.Message);
    }
}
