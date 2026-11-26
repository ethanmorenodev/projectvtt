using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Notes;

public static class NoteEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Notes")
            .RequireAuthorization();

        group.MapGet("/campaigns/{campaignId:guid}/notes", ListNotesAsync);
        group.MapPost("/campaigns/{campaignId:guid}/notes", CreateNoteAsync);
        group.MapPatch("/notes/{noteId:guid}", UpdateNoteAsync);
        group.MapDelete("/notes/{noteId:guid}", DeleteNoteAsync);

        return endpoints;
    }

    private static async Task<IResult> ListNotesAsync(
        HttpContext http, Guid campaignId, NoteService notes, CancellationToken cancellationToken) =>
        Results.Ok(await notes.ListAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> CreateNoteAsync(
        HttpContext http, Guid campaignId, CreateNoteRequest request, NoteService notes,
        CancellationToken cancellationToken) =>
        Results.Created(
            $"/api/v1/campaigns/{campaignId}/notes",
            await notes.CreateAsync(campaignId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> UpdateNoteAsync(
        HttpContext http, Guid noteId, UpdateNoteRequest request, NoteService notes,
        CancellationToken cancellationToken) =>
        Results.Ok(await notes.UpdateAsync(noteId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> DeleteNoteAsync(
        HttpContext http, Guid noteId, NoteService notes, CancellationToken cancellationToken) =>
        Results.Ok(await notes.DeleteAsync(noteId, GetUserId(http), cancellationToken));

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
