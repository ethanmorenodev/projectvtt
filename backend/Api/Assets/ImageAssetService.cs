using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Assets;

/// <summary>
/// Biblioteca de imágenes (PMB-4.7): metadatos, álbumes/etiquetas, licencias
/// y atribuciones. El servidor valida formato/tamaño y dueño de cada asset;
/// los clientes nunca deciden permisos ni procedencia.
/// </summary>
public sealed class ImageAssetService(AppDbContext db, IAssetStore assets)
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "image/svg+xml",
    };
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "icono", "miniatura", "ilustracion",
    };

    public async Task<List<ImageAssetDto>> ListAsync(
        Guid userId, string? role, string? album, string? tag, string? license,
        string? query, string? packKey = null, CancellationToken cancellationToken = default)
    {
        var rows = await db.ImageAssets.AsNoTracking()
            .Where(x => x.OwnerUserId == userId || x.PackKey != null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var q = query?.Trim().ToLowerInvariant();
        var filtered = rows
            .Where(x => role is null || x.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
            .Where(x => album is null || Parse(x.AlbumsJson).Contains(album))
            .Where(x => tag is null || Parse(x.TagsJson).Contains(tag))
            .Where(x => license is null
                || x.License?.Equals(license, StringComparison.OrdinalIgnoreCase) == true)
            .Where(x => packKey is null
                || x.PackKey?.Equals(packKey, StringComparison.OrdinalIgnoreCase) == true
                || (packKey.Equals("propio", StringComparison.OrdinalIgnoreCase) && x.PackKey is null))
            .Where(x => string.IsNullOrEmpty(q) || x.FileName.ToLowerInvariant().Contains(q))
            .ToList();

        var ids = filtered.Select(x => x.Id).ToList();
        var counts = await db.ContentItems.AsNoTracking()
            .Where(x => x.ImageId.HasValue && ids.Contains(x.ImageId.Value))
            .GroupBy(x => x.ImageId!.Value)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return filtered
            .Select(x => ToDto(x, counts.GetValueOrDefault(x.Id)))
            .ToList();
    }

    public async Task<ImageAssetDto> UploadAsync(
        Guid userId, string fileName, string contentType, Stream content, long sizeBytes,
        UploadImageAssetRequest request, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(InputGuard.RequiredText(fileName, "fileName", maxLength: 260));
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            throw ApiException.BadRequest("Nombre de archivo no válido.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }
        if (!AllowedTypes.Contains(contentType))
        {
            throw ApiException.BadRequest(
                "Formato no admitido (PNG, JPEG, WebP o SVG).",
                new Dictionary<string, object?> { ["field"] = "file" });
        }
        if (sizeBytes <= 0 || sizeBytes > MaxBytes)
        {
            throw ApiException.BadRequest(
                "El archivo debe tener entre 1 byte y 5 MB.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }

        var role = NormalizeRole(request.Role);
        var id = Guid.NewGuid();
        var storageKey = $"images/{userId:N}/{id:N}/{name}";
        await assets.SaveAsync(storageKey, content, cancellationToken);

        var asset = new ImageAsset
        {
            Id = id,
            OwnerUserId = userId,
            FileName = name,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Role = role,
            StorageKey = storageKey,
            Focal = request.Focal,
            TagsJson = Serialize(request.Tags),
            AlbumsJson = Serialize(request.Albums),
            // Las variantes derivadas se generarán en el editor (ADR-035):
            // por ahora se sirve el original con recorte CSS en la rejilla.
            VariantsJson = "{}",
            License = string.IsNullOrWhiteSpace(request.License) ? "Propietario" : request.License.Trim(),
            LicenseUrl = string.IsNullOrWhiteSpace(request.LicenseUrl) ? null : request.LicenseUrl.Trim(),
            Author = string.IsNullOrWhiteSpace(request.Author) ? "Usuario" : request.Author.Trim(),
            Origin = string.IsNullOrWhiteSpace(request.Origin) ? "Subido por el usuario" : request.Origin.Trim(),
            AttributionJson = Serialize(request.Attribution),
            CreatedAt = DateTime.UtcNow,
        };
        db.ImageAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(asset, cancellationToken);
    }

    public async Task<ImageAssetDto> UpdateAsync(
        Guid id, Guid userId, UpdateImageAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var asset = await FindOwnedAsync(id, userId, cancellationToken);
        if (asset.PackKey is not null && (request.License is not null || request.Attribution is not null))
        {
            throw ApiException.BadRequest(
                "Las licencias y atribuciones de un paquete importado son inmutables (ADR-036).");
        }

        if (request.Role is not null) asset.Role = NormalizeRole(request.Role);
        if (request.Albums is not null) asset.AlbumsJson = Serialize(request.Albums);
        if (request.Tags is not null) asset.TagsJson = Serialize(request.Tags);
        if (request.License is not null) asset.License = InputGuard.RequiredText(request.License, "license", 100);
        if (request.LicenseUrl is not null) asset.LicenseUrl = request.LicenseUrl;
        if (request.Author is not null) asset.Author = InputGuard.RequiredText(request.Author, "author", 200);
        if (request.Origin is not null) asset.Origin = request.Origin;
        if (request.Attribution is not null) asset.AttributionJson = Serialize(request.Attribution);
        if (request.Focal is not null) asset.Focal = request.Focal;

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(asset, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var asset = await FindOwnedAsync(id, userId, cancellationToken);
        db.ImageAssets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
        await assets.DeleteAsync(asset.StorageKey, cancellationToken);
    }

    /// <summary>
    /// Sustituye el archivo conservando `Id` y `StorageKey`: los items que
    /// referencian `imageId` siguen apuntando al mismo recurso (PMB-4.10).
    /// </summary>
    public async Task<ImageAssetDto> ReplaceAsync(
        Guid id, Guid userId, string fileName, string contentType,
        Stream content, long sizeBytes,
        IReadOnlyDictionary<string, (Stream Content, string ContentType, long Size)> variants,
        CancellationToken cancellationToken = default)
    {
        var asset = await FindOwnedAsync(id, userId, cancellationToken);
        var name = Path.GetFileName(InputGuard.RequiredText(fileName, "fileName", maxLength: 260));
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            throw ApiException.BadRequest("Nombre de archivo no válido.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }
        if (!AllowedTypes.Contains(contentType) || sizeBytes <= 0 || sizeBytes > MaxBytes)
        {
            throw ApiException.BadRequest(
                "Formato o tamaño no admitido (PNG, JPEG, WebP o SVG, hasta 5 MB).",
                new Dictionary<string, object?> { ["field"] = "file" });
        }

        await assets.SaveAsync(asset.StorageKey, content, cancellationToken);
        var stored = new Dictionary<string, string>();
        foreach (var (key, variant) in variants)
        {
            var normalized = key.Trim().ToLowerInvariant();
            if (normalized is not ("thumb-sm" or "thumb-md" or "thumb-lg"))
            {
                throw ApiException.BadRequest($"Variante desconocida '{key}'.");
            }
            if (!AllowedTypes.Contains(variant.ContentType)
                || variant.Size <= 0 || variant.Size > MaxBytes)
            {
                throw ApiException.BadRequest(
                    $"Variante '{key}' no válida.",
                    new Dictionary<string, object?> { ["field"] = key });
            }
            var variantKey = $"{asset.StorageKey}.{normalized}";
            await assets.SaveAsync(variantKey, variant.Content, cancellationToken);
            stored[normalized] = variantKey;
        }

        asset.FileName = name;
        asset.ContentType = contentType;
        asset.SizeBytes = sizeBytes;
        asset.VariantsJson = JsonSerializer.Serialize(stored);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(asset, cancellationToken);
    }

    public async Task<(ImageAssetDto Asset, Stream Content)?> OpenAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var asset = await db.ImageAssets.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == id && (x.OwnerUserId == userId || x.PackKey != null), cancellationToken);
        if (asset is null)
        {
            return null;
        }
        var content = await assets.OpenReadAsync(asset.StorageKey, cancellationToken);
        return content is null
            ? null
            : (await ToDtoAsync(asset, cancellationToken), content);
    }

    /// <summary>Nota de atribución exportable (ADR-036 §5).</summary>
    public async Task<string> ExportCreditsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var packs = await db.ContentPacks.AsNoTracking()
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var images = await db.ImageAssets.AsNoTracking()
            .Where(x => x.OwnerUserId == userId || x.PackKey != null)
            .OrderBy(x => x.FileName).ToListAsync(cancellationToken);

        var lines = new List<string> { "Atribuciones de ProjectVTT", string.Empty };
        // Iconografía de la interfaz (PMB-8.6b, CC BY 3.0): va siempre en la
        // nota exportable para no perder la atribución obligatoria (ADR-036).
        lines.Add("Iconos de game-icons.net, por Lorc, Delapouite y colaboradores - CC BY 3.0 (iconografía de la interfaz)");
        lines.Add("Licencia: http://creativecommons.org/licenses/by/3.0/");
        lines.Add("Fuente: https://game-icons.net/");
        lines.Add(string.Empty);
        foreach (var pack in packs)
        {
            lines.Add($"{pack.Name} — {pack.License} ({pack.LicenseUrl})");
            lines.AddRange(Parse(pack.AttributionJson));
        }
        lines.Add(string.Empty);
        foreach (var image in images)
        {
            lines.Add($"{image.FileName} — {image.Role} — {image.License}"
                + (image.LicenseUrl is null ? string.Empty : $" ({image.LicenseUrl})"));
            lines.AddRange(Parse(image.AttributionJson));
        }
        return string.Join('\n', lines);
    }

    private async Task<ImageAsset> FindOwnedAsync(
        Guid id, Guid userId, CancellationToken cancellationToken) =>
        await db.ImageAssets.FirstOrDefaultAsync(
            x => x.Id == id && x.OwnerUserId == userId, cancellationToken)
            ?? throw ApiException.NotFound("Imagen no encontrada.");

    private static string NormalizeRole(string? raw) =>
        raw is not null && AllowedRoles.Contains(raw.Trim())
            ? raw.Trim().ToLowerInvariant()
            : "miniatura";

    private static string Serialize(string[]? values) =>
        JsonSerializer.Serialize(values ?? []);

    private static string[] Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<ImageAssetDto> ToDtoAsync(ImageAsset x, CancellationToken ct) =>
        ToDto(x, await db.ContentItems.CountAsync(item => item.ImageId == x.Id, ct));

    private static ImageAssetDto ToDto(ImageAsset x, int referenceCount) => new(
        x.Id, x.FileName, x.ContentType, x.SizeBytes, x.Role,
        Parse(x.AlbumsJson), Parse(x.TagsJson),
        x.License ?? "Propietario", x.LicenseUrl, x.Author ?? "Usuario",
        x.Origin ?? string.Empty, Parse(x.AttributionJson), x.Focal,
        x.PackKey, x.CreatedAt, referenceCount, ParseVariants(x.VariantsJson));

    private static string[] ParseVariants(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)?.Keys.ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
