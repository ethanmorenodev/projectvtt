using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Actors;
using ProjectVTT.Api.Annotations;
using ProjectVTT.Api.Assets;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Characters;
using ProjectVTT.Api.Chat;
using ProjectVTT.Api.Combats;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Fog;
using ProjectVTT.Api.History;
using ProjectVTT.Api.Identity;
using ProjectVTT.Api.Notes;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Rules;
using ProjectVTT.Api.Scenes;
using ProjectVTT.Api.Shared;
using ProjectVTT.Api.Worlds;

var builder = WebApplication.CreateBuilder(args);

// Logs estructurados (Épica H, ADR-022): JSON por línea en consola con
// marca UTC, apto para ingesta por herramientas de operación. Sin
// dependencias extra: basta el formatter incluido en .NET.
builder.Logging.AddJsonConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    options.UseUtcTimestamp = true;
    options.IncludeScopes = true;
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

// JSON: los enums se serializan como texto ("Owner", "Player"...).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Operación (Épica H, ADR-026): métricas y salud de la base de datos.
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// Assets (ADR-028): almacén de archivos; el backend por defecto es el
// sistema de archivos local y un S3/MinIO (ADR-007) solo implementa
// IAssetStore.
builder.Services.AddSingleton<IAssetStore, FileAssetStore>();
builder.Services.AddScoped<MapService>();

// Backups y restauración (ADR-029): núcleo compartido con la CLI de
// operación (tools/ProjectVTT.Tools).
builder.Services.AddScoped<BackupService>();

// Base de datos: 'postgres' por defecto; 'sqlite' se usa en las pruebas
// de integración para no requerir Docker (ver ADR-009). Con SQLite no hay
// migraciones aplicables (son de Npgsql), así que en local sin Docker se
// crea el esquema con EnsureCreated; con postgres se mantiene sin
// auto-migrate (migraciones explícitas con dotnet ef). La configuración
// se valida de fail-fast sin imprimir valores (ADR-024).
var (provider, connectionString) = StartupGuard.Validate(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // El proveedor ya está validado por StartupGuard.
    if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

// Autenticación por sesión opaca (ADR-009): sin secretos que gestionar.
builder.Services.AddSingleton(new AuthSessionOptions
{
    LifetimeMinutes = builder.Configuration.GetValue("Auth:SessionMinutes", 720),
});
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<SceneService>();
builder.Services.AddScoped<ActorService>();
builder.Services.AddScoped<ActorItemService>();
builder.Services.AddScoped<ActorEffectService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<DirectMessageService>();
builder.Services.AddScoped<CombatService>();
builder.Services.AddScoped<FogService>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<RollService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<WorldService>();
builder.Services.AddScoped<LoreService>();
builder.Services.AddScoped<PackageService>();
builder.Services.AddScoped<ContentPackService>();
builder.Services.AddScoped<ImageAssetService>();
builder.Services.AddScoped<CharacterBlueprintService>();
builder.Services.AddScoped<RealtimeService>();

// Rulesets: cada IRuleset se registra y el resolutor los expone por clave
// (el Core no conoce reglas específicas; ver ARCHITECTURE.md §4).
builder.Services.AddSingleton<IRuleset, Dnd5eRuleset>();
builder.Services.AddSingleton<IRulesetRegistry, RulesetRegistry>();
builder.Services.AddSignalR();
builder.Services
    .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization(options =>
    options.AddPolicy(ContentEndpoints.ContentOperatorPolicy, policy =>
        policy.RequireClaim(BearerTokenAuthenticationHandler.ContentOperatorClaim)));

// Límite de peticiones para registro e inicio de sesión.
var authPermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermitLimit", 10);
var authWindowSeconds = builder.Configuration.GetValue("RateLimit:AuthWindowSeconds", 10);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status429TooManyRequests,
            title = "Demasiadas solicitudes.",
            detail = "Espera unos segundos antes de volver a intentarlo.",
        }, cancellationToken);
    };
});

// CORS para el cliente web en desarrollo (origenes en la configuración).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

// Esquema para SQLite en local sin Docker (el DDL de Npgsql no aplica aquí).
// EnsureCreated no actualiza BDs existentes: la guardia detecta un esquema
// desactualizado y, solo si el launcher local lo autoriza, la respalda y
// regenera. En despliegues reales falla con un mensaje accionable.
if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var resetDemo = Environment.GetEnvironmentVariable(
        SqliteSchemaGuard.ResetEnvironmentVariable) == "1";
    await SqliteSchemaGuard.EnsureUsableAsync(db, connectionString, resetDemo);
    db.Database.EnsureCreated();
}

// Contenido real empaquetado (PMB-P.4, ADR-037): importación idempotente
// por (paquete, key) al arrancar; sin cambios si el contenido no difiere.
await ContentSeedLoader.ImportAsync(app.Services);

// Blueprints de creación por sistema (PMB-3.2, ADR-034): datos, no código.
await CharacterBlueprintLoader.ImportAsync(app.Services);

app.UseExceptionHandler();
app.UseRateLimiter();

// Los errores 4xx/5xx sin cuerpo (p. ej. ruta inexistente) también se
// responden como ProblemDetails, para que todos los fallos sean consistentes.
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var status = response.StatusCode;
    if (response.HasStarted || response.ContentLength is not null || status < 400)
    {
        return;
    }

    var (title, detail) = status switch
    {
        StatusCodes.Status404NotFound => ("No encontrado.", "La ruta solicitada no existe."),
        StatusCodes.Status405MethodNotAllowed =>
            ("Método no permitido.", "El método HTTP no está soportado en esta ruta."),
        _ => ("Error.", $"La petición falló con el código {status}."),
    };

    await response.WriteAsJsonAsync(
        new { status, title, detail, type = $"https://httpstatuses.com/{status}" },
        options: null,
        contentType: "application/problem+json");
});

app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
// Log estructurado por petición (sin query string ni cabeceras: nunca
// tokens en los logs; ADR-022). Tras la autenticación para tener usuario.
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "projectvtt-api" }));
app.MapGet("/", () => Results.Ok(new
{
    name = "ProjectVTT API",
    openapi = "/openapi/v1.json",
}));
app.MapAuthEndpoints();
app.MapCampaignEndpoints();
app.MapSceneEndpoints();
app.MapSceneAnnotationEndpoints();
app.MapActorEndpoints();
app.MapContentEndpoints();
app.MapBuildEndpoints();
app.MapChatEndpoints();
app.MapCombatEndpoints();
app.MapFogEndpoints();
app.MapHistoryEndpoints();
app.MapNoteEndpoints();
app.MapRulesEndpoints();
app.MapAssetEndpoints();
app.MapWorldEndpoints();
app.MapOpsEndpoints();
app.MapHub<CampaignHub>("/hubs/campaign");

app.Run();

// Permite que las pruebas de integración arranquen la API con WebApplicationFactory.
public partial class Program { }
