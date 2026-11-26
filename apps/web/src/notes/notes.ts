/**
 * Notas privadas del GM (Épica F, ADR-019): sanidad de los formularios y
 * utilidades de la lista. El servidor revalida permisos (solo GM u
 * Owner) y campos; aquí solo se evita enviar basura.
 */

import type { NoteDto } from '../api/types';

/** Límites de sanidad; el servidor valida lo mismo (ADR-019). */
export const MAX_NOTE_TITLE_LENGTH = 100;
export const MAX_NOTE_BODY_LENGTH = 5000;

/** Añade la nota nueva (o la sustituye si ya estaba) en la lista. */
export function upsertNote(current: NoteDto[], note: NoteDto): NoteDto[] {
  return current.some((item) => item.id === note.id)
    ? current.map((item) => (item.id === note.id ? note : item))
    : [note, ...current];
}

/** Quita la nota de la lista, sin tocar el resto. */
export function removeNote(current: NoteDto[], noteId: string): NoteDto[] {
  return current.filter((item) => item.id !== noteId);
}

/**
 * Cuerpo de `POST /api/v1/campaigns/{id}/notes`. Devuelve `null` si el
 * título queda vacío o algún campo supera su límite.
 */
export function toCreateNoteRequest(
  title: string,
  body: string,
): { title: string; body: string } | null {
  const trimmed = title.trim();
  if (trimmed.length < 1 || trimmed.length > MAX_NOTE_TITLE_LENGTH) {
    return null;
  }
  if (body.length > MAX_NOTE_BODY_LENGTH) {
    return null;
  }
  return { title: trimmed, body };
}

/**
 * Cuerpo de `PATCH /api/v1/notes/{id}` con ambos campos; mismas reglas
 * que el alta.
 */
export function toUpdateNoteRequest(
  title: string,
  body: string,
): { title: string; body: string } | null {
  return toCreateNoteRequest(title, body);
}
