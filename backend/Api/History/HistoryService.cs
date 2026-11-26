using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.History;

/// <summary>
/// Historial de cambios de la campaña (Épica F, ADR-021). `Record` solo
/// añade la entrada al contexto: el `SaveChangesAsync` de la mutación
/// que lo llama la persiste en la misma transacción (atómico, sin
/// escrituras extra). Leer es de miembro; no hay mutación por HTTP: el
/// historial es append-only y lo escribe el servidor.
/// </summary>
public sealed class HistoryService(AppDbContext db, CampaignService campaigns)
{
    /// <summary>Entradas que devuelve el historial (las más recientes primero).</summary>
    private const int HistoryLimit = 100;

    /// <summary>Resumen redactado para mostrar.</summary>
    public const int MaxSummaryLength = 200;

    /// <summary>
    /// Registra una acción sobre el estado compartido. Se guarda con el
    /// próximo `SaveChangesAsync` del mismo contexto.
    /// </summary>
    public void Record(Guid campaignId, Guid userId, string kind, string summary)
    {
        db.HistoryEntries.Add(new HistoryEntry
        {
            // Id v7 (monótono por tiempo): con timestamps iguales el
            // desempate del orden del historial sigue siendo estable.
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            UserId = userId,
            Kind = kind,
            Summary = summary,
            CreatedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Historial reciente de la campaña: hasta 100 entradas, de más reciente a más antigua.</summary>
    public async Task<IReadOnlyList<HistoryEntryDto>> ListAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        return await (
            from h in db.HistoryEntries
            join u in db.Users on h.UserId equals u.Id
            where h.CampaignId == campaignId
            orderby h.CreatedAt descending, h.Id descending
            select new HistoryEntryDto(
                h.Id, h.CampaignId, h.UserId, u.UserName, h.Kind, h.Summary, h.CreatedAt))
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Actividad reciente en todas las campañas del usuario (PMB-2.2,
    /// pantalla de Inicio). Solo campañas propias; mismas garantías de
    /// privacidad que el historial (sin notas ni mensajes privados).
    /// </summary>
    public async Task<IReadOnlyList<ActivityEntryDto>> ListActivityAsync(
        Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 50);
        return await (
            from h in db.HistoryEntries
            join u in db.Users on h.UserId equals u.Id
            join c in db.Campaigns on h.CampaignId equals c.Id
            where db.CampaignMembers.Any(m => m.CampaignId == h.CampaignId && m.UserId == userId)
            orderby h.CreatedAt descending, h.Id descending
            select new ActivityEntryDto(
                h.CampaignId, c.Name, u.UserName, h.Kind, h.Summary, h.CreatedAt))
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
