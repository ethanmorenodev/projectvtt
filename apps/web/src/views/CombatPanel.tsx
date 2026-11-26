import { useState, type FormEvent } from 'react';
import { apiFetch } from '../api/client';
import './panels.css';
import type { CombatStateDto } from '../api/types';
import { isCurrentTurn, toCreateCombatantRequest } from '../combat/combat';

interface Props {
  sceneId: string;
  canManage: boolean;
  combat: CombatStateDto;
  /** Tokens del mapa (solo id y nombre, para el vínculo opcional). */
  tokens: Array<{ id: string; name: string }>;
  onCombatChanged: (state: CombatStateDto) => void;
  onError: (message: string) => void;
}

/**
 * Combate e iniciativa de la escena (Épica F, ADR-018): tercer aside con
 * el orden de turno (iniciativa descendente, turno actual destacado) y
 * los controles de GM+ (iniciar, añadir/quitar combatientes, avanzar
 * turno y terminar). Los jugadores ven el estado en vivo por
 * `CombatUpdated`; los cambios solo los hace el servidor.
 */
export function CombatPanel({
  sceneId,
  canManage,
  combat,
  tokens,
  onCombatChanged,
  onError,
}: Props) {
  const [name, setName] = useState('');
  const [initiative, setInitiative] = useState('');
  const [tokenId, setTokenId] = useState('');

  const post = async (path: string, method: 'POST' | 'DELETE' = 'POST') => {
    try {
      const state = await apiFetch<CombatStateDto>(path, { method });
      onCombatChanged(state);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo actualizar el combate.');
    }
  };

  const addCombatant = async (event: FormEvent) => {
    event.preventDefault();
    const request = toCreateCombatantRequest(name, initiative, tokenId || null);
    if (!request) {
      return;
    }

    try {
      const state = await apiFetch<CombatStateDto>(
        `/api/v1/scenes/${sceneId}/combat/combatants`,
        { method: 'POST', body: request },
      );
      onCombatChanged(state);
      setName('');
      setInitiative('');
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo añadir el combatiente.');
    }
  };

  const request = toCreateCombatantRequest(name, initiative, tokenId || null);

  return (
    <aside className="scene-sheet">
      <h3>Combate</h3>
      {!combat.active ? (
        <>
          <p className="muted">No hay combate en curso.</p>
          {canManage && (
            <form
              onSubmit={(event) => {
                event.preventDefault();
                void post(`/api/v1/scenes/${sceneId}/combat/start`);
              }}
              className="sheet-form"
            >
              <button type="submit">Iniciar combate</button>
            </form>
          )}
        </>
      ) : (
        <>
          <p className="badge">Ronda {combat.round}</p>
          {combat.combatants.length === 0 ? (
            <p className="muted">Sin combatientes todavía.</p>
          ) : (
            <ul className="roll-list">
              {combat.combatants.map((combatant) => (
                <li key={combatant.id}>
                  <strong>
                    {combatant.name}
                    {isCurrentTurn(combat, combatant) ? ' · turno actual' : ''}
                  </strong>
                  <span className="muted">Iniciativa {combatant.initiative}</span>
                  {canManage && (
                    <button
                      type="button"
                      onClick={() =>
                        void post(
                          `/api/v1/scenes/${sceneId}/combat/combatants/${combatant.id}`,
                          'DELETE',
                        )
                      }
                    >
                      Quitar
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
          {canManage && (
            <>
              <form onSubmit={(event) => void addCombatant(event)} className="sheet-form">
                <input
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  maxLength={100}
                  aria-label="Nombre del combatiente"
                />
                <input
                  type="number"
                  min={-100}
                  max={100}
                  value={initiative}
                  onChange={(event) => setInitiative(event.target.value)}
                  aria-label="Iniciativa"
                />
                <select
                  value={tokenId}
                  onChange={(event) => setTokenId(event.target.value)}
                  aria-label="Token vinculado"
                >
                  <option value="">Sin token</option>
                  {tokens.map((token) => (
                    <option key={token.id} value={token.id}>
                      {token.name}
                    </option>
                  ))}
                </select>
                <button type="submit" disabled={!request}>
                  Añadir
                </button>
              </form>
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  void post(`/api/v1/scenes/${sceneId}/combat/advance`);
                }}
                className="sheet-form"
              >
                <button type="submit">Siguiente turno</button>
                <button
                  type="button"
                  onClick={() => void post(`/api/v1/scenes/${sceneId}/combat/end`)}
                >
                  Terminar
                </button>
              </form>
            </>
          )}
        </>
      )}
    </aside>
  );
}
