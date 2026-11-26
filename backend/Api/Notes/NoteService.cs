using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Notes;

/// <summary>
/// Notas privadas del GM de una campaña (Épica F, ADR-019): solo GM u
/// Owner pueden leerlas ni tocarlas (un jugador recibe 403 y un no
/// miembro 404). Sin evento en vivo: la UI vive en la página de campaña
/// y se actualiza por petición, lo que además elimina cualquier riesgo
/// de filtración a jugadores.
/// </summary>
public sealed class NoteService(AppDbContext db, CampaignService campaigns)
{
    /// <summary>Límites del título y el cuerpo; el servidor y el cliente validan lo mismo.</summary>
    public const int MaxTitleLength = 100;
    public const int MaxBodyLength = 5000;

    public async Task<IReadOnlyList<NoteDto>> ListAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await RequireGmAsync(campaignId, userId, cancellationToken);
        return await QueryAsync(campaignId, cancellationToken);
    }

    public async Task<NoteDto> CreateAsync(
        Guid campaignId, Guid userId, CreateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireGmAsync(campaignId, userId, cancellationToken);
        var title = InputGuard.RequiredText(request.Title, "title", maxLength: MaxTitleLength);
        var body = InputGuard.RequiredText(
            request.Body, "body", maxLength: MaxBodyLength, minLength: 0);
        var now = DateTime.UtcNow;

        var note = new CampaignNote
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Title = title,
            Body = body,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.CampaignNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<NoteDto> UpdateAsync(
        Guid noteId, Guid userId, UpdateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var note = await db.CampaignNotes
            .FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken)
            ?? throw ApiException.NotFound("La nota no existe.");
        // La nota pertenece a SU campaña: no miembro → 404, sin IDOR.
        await RequireGmAsync(note.CampaignId, userId, cancellationToken);

        if (request.Title is not null)
        {
            note.Title = InputGuard.RequiredText(request.Title, "title", maxLength: MaxTitleLength);
        }

        if (request.Body is not null)
        {
            note.Body = InputGuard.RequiredText(
                request.Body, "body", maxLength: MaxBodyLength, minLength: 0);
        }

        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<NoteDto> DeleteAsync(
        Guid noteId, Guid userId, CancellationToken cancellationToken = default)
    {
        var note = await db.CampaignNotes
            .FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken)
            ?? throw ApiException.NotFound("La nota no existe.");
        await RequireGmAsync(note.CampaignId, userId, cancellationToken);

        db.CampaignNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    // --- Utilidades -------------------------------------------------------

    /// <summary>Garantiza membresía y rol GM u Owner (404 si no miembro, 403 si no).</summary>
    private async Task RequireGmAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);
        CampaignService.RequireRole(membership, CampaignRole.GM);
    }

    private async Task<IReadOnlyList<NoteDto>> QueryAsync(
        Guid campaignId, CancellationToken cancellationToken) =>
        await db.CampaignNotes
            .AsNoTracking()
            .Where(n => n.CampaignId == campaignId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Select(n => new NoteDto(n.Id, n.CampaignId, n.Title, n.Body, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(cancellationToken);

    private static NoteDto ToDto(CampaignNote note) => new(
        note.Id, note.CampaignId, note.Title, note.Body, note.CreatedAt, note.UpdatedAt);
}
