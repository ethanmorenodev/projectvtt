import { useEffect, useState } from 'react';
import { apiFetch } from '../api/client';
import './panels.css';
import type { HistoryEntryDto } from '../api/types';
import { historyLabel, historyTime } from '../history/history';

interface Props {
  campaignId: string;
}

/**
 * Historial de cambios (Épica F, ADR-021): lista de solo lectura de la
 * página de campaña, visible para todos los miembros. Las entradas las
 * escribe el servidor (sin notas ni mensajes privados); sin evento en
 * vivo: la página no tiene hub, se refresca al volver a entrar.
 */
export function HistoryPanel({ campaignId }: Props) {
  const [entries, setEntries] = useState<HistoryEntryDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const loaded = await apiFetch<HistoryEntryDto[]>(
          `/api/v1/campaigns/${campaignId}/history`,
        );
        if (!cancelled) {
          setEntries(loaded);
        }
      } catch (cause) {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : 'No se pudo cargar el historial.');
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [campaignId]);

  return (
    <section className="sheet-card">
      <h3>Historial de cambios</h3>
      {error && <p className="error">{error}</p>}
      {entries === null ? (
        <p>Cargando…</p>
      ) : entries.length === 0 ? (
        <p className="muted">Todavía no hay cambios registrados.</p>
      ) : (
        <ul className="roll-list">
          {entries.map((entry) => (
            <li key={entry.id}>
              <strong>{entry.userName}</strong>
              <span>{entry.summary}</span>
              <span className="muted">
                {historyTime(entry.createdAt)} · {historyLabel(entry.kind)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
