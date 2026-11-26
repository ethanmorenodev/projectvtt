import { useEffect, useState } from 'react'
import { apiFetch } from '../api/client'
import type {
  ActorDto,
  ActorEffectDto,
  ActorItemDto,
  ContentItemDto,
  ContentRelationDto,
  HistoryEntryDto,
  ItemUsageDto,
} from '../api/types'
import { historyLabel, historyTime } from '../history/history'
import {
  DND5E_ABILITIES,
  DND5E_ABILITY_LABELS,
  formatModifier,
} from '../rules/dnd5e'
import type { Dnd5eAbility } from '../rules/dnd5e'
import { t } from '../i18n'
import { ActorPortrait } from '../ui/ActorPortrait'
import './character-hub.css'

/**
 * Character Hub (PMB-3.7, CHARACTERS.md §3): ventana tipo menú de RPG
 * sincronizada con la ficha clásica — ambos comparten el mismo estado y
 * eventos (una sola verdad). Secciones: Perfil (datos reales del Actor),
 * Equipo (inventario y efectos reales), Relaciones (reservada: nace de
 * `ContentRelation`) e Historia (cronología real de la campaña + sección
 * «Solo DM» que **solo se renderiza para el DM**; el filtrado del servidor
 * es la segunda barrera, PMB-3.8).
 */

interface Props {
  campaignId: string
  canManage: boolean
  actors: ActorDto[]
  selectedActorId: string | null
  items: ActorItemDto[]
  effects: ActorEffectDto[]
  onSelectActor: (id: string | null) => void
  onActorUpdated?: (actor: ActorDto) => void
}

type Tab = 'perfil' | 'equipo' | 'relaciones' | 'historia'

const TABS: Array<{ id: Tab; label: string }> = [
  { id: 'perfil', label: t.hub.tabs.perfil },
  { id: 'equipo', label: t.hub.tabs.equipo },
  { id: 'relaciones', label: t.hub.tabs.relaciones },
  { id: 'historia', label: t.hub.tabs.historia },
]

export function CharacterHub({
  campaignId,
  canManage,
  actors,
  selectedActorId,
  items,
  effects,
  onSelectActor,
  onActorUpdated,
}: Props) {
  const [tab, setTab] = useState<Tab>('perfil')
  const [history, setHistory] = useState<HistoryEntryDto[]>([])
  const [relations, setRelations] = useState<ContentRelationDto[] | null>(null)
  const [relationsError, setRelationsError] = useState(false)
  const [sourceIds, setSourceIds] = useState<string[]>([])
  const [itemNames, setItemNames] = useState<Record<string, string>>({})
  const actor = actors.find((entry) => entry.id === selectedActorId) ?? null

  useEffect(() => {
    let cancelled = false
    void apiFetch<HistoryEntryDto[]>(`/api/v1/campaigns/${campaignId}/history`)
      .then((entries) => {
        if (!cancelled) {
          setHistory(Array.isArray(entries) ? entries.slice(0, 12) : [])
        }
      })
      .catch(() => {
        if (!cancelled) {
          setHistory([])
        }
      })
    return () => {
      cancelled = true
    }
  }, [campaignId])

  // Relaciones tipadas: el servidor ya filtra por visibilidad. Se parten de
  // los usos de contenido del actor (raza/clase/objetos fijados por el
  // creador) y se consultan sus relaciones entrantes y salientes.
  useEffect(() => {
    if (tab !== 'relaciones' || !selectedActorId) return
    let cancelled = false
    setRelations(null)
    setRelationsError(false)
    void (async () => {
      try {
        const [usages, items] = await Promise.all([
          apiFetch<ItemUsageDto[]>(`/api/v1/actors/${selectedActorId}/usages`),
          apiFetch<ContentItemDto[]>('/api/v1/content/items'),
        ])
        if (cancelled) return
        setItemNames(Object.fromEntries(items.map((item) => [item.id, item.name])))
        const ids = [...new Set(usages.map((usage) => usage.itemId))]
        setSourceIds(ids)
        const groups = await Promise.all(ids.map((id) =>
          apiFetch<ContentRelationDto[]>(`/api/v1/content/items/${id}/relations`)
            .catch(() => [] as ContentRelationDto[])))
        if (cancelled) return
        const seen = new Set<string>()
        setRelations(groups.flat().filter((relation) => {
          if (seen.has(relation.id)) return false
          seen.add(relation.id)
          return true
        }))
      } catch {
        if (!cancelled) {
          setRelations([])
          setRelationsError(true)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [tab, selectedActorId])

  if (!actor) {
    return <p className="hub-empty">{t.hub.noSelection}</p>
  }

  const kindLabel =
    (t.hub.kinds as Record<string, string>)[actor.kind] ?? actor.kind
  const hitPoints = actor.resources.hitPoints
  const relationName = (id: string) => itemNames[id] ?? id.slice(0, 8)
  const outRelations = relations?.filter((relation) => sourceIds.includes(relation.fromItemId)) ?? []
  const inRelations = relations?.filter((relation) => sourceIds.includes(relation.toItemId)) ?? []

  return (
    <div className="hub">
      <header className="hub__head">
        <ActorPortrait
          actor={actor}
          editable={canManage}
          onUpdated={onActorUpdated}
          className="hub__portrait"
        />
        <div className="hub__ident">
          <h1 className="hub__name">{actor.name}</h1>
          <span className="hub__meta">{t.hub.identity(kindLabel, actor.level)}</span>
        </div>
        <select
          className="pv-input hub__switch"
          aria-label={t.hub.switchLabel}
          value={actor.id}
          onChange={(event) => onSelectActor(event.target.value || null)}
        >
          {actors.map((entry) => (
            <option key={entry.id} value={entry.id}>
              {entry.name}
            </option>
          ))}
        </select>
        <nav className="hub__tabs" aria-label={t.hub.title}>
          {TABS.map((entry) => (
            <button
              key={entry.id}
              type="button"
              className={`hub__tab${tab === entry.id ? ' hub__tab--on' : ''}`}
              aria-current={tab === entry.id ? 'page' : undefined}
              onClick={() => setTab(entry.id)}
            >
              {entry.label}
            </button>
          ))}
        </nav>
      </header>

      <div className="hub__body">
        {tab === 'perfil' ? (
          <div className="hub-cols">
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.atributos}</h2>
              <div className="hub-attrs">
                {DND5E_ABILITIES.map((ability) => {
                  const score = actor.attributes[ability] ?? 10
                  return (
                    <div key={ability} className="hub-attr">
                      <strong>{score}</strong>
                      <span>{DND5E_ABILITY_LABELS[ability as Dnd5eAbility] ?? ability}</span>
                      <span className="hub-attr__mod">{formatModifier(score)}</span>
                    </div>
                  )
                })}
              </div>
            </section>
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.estado}</h2>
              {hitPoints ? (
                <ul className="hub-list">
                  <li>{t.hub.hitPoints(hitPoints.current, hitPoints.max)}</li>
                </ul>
              ) : (
                <p className="hub-empty">{t.hub.noResources}</p>
              )}
              <h2 className="hub-panel__title">{t.hub.conditions}</h2>
              {effects.length > 0 ? (
                <ul className="hub-list">
                  {effects.map((effect) => (
                    <li key={effect.id}>{effect.name}</li>
                  ))}
                </ul>
              ) : (
                <p className="hub-hint">{t.hub.noConditions}</p>
              )}
            </section>
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.personality}</h2>
              <p className="hub-text">
                {actor.personality || actor.biography || t.hub.noPersonality}
              </p>
            </section>
          </div>
        ) : null}

        {tab === 'equipo' ? (
          <div className="hub-cols">
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.mochila}</h2>
              {items.length > 0 ? (
                <ul className="hub-list">
                  {items.map((item) => (
                    <li key={item.id}>
                      {item.name} ×{item.quantity}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="hub-empty">{t.hub.emptyBag}</p>
              )}
            </section>
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.tabs.equipo}</h2>
              <p className="hub-hint">{t.hub.equipmentNote}</p>
            </section>
          </div>
        ) : null}

        {tab === 'relaciones' ? (
          <div className="hub-cols">
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.relacionesTitle}</h2>
              {relations === null ? (
                <p className="hub-empty">{t.hub.relacionesLoading}</p>
              ) : relationsError ? (
                <p className="hub-empty">{t.hub.relacionesError}</p>
              ) : relations.length === 0 ? (
                <p className="hub-empty">{t.hub.relacionesEmpty}</p>
              ) : (
                <div className="hub-relations">
                  {outRelations.length > 0 ? (
                    <ul className="hub-list">
                      {outRelations.map((relation) => (
                        <li key={relation.id}>
                          <strong>{(t.biblioteca.relationKinds as Record<string, string>)[relation.kind] ?? relation.kind}</strong>
                          {' → '}{relationName(relation.toItemId)}
                          {relation.label ? ` · ${relation.label}` : ''}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                  {inRelations.length > 0 ? (
                    <ul className="hub-list">
                      {inRelations.map((relation) => (
                        <li key={relation.id}>
                          <strong>{(t.biblioteca.relationKinds as Record<string, string>)[relation.kind] ?? relation.kind}</strong>
                          {' ← '}{relationName(relation.fromItemId)}
                          {relation.label ? ` · ${relation.label}` : ''}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </div>
              )}
            </section>
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.relacionesSource}</h2>
              <p className="hub-hint">{t.hub.relacionesHint}</p>
            </section>
          </div>
        ) : null}

        {tab === 'historia' ? (
          <div className="hub-history">
            <section className="hub-panel">
              <h2 className="hub-panel__title">{t.hub.cronologia}</h2>
              {history.length > 0 ? (
                <ul className="hub-timeline">
                  {history.map((entry) => (
                    <li key={entry.id}>
                      <strong>{historyTime(entry.createdAt)}</strong>
                      <span>
                        {historyLabel(entry.kind)} — {entry.summary}
                      </span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="hub-empty">{t.hub.noTimeline}</p>
              )}
            </section>
            {canManage ? (
              <section className="hub-panel">
                <h2 className="hub-panel__title">
                  {t.hub.dmLore} <span className="hub-badge">{t.hub.dmOnly}</span>
                </h2>
                <p className="hub-hint">{t.hub.dmLoreEmpty}</p>
              </section>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  )
}
