using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Assets;

public static class AssetEndpoints
{
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Assets")
            .RequireAuthorization();

        group.MapGet("/scenes/{sceneId:guid}/map", GetMapAsync);
        group.MapPut("/scenes/{sceneId:guid}/map", UploadMapAsync);
        group.MapDelete("/scenes/{sceneId:guid}/map", DeleteMapAsync);

        // Biblioteca de imágenes y créditos (PMB-4.7, aditivo aprobado).
        group.MapGet("/images", ListImagesAsync);
        group.MapGet("/images/{imageId:guid}", GetImageAsync);
        group.MapGet("/images/{imageId:guid}/file", GetImageFileAsync);
        group.MapPost("/images", UploadImageAsync);
        group.MapPatch("/images/{imageId:guid}", UpdateImageAsync);
        group.MapDelete("/images/{imageId:guid}", DeleteImageAsync);
        group.MapPost("/images/{imageId:guid}/replace", ReplaceImageAsync);
        group.MapGet("/credits/export", ExportCreditsAsync);

        return endpoints;
    }

    private static async Task<IResult> GetMapAsync(
        HttpContext http, Guid sceneId, MapService maps, CancellationToken cancellationToken)
    {
        var opened = await maps.OpenAsync(sceneId, GetUserId(http), cancellationToken);
        if (opened is null)
        {
            return Results.NotFound();
        }

        var (map, content) = opened.Value;
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Sin caché: el mapa puede cambiar en caliente.
        http.Response.Headers["Cache-Control"] = "no-store";
        return Results.File(content, map.ContentType);
    }

    private static async Task<IResult> UploadMapAsync(
        HttpContext http, Guid sceneId, MapService maps, CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file")
            ?? throw ApiException.BadRequest(
                "Falta el archivo 'file'.",
                new Dictionary<string, object?> { ["field"] = "file" });

        await using var content = file.OpenReadStream();
        var map = await maps.UploadAsync(
            sceneId, GetUserId(http), file.FileName, file.ContentType,
            content, file.Length, cancellationToken);
        return Results.Ok(map);
    }

    private static async Task<IResult> DeleteMapAsync(
        HttpContext http, Guid sceneId, MapService maps, CancellationToken cancellationToken)
    {
        await maps.DeleteAsync(sceneId, GetUserId(http), cancellationToken);
        return Results.Ok(new { deleted = true });
    }

    // --- Biblioteca de imágenes (PMB-4.7) -------------------------------

    private static async Task<IResult> ListImagesAsync(
        HttpContext http, string? role, string? album, string? tag, string? license,
        string? query, string? packKey, ImageAssetService images,
        CancellationToken cancellationToken) =>
        Results.Ok(await images.ListAsync(
            GetUserId(http), role, album, tag, license, query, packKey, cancellationToken));

    private static async Task<IResult> GetImageAsync(
        HttpContext http, Guid imageId, ImageAssetService images,
        CancellationToken cancellationToken)
    {
        var rows = await images.ListAsync(
            GetUserId(http), null, null, null, null, null, null, cancellationToken);
        var asset = rows.FirstOrDefault(x => x.Id == imageId);
        return asset is null ? Results.NotFound() : Results.Ok(asset);
    }

    private static async Task<IResult> GetImageFileAsync(
        HttpContext http, Guid imageId, ImageAssetService images,
        CancellationToken cancellationToken)
    {
        var opened = await images.OpenAsync(imageId, GetUserId(http), cancellationToken);
        if (opened is null)
        {
            return Results.NotFound();
        }
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers["Cache-Control"] = "no-store";
        return Results.File(opened.Value.Content, opened.Value.Asset.ContentType);
    }

    private static async Task<IResult> UploadImageAsync(
        HttpContext http, ImageAssetService images, CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file")
            ?? throw ApiException.BadRequest(
                "Falta el archivo 'file'.",
                new Dictionary<string, object?> { ["field"] = "file" });
        await using var content = file.OpenReadStream();
        var created = await images.UploadAsync(
            GetUserId(http), file.FileName, file.ContentType ?? "application/octet-stream",
            content, file.Length,
            new UploadImageAssetRequest(
                Field(form, "role"), List(form, "albums"), List(form, "tags"),
                Field(form, "license"), Field(form, "licenseUrl"), Field(form, "author"),
                Field(form, "origin"), List(form, "attribution"), Field(form, "focal")),
            cancellationToken);
        return Results.Created($"/api/v1/images/{created.Id}", created);
    }

    private static async Task<IResult> UpdateImageAsync(
        HttpContext http, Guid imageId, UpdateImageAssetRequest request,
        ImageAssetService images, CancellationToken cancellationToken) =>
        Results.Ok(await images.UpdateAsync(
            imageId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> ReplaceImageAsync(
        HttpContext http, Guid imageId, ImageAssetService images,
        CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file")
            ?? throw ApiException.BadRequest(
                "Falta el archivo 'file'.",
                new Dictionary<string, object?> { ["field"] = "file" });
        var variants = new Dictionary<string, (Stream Content, string ContentType, long Size)>();
        foreach (var key in new[] { "thumb-sm", "thumb-md", "thumb-lg" })
        {
            var variant = form.Files.GetFile(key);
            if (variant is not null)
            {
                variants[key] = (
                    variant.OpenReadStream(),
                    variant.ContentType ?? "application/octet-stream",
                    variant.Length);
            }
        }
        await using var content = file.OpenReadStream();
        var updated = await images.ReplaceAsync(
            imageId, GetUserId(http), file.FileName,
            file.ContentType ?? "application/octet-stream", content, file.Length,
            variants, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task<IResult> DeleteImageAsync(
        HttpContext http, Guid imageId, ImageAssetService images,
        CancellationToken cancellationToken)
    {
        await images.DeleteAsync(imageId, GetUserId(http), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ExportCreditsAsync(
        HttpContext http, ImageAssetService images, CancellationToken cancellationToken) =>
        Results.Text(
            await images.ExportCreditsAsync(GetUserId(http), cancellationToken),
            "text/plain; charset=utf-8");

    private static string? Field(IFormCollection form, string key)
    {
        var value = form[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string[]? List(IFormCollection form, string key)
    {
        var value = Field(form, key);
        return value is null
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static Guid GetUserId(HttpContext http)
    {
        var value = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            throw ApiException.Unauthorized();
        }

        return userId;
    }
}
