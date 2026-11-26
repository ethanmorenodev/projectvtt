import { useEffect, useState, type FormEvent } from 'react';
import { apiFetch } from '../api/client';
import './panels.css';
import type { NoteDto } from '../api/types';
import {
  toCreateNoteRequest,
  toUpdateNoteRequest,
  upsertNote,
  removeNote,
} from '../notes/notes';

interface Props {
  campaignId: string;
}

/**
 * Notas privadas del GM (Épica F, ADR-019): lista editable de la página
 * de campaña, visible solo para GM u Owner (el servidor lo vuelve a
 * validar). Sin evento en vivo (la página no tiene hub): cada cambio se
 * refleja con la respuesta de la API.
 */
export function NotesPanel({ campaignId }: Props) {
  const [notes, setNotes] = useState<NoteDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newTitle, setNewTitle] = useState('');
  const [newBody, setNewBody] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const loaded = await apiFetch<NoteDto[]>(`/api/v1/campaigns/${campaignId}/notes`);
        if (!cancelled) {
          setNotes(loaded);
        }
      } catch (cause) {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : 'No se pudieron cargar las notas.');
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [campaignId]);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    const request = toCreateNoteRequest(newTitle, newBody);
    if (!request) {
      return;
    }

    setBusy(true);
    try {
      const created = await apiFetch<NoteDto>(`/api/v1/campaigns/${campaignId}/notes`, {
        method: 'POST',
        body: request,
      });
      setNotes((current) => upsertNote(current ?? [], created));
      setNewTitle('');
      setNewBody('');
      setError(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo crear la nota.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="sheet-card">
      <h3>Notas privadas del GM</h3>
      {error && <p className="error">{error}</p>}
      <form onSubmit={(event) => void create(event)} className="row">
        <input
          value={newTitle}
          onChange={(event) => setNewTitle(event.target.value)}
          placeholder="Título"
          maxLength={100}
          aria-label="Título de la nota"
        />
        <button type="submit" disabled={busy || !toCreateNoteRequest(newTitle, newBody)}>
          Crear nota
        </button>
      </form>
      <textarea
        value={newBody}
        onChange={(event) => setNewBody(event.target.value)}
        placeholder="Cuerpo de la nota"
        rows={3}
        aria-label="Cuerpo de la nota"
      />
      {notes === null ? (
        <p>Cargando…</p>
      ) : notes.length === 0 ? (
        <p className="muted">Aún no hay notas.</p>
      ) : (
        <ul className="item-list">
          {notes.map((note) => (
            <NoteRow
              key={note.id}
              note={note}
              onSaved={(updated) => setNotes((current) => upsertNote(current ?? [], updated))}
              onDeleted={(id) => setNotes((current) => removeNote(current ?? [], id))}
              onError={setError}
            />
          ))}
        </ul>
      )}
    </section>
  );
}

interface RowProps {
  note: NoteDto;
  onSaved: (note: NoteDto) => void;
  onDeleted: (noteId: string) => void;
  onError: (message: string) => void;
}

/** Fila editable de una nota: título y cuerpo con Guardar/Borrar. */
function NoteRow({ note, onSaved, onDeleted, onError }: RowProps) {
  const [title, setTitle] = useState(note.title);
  const [body, setBody] = useState(note.body);
  const [busy, setBusy] = useState(false);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const request = toUpdateNoteRequest(title, body);
    if (!request) {
      return;
    }

    setBusy(true);
    try {
      onSaved(
        await apiFetch<NoteDto>(`/api/v1/notes/${note.id}`, {
          method: 'PATCH',
          body: request,
        }),
      );
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo guardar la nota.');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    try {
      await apiFetch<NoteDto>(`/api/v1/notes/${note.id}`, { method: 'DELETE' });
      onDeleted(note.id);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo borrar la nota.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <li>
      <form onSubmit={(event) => void save(event)}>
        <input
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          maxLength={100}
          aria-label={`Título de ${note.title}`}
        />
        <textarea
          value={body}
          onChange={(event) => setBody(event.target.value)}
          rows={3}
          aria-label={`Cuerpo de ${note.title}`}
        />
        <div className="row">
          <button type="submit" disabled={busy || !toUpdateNoteRequest(title, body)}>
            Guardar
          </button>
          <button type="button" disabled={busy} onClick={() => void remove()}>
            Borrar
          </button>
        </div>
      </form>
    </li>
  );
}
