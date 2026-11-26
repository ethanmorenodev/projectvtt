import { describe, expect, it } from 'vitest';
import type { NoteDto } from '../api/types';
import {
  MAX_NOTE_BODY_LENGTH,
  MAX_NOTE_TITLE_LENGTH,
  removeNote,
  toCreateNoteRequest,
  toUpdateNoteRequest,
  upsertNote,
} from './notes';

const noteFixture = (overrides: Partial<NoteDto> = {}): NoteDto => ({
  id: 'note-1',
  campaignId: 'campaign-1',
  title: 'Sesión 3',
  body: 'El dragón espera.',
  createdAt: '2026-11-09T00:00:00Z',
  updatedAt: '2026-11-09T00:00:00Z',
  ...overrides,
});

describe('lista de notas', () => {
  it('upsertNote añade al principio y sustituye la existente', () => {
    const first = upsertNote([], noteFixture());
    expect(first).toHaveLength(1);

    const withSecond = upsertNote(first, noteFixture({ id: 'note-2', title: 'Otra' }));
    expect(withSecond.map((item) => item.id)).toEqual(['note-2', 'note-1']);

    const updated = upsertNote(
      withSecond,
      noteFixture({ title: 'Sesión 3 (editada)', updatedAt: '2026-11-09T01:00:00Z' }),
    );
    expect(updated).toHaveLength(2);
    expect(updated.find((item) => item.id === 'note-1')?.title).toBe('Sesión 3 (editada)');
  });

  it('removeNote quita solo la indicada', () => {
    const current = [noteFixture(), noteFixture({ id: 'note-2' })];
    const next = removeNote(current, 'note-1');
    expect(next.map((item) => item.id)).toEqual(['note-2']);
    // Inmutable.
    expect(current).toHaveLength(2);
  });
});

describe('peticiones de notas', () => {
  it('toCreateNoteRequest recorta el título y acepta cuerpo vacío', () => {
    expect(toCreateNoteRequest('  Sesión  ', '')).toEqual({ title: 'Sesión', body: '' });
  });

  it('toCreateNoteRequest rechaza títulos vacíos y campos demasiado largos', () => {
    expect(toCreateNoteRequest('   ', 'cuerpo')).toBeNull();
    expect(toCreateNoteRequest('x'.repeat(MAX_NOTE_TITLE_LENGTH + 1), '')).toBeNull();
    expect(toCreateNoteRequest('Nota', 'x'.repeat(MAX_NOTE_BODY_LENGTH + 1))).toBeNull();
    expect(toCreateNoteRequest('Nota', 'x'.repeat(MAX_NOTE_BODY_LENGTH))).toEqual({
      title: 'Nota',
      body: 'x'.repeat(MAX_NOTE_BODY_LENGTH),
    });
  });

  it('toUpdateNoteRequest sigue las mismas reglas', () => {
    expect(toUpdateNoteRequest('  Editada  ', 'nuevo')).toEqual({
      title: 'Editada',
      body: 'nuevo',
    });
    expect(toUpdateNoteRequest('', 'nuevo')).toBeNull();
  });
});
