using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectVTT.Api.Data;

/// <summary>
/// Permite ejecutar comandos de EF (migrations add, migrations script)
/// sin arrancar la API ni disponer de una base de datos real. Prefiere la
/// variable `ConnectionStrings__Default`; la cadena de respaldo es INERTE
/// (usuario `design_time` inexistente) y solo satisface al tooling de EF
/// (ADR-024: ningún secreto real vive en el repositorio).
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string InertFallback =
        "Host=localhost;Database=projectvtt;Username=design_time;Password=not-a-secret";

    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? InertFallback)
            .Options);
}
