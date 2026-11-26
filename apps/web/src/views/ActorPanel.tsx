import { useEffect, useState, type FormEvent } from 'react';
import { ApiError, apiFetch } from '../api/client';
import type {
  ActorDto,
  ActorEffectDto,
  ActorEffectsDto,
  ActorInventoryDto,
  ActorItemDto,
  ContentItemDto,
  RollDto,
  RollResult,
} from '../api/types';
import { toCreateItemRequest, toItemPatch } from '../actors/inventory';
import { toCreateEffectRequest } from '../actors/effects';
import { rollModeLabel } from '../actors/rollView';
import { navigate } from '../routing/router';
import { t } from '../i18n';
import { ActorPortrait } from '../ui/ActorPortrait';
import {
  DND5E_ABILITIES,
  DND5E_ABILITY_LABELS,
  defaultDnd5eSheet,
  formatModifier,
  patchFromDraft,
  sheetDraftOf,
  type ActorSheetDraft,
  type Dnd5eAbility,
} from '../rules/dnd5e';

interface Props {
  campaignId: string;
  canManage: boolean;
  currentUserId: string | null;
  actors: ActorDto[];
  rolls: RollDto[];
  selectedActorId: string | null;
  items: ActorItemDto[];
  effects: ActorEffectDto[];
  onSelectActor: (id: string | null) => void;
  onActorCreated: (actor: ActorDto) => void;
  onActorUpdated: (actor: ActorDto) => void;
  onInventoryChanged: (inventory: ActorInventoryDto) => void;
  onEffectsChanged: (effects: ActorEffectsDto) => void;
  onRollResolved: (roll: RollDto) => void;
  onError: (message: string | null) => void;
}

/**
 * Ficha, inventario, efectos y tiradas de la campaña. El dado lo lanza
 * el servidor; este panel solo pide atributo, extra y CD, y el Ruleset
 * interpreta los efectos activos del actor (ADR-015). Permisos
 * (ADR-012): cualquier miembro crea su propia ficha; tirar y editar
 * (ficha, inventario o efectos) solo con las propias o, siendo GM, con
 * cualquiera.
 */
export function ActorPanel({
  campaignId,
  canManage,
  currentUserId,
  actors,
  rolls,
  selectedActorId,
  items,
  effects,
  onSelectActor,
  onActorCreated,
  onActorUpdated,
  onInventoryChanged,
  onEffectsChanged,
  onRollResolved,
  onError,
}: Props) {
  const [newName, setNewName] = useState('Personaje');
  const [extra, setExtra] = useState('0');
  const [dc, setDc] = useState('');
  const [abilityId, setAbilityId] = useState('');
  const [abilities, setAbilities] = useState<ContentItemDto[]>([]);
  const [abilityGuide, setAbilityGuide] = useState<{
    resolution: string; steps: string[];
  } | null>(null);
  const [busy, setBusy] = useState(false);
  const [draft, setDraft] = useState<ActorSheetDraft | null>(null);
  const [itemName, setItemName] = useState('');
  const [itemQty, setItemQty] = useState('1');
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [editName, setEditName] = useState('');
  const [editQty, setEditQty] = useState('');
  const [effectName, setEffectName] = useState('');
  const selected = actors.find((actor) => actor.id === selectedActorId) ?? null;

  // Cambiar de ficha descarta el borrador, el editor de ítems y el
  // formulario de efectos sin guardar.
  useEffect(() => {
    setDraft(null);
    setEditingItemId(null);
    setItemName('');
    setItemQty('1');
    setEffectName('');
  }, [selectedActorId]);

  // Habilidades publicadas del SRD (PMB-5.4): el servidor envía la guía
  // de resolución; este panel nunca aplica efectos por su cuenta.
  useEffect(() => {
    void apiFetch<ContentItemDto[]>('/api/v1/content/items?typeKey=ability')
      .then((rows) => setAbilities(rows.filter(
        (row) => row.workflowState === 'published')))
      .catch(() => setAbilities([]));
  }, []);

  useEffect(() => {
    if (!abilityId) {
      setAbilityGuide(null);
      return;
    }
    void apiFetch<{ resolution: string; steps: string[] }>(
      `/api/v1/content/items/${abilityId}/ability-guide`)
      .then(setAbilityGuide)
      .catch(() => setAbilityGuide(null));
  }, [abilityId]);

  // ADR-012: tanto para tirar como para editar hay que ser propietario o GM.
  const isOwnerOrGm = (actor: ActorDto) =>
    canManage || actor.createdByUserId === currentUserId;

  const createActor = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      const created = await apiFetch<ActorDto>(`/api/v1/campaigns/${campaignId}/actors`, {
        method: 'POST',
        body: defaultDnd5eSheet(newName),
      });
      onActorCreated(created);
      onSelectActor(created.id);
      onError(null);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo crear el actor.');
    } finally {
      setBusy(false);
    }
  };

  const rollAbility = async (attribute: Dnd5eAbility) => {
    if (!selected || !isOwnerOrGm(selected)) {
      return;
    }
    if (abilityGuide?.resolution === 'manual') {
      onError(t.habilidades.guideNote);
      return;
    }
    setBusy(true);
    try {
      const extraValue = Number(extra);
      const dcValue = dc.trim() === '' ? null : Number(dc);
      const result = await apiFetch<RollResult>(`/api/v1/campaigns/${campaignId}/rolls`, {
        method: 'POST',
        body: {
          commandId: crypto.randomUUID(),
          actorId: selected.id,
          attribute,
          extra: Number.isFinite(extraValue) ? extraValue : 0,
          dc: dcValue !== null && Number.isFinite(dcValue) ? dcValue : null,
          abilityId: abilityId && abilityGuide?.resolution === 'auto' ? abilityId : undefined,
        },
      });
      onRollResolved(result.roll);
      onError(null);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo tirar.');
    } finally {
      setBusy(false);
    }
  };

  const hp = selected?.resources.hitPoints;
  const patch = selected && draft ? patchFromDraft(selected, draft) : null;

  const updateDraft = (change: (current: ActorSheetDraft) => ActorSheetDraft) =>
    setDraft((current) => (current ? change(current) : current));

  const saveEdit = async (event: FormEvent) => {
    event.preventDefault();
    if (!selected || !draft || !patch) {
      return;
    }
    setBusy(true);
    try {
      const updated = await apiFetch<ActorDto>(`/api/v1/actors/${selected.id}`, {
        method: 'PATCH',
        body: patch,
      });
      onActorUpdated(updated);
      setDraft(null);
      onError(null);
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409) {
        // Conflicto de versión: otro cliente ya cambió la ficha. Se cierra
        // el editor para releer la versión actual (llega por ActorUpdated).
        setDraft(null);
      }
      onError(cause instanceof Error ? cause.message : 'No se pudo guardar la ficha.');
    } finally {
      setBusy(false);
    }
  };

  const addItem = async (event: FormEvent) => {
    event.preventDefault();
    if (!selected) {
      return;
    }
    const body = toCreateItemRequest(itemName, itemQty);
    if (!body) {
      onError('Revisa el nombre y la cantidad (1..9999).');
      return;
    }
    setBusy(true);
    try {
      const inventory = await apiFetch<ActorInventoryDto>(
        `/api/v1/actors/${selected.id}/items`,
        { method: 'POST', body },
      );
      onInventoryChanged(inventory);
      setItemName('');
      setItemQty('1');
      onError(null);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo añadir el ítem.');
    } finally {
      setBusy(false);
    }
  };

  const saveItemEdit = async (item: ActorItemDto) => {
    const body = toItemPatch(item, editName, editQty);
    if (!body) {
      onError('Revisa el nombre y la cantidad (1..9999).');
      return;
    }
    setBusy(true);
    try {
      const inventory = await apiFetch<ActorInventoryDto>(
        `/api/v1/actors/${item.actorId}/items/${item.id}`,
        { method: 'PATCH', body },
      );
      onInventoryChanged(inventory);
      setEditingItemId(null);
      onError(null);
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409) {
        // Conflicto de versión: se cierra el editor; el evento
        // InventoryUpdated trae el inventario actualizado.
        setEditingItemId(null);
      }
      onError(cause instanceof Error ? cause.message : 'No se pudo guardar el ítem.');
    } finally {
      setBusy(false);
    }
  };

  const deleteItem = async (item: ActorItemDto) => {
    setBusy(true);
    try {
      const inventory = await apiFetch<ActorInventoryDto>(
        `/api/v1/actors/${item.actorId}/items/${item.id}`,
        { method: 'DELETE' },
      );
      onInventoryChanged(inventory);
      setEditingItemId(null);
      onError(null);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo borrar el ítem.');
    } finally {
      setBusy(false);
    }
  };

  const applyEffect = async (event: FormEvent) => {
    event.preventDefault();
    if (!selected) {
      return;
    }
    const body = toCreateEffectRequest(effectName);
    if (!body) {
      onError('El nombre del efecto debe tener entre 1 y 100 caracteres.');
      return;
    }
    setBusy(true);
    try {
      const updated = await apiFetch<ActorEffectsDto>(
        `/api/v1/actors/${selected.id}/effects`,
        { method: 'POST', body },
      );
      onEffectsChanged(updated);
      setEffectName('');
      onError(null);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo aplicar el efecto.');
    } finally {
      setBusy(false);
    }
  };

  const removeEffect = async (effect: ActorEffectDto) => {
    setBusy(true);
    try {
      const updated = await apiFetch<ActorEffectsDto>(
        `/api/v1/actors/${effect.actorId}/effects/${effect.id}`,
        { method: 'DELETE' },
      );
      onEffectsChanged(updated);
      onError(null);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo retirar el efecto.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <aside className="scene-sheet">
      <h2>Fichas</h2>
      <form onSubmit={(event) => void createActor(event)} className="sheet-form">
        <input
          value={newName}
          onChange={(event) => setNewName(event.target.value)}
          maxLength={100}
          aria-label="Nombre del personaje"
        />
        <button type="submit" disabled={busy}>
          Crear
        </button>
      </form>
      {actors.length === 0 ? (
        <p className="muted">Aún no hay actores en esta campaña.</p>
      ) : (
        <ul className="item-list">
          {actors.map((actor) => (
            <li key={actor.id}>
              <button
                type="button"
                className={actor.id === selectedActorId ? 'item item-selected' : 'item'}
                onClick={() => onSelectActor(actor.id)}
              >
                <span>{actor.name}</span>
                <span className="badge">{actor.rulesetKey}</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {selected && (
        <section className="sheet-card">
          <header className="sheet-head">
            <ActorPortrait
              actor={selected}
              editable={isOwnerOrGm(selected)}
              onUpdated={onActorUpdated}
              className="sheet-head__portrait"
            />
            <div className="sheet-head__ident">
              <h3>{selected.name}</h3>
              <span className="sheet-head__meta">
                Ficha · {selected.rulesetKey}
              </span>
            </div>
            {!draft && isOwnerOrGm(selected) && (
              <button
                type="button"
                disabled={busy}
                onClick={() => setDraft(sheetDraftOf(selected))}
              >
                Editar ficha
              </button>
            )}
          </header>
          {draft ? (
            <form onSubmit={(event) => void saveEdit(event)}>
              <div className="sheet-form">
                <label>
                  Nombre
                  <input
                    value={draft.name}
                    maxLength={100}
                    aria-label="Nombre de la ficha"
                    onChange={(event) =>
                      updateDraft((current) => ({
                        ...current,
                        name: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <div className="ability-grid">
                {DND5E_ABILITIES.map((ability) => (
                  <label key={ability} className="ability">
                    <span className="ability-label">
                      {DND5E_ABILITY_LABELS[ability]}
                    </span>
                    <input
                      type="number"
                      step={1}
                      value={draft.attributes[ability]}
                      aria-label={DND5E_ABILITY_LABELS[ability]}
                      onChange={(event) =>
                        updateDraft((current) => ({
                          ...current,
                          attributes: {
                            ...current.attributes,
                            [ability]: event.target.value,
                          },
                        }))
                      }
                    />
                  </label>
                ))}
              </div>
              {draft.hitPoints && (
                <div className="sheet-form">
                  <label>
                    PG actual
                    <input
                      type="number"
                      step={1}
                      value={draft.hitPoints.current}
                      aria-label="Puntos de vida actuales"
                      onChange={(event) =>
                        updateDraft((current) =>
                          current.hitPoints
                            ? {
                                ...current,
                                hitPoints: {
                                  ...current.hitPoints,
                                  current: event.target.value,
                                },
                              }
                            : current,
                        )
                      }
                    />
                  </label>
                  <label>
                    PG máx
                    <input
                      type="number"
                      step={1}
                      value={draft.hitPoints.max}
                      aria-label="Puntos de vida máximos"
                      onChange={(event) =>
                        updateDraft((current) =>
                          current.hitPoints
                            ? {
                                ...current,
                                hitPoints: {
                                  ...current.hitPoints,
                                  max: event.target.value,
                                },
                              }
                            : current,
                        )
                      }
                    />
                  </label>
                </div>
              )}
              <div className="sheet-form">
                <button
                  type="submit"
                  disabled={busy || !patch}
                  title={
                    patch
                      ? 'Guardar cambios en el servidor'
                      : 'Revisa el nombre y los números'
                  }
                >
                  Guardar
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setDraft(null)}
                >
                  Cancelar
                </button>
              </div>
            </form>
          ) : (
            <>
              {hp && (
                <p className="muted">
                  PG {hp.current}/{hp.max}
                </p>
              )}
              <div className="ability-grid">
                {DND5E_ABILITIES.map((ability) => {
                  const value = selected.attributes[ability];
                  if (value === undefined) {
                    return null;
                  }
                  return (
                    <button
                      key={ability}
                      type="button"
                      className="ability"
                      disabled={busy || !isOwnerOrGm(selected)}
                      onClick={() => void rollAbility(ability)}
                      title={
                        isOwnerOrGm(selected)
                          ? `Tirar ${DND5E_ABILITY_LABELS[ability]}`
                          : 'Solo el propietario o un GM pueden tirar con este actor'
                      }
                    >
                      <span className="ability-label">
                        {DND5E_ABILITY_LABELS[ability]}
                      </span>
                      <strong>{value}</strong>
                      <span className="muted">{formatModifier(value)}</span>
                    </button>
                  );
                })}
              </div>
              <div className="sheet-form">
                <label>
                  {t.habilidades.label}
                  <select value={abilityId}
                    onChange={(event) => setAbilityId(event.target.value)}>
                    <option value="">{t.habilidades.none}</option>
                    {abilities.map((ability) => (
                      <option key={ability.id} value={ability.id}>{ability.name}</option>
                    ))}
                  </select>
                </label>
                {abilityGuide?.resolution === 'manual' && (
                  <div className="sheet-guide">
                    <strong>{t.habilidades.guide}</strong>
                    <p className="muted">{t.habilidades.guideNote}</p>
                    <ul>
                      {abilityGuide.steps.map((step, index) => (
                        <li key={`${index}-${step}`}>{step}</li>
                      ))}
                    </ul>
                  </div>
                )}
                <label>
                  Extra
                  <input
                    type="number"
                    min={-20}
                    max={20}
                    value={extra}
                    onChange={(event) => setExtra(event.target.value)}
                  />
                </label>
                <label>
                  CD
                  <input
                    type="number"
                    min={0}
                    max={100}
                    value={dc}
                    placeholder="—"
                    onChange={(event) => setDc(event.target.value)}
                  />
                </label>
              </div>
              <p className="muted">
                {isOwnerOrGm(selected)
                  ? 'Pulsa un atributo para tirar 1d20 en el servidor.'
                  : 'Solo el propietario o un GM pueden tirar con este actor.'}
              </p>
              <div className="sheet-card">
                <h4>Inventario</h4>
                <button type="button" className="pv-btn pv-btn--ghost"
                  onClick={() => navigate(`/inventario/${selected.id}`)}>
                  {t.inventario.open}
                </button>
                {items.length === 0 ? (
                  <p className="muted">Sin ítems.</p>
                ) : (
                  <ul className="item-list">
                    {items.map((item) =>
                      editingItemId === item.id ? (
                        <li key={item.id}>
                          <form
                            className="sheet-form"
                            onSubmit={(event) => {
                              event.preventDefault();
                              void saveItemEdit(item);
                            }}
                          >
                            <label>
                              Nombre
                              <input
                                value={editName}
                                maxLength={100}
                                aria-label="Nombre del ítem"
                                onChange={(event) => setEditName(event.target.value)}
                              />
                            </label>
                            <label>
                              Cant.
                              <input
                                type="number"
                                min={1}
                                max={9999}
                                value={editQty}
                                aria-label="Cantidad del ítem"
                                onChange={(event) => setEditQty(event.target.value)}
                              />
                            </label>
                            <button type="submit" disabled={busy}>
                              Guardar
                            </button>
                            <button
                              type="button"
                              disabled={busy}
                              onClick={() => setEditingItemId(null)}
                            >
                              Cancelar
                            </button>
                          </form>
                        </li>
                      ) : (
                        <li key={item.id}>
                          <div className="item">
                            <span>
                              {item.name} × {item.quantity}
                            </span>
                            {isOwnerOrGm(selected) && (
                              <span>
                                <button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => {
                                    setEditingItemId(item.id);
                                    setEditName(item.name);
                                    setEditQty(String(item.quantity));
                                  }}
                                >
                                  Editar
                                </button>
                                <button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => void deleteItem(item)}
                                >
                                  Borrar
                                </button>
                              </span>
                            )}
                          </div>
                        </li>
                      ),
                    )}
                  </ul>
                )}
                {isOwnerOrGm(selected) && (
                  <form
                    className="sheet-form"
                    onSubmit={(event) => void addItem(event)}
                  >
                    <label>
                      Nombre
                      <input
                        value={itemName}
                        maxLength={100}
                        placeholder="Ej. Antorcha"
                        aria-label="Nombre del nuevo ítem"
                        onChange={(event) => setItemName(event.target.value)}
                      />
                    </label>
                    <label>
                      Cant.
                      <input
                        type="number"
                        min={1}
                        max={9999}
                        value={itemQty}
                        aria-label="Cantidad del nuevo ítem"
                        onChange={(event) => setItemQty(event.target.value)}
                      />
                    </label>
                    <button
                      type="submit"
                      disabled={busy || !toCreateItemRequest(itemName, itemQty)}
                    >
                      Añadir
                    </button>
                  </form>
                )}
              </div>
              <div className="sheet-card">
                <h4>Efectos y condiciones</h4>
                {effects.length === 0 ? (
                  <p className="muted">Sin efectos.</p>
                ) : (
                  <ul className="item-list">
                    {effects.map((effect) => (
                      <li key={effect.id}>
                        <div className="item">
                          <span>{effect.name}</span>
                          {isOwnerOrGm(selected) && (
                            <span>
                              <button
                                type="button"
                                disabled={busy}
                                onClick={() => void removeEffect(effect)}
                              >
                                Retirar
                              </button>
                            </span>
                          )}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
                {isOwnerOrGm(selected) && (
                  <form
                    className="sheet-form"
                    onSubmit={(event) => void applyEffect(event)}
                  >
                    <label>
                      Nombre
                      <input
                        value={effectName}
                        maxLength={100}
                        placeholder="Ej. Envenenado"
                        aria-label="Nombre del efecto"
                        onChange={(event) => setEffectName(event.target.value)}
                      />
                    </label>
                    <button
                      type="submit"
                      disabled={busy || !toCreateEffectRequest(effectName)}
                    >
                      Aplicar
                    </button>
                  </form>
                )}
              </div>
            </>
          )}
        </section>
      )}

      <h3>Tiradas</h3>
      {rolls.length === 0 ? (
        <p className="muted">Nadie ha tirado todavía.</p>
      ) : (
        <ul className="roll-list">
          {rolls.slice(0, 12).map((entry) => {
            const actor = actors.find((item) => item.id === entry.actorId);
            const label =
              DND5E_ABILITY_LABELS[entry.attribute as Dnd5eAbility] ?? entry.attribute;
            const outcome =
              entry.breakdown.success === true
                ? ' superada'
                : entry.breakdown.success === false
                  ? ' no superada'
                  : '';
            const mode = rollModeLabel(entry.breakdown.rollMode);
            return (
              <li key={entry.rollId}>
                <strong>{entry.breakdown.total}</strong>
                <span>
                  {actor?.name ?? 'Actor'} · {label}
                  {outcome}
                </span>
                <span className="muted">
                  {mode ? `${mode} · ` : ''}
                  {entry.breakdown.formula}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </aside>
  );
}
