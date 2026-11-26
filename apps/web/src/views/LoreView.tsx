import { useEffect, useState } from 'react'
import { apiFetch } from '../api/client'
import { t } from '../i18n'
import type { LoreEventDto, LoreGraphDto } from '../api/types'
import { Button } from '../ui'
import { useNavigate } from '../routing/router'
import '../views/library.css'

/** Lore del mundo (PMB-7.3): grafo de relaciones y cronología de eventos. */
export function LoreView() {
  const go = useNavigate()
  const [graph, setGraph] = useState<LoreGraphDto | null>(null)
  const [events, setEvents] = useState<LoreEventDto[] | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void apiFetch<LoreGraphDto>('/api/v1/content/lore/graph')
      .then(setGraph).catch((cause) => setError(String(cause)))
    void apiFetch<LoreEventDto[]>('/api/v1/content/lore/timeline')
      .then(setEvents).catch((cause) => setError(String(cause)))
  }, [])

  const selected = graph?.nodes.find((node) => node.id === selectedId) ?? null
  const edges = graph?.edges.filter((edge) =>
    edge.fromItemId === selectedId || edge.toItemId === selectedId) ?? []
  const nameOf = (id: string) => graph?.nodes.find((node) => node.id === id)?.name ?? id

  return (
    <div className="biblio">
      <header className="img-biblio__head">
        <div>
          <h1 className="biblio__title">{t.lore.title}</h1>
          <p className="biblio__subtitle">{t.lore.subtitle}</p>
        </div>
      </header>
      {error && <p className="biblio-status biblio-status--error" role="alert">{error}</p>}

      {graph !== null && events !== null && graph.nodes.length === 0 && events.length === 0 ? (
        <div className="first-time first-time--wide" role="note">
          <img src="/icons/family-tree.svg" alt="" aria-hidden="true" />
          <div>
            <h3>{t.lore.firstTitle}</h3>
            <p>{t.lore.firstText}</p>
            <Button variant="primary" onClick={() => go({ kind: 'biblioteca' })}>
              {t.lore.firstAction}
            </Button>
          </div>
        </div>
      ) : null}

      <div className="img-biblio__body">
        <section className="img-biblio__grid" aria-label={t.lore.graph}>
          <h2 className="biblio-zone__title">{t.lore.graph}</h2>
          {graph === null ? (
            <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
          ) : graph.nodes.length === 0 ? (
            <p className="biblio-inspector__empty">{t.lore.empty}</p>
          ) : (
            <>
              <p className="biblio-toolbar__count">{t.lore.nodes(graph.nodes.length)}</p>
              <ul className="img-grid__tiles">
                {graph.nodes.map((node) => (
                  <li key={node.id}>
                    <button type="button"
                      className={`img-card ${selectedId === node.id ? 'is-selected' : ''}`}
                      onClick={() => setSelectedId(node.id)}>
                      <span className="img-card__name">{node.name}</span>
                      <span className="img-card__meta">
                        <span className="biblio-tag">{node.typeKey}</span>
                        {node.isPrivate && <span className="biblio-tag">DM</span>}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>

        <section className="img-biblio__inspector" aria-label={t.lore.timeline}>
          <h2 className="biblio-zone__title">{t.lore.timeline}</h2>
          {events === null ? (
            <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
          ) : events.length === 0 ? (
            <p className="biblio-inspector__empty">{t.lore.timelineEmpty}</p>
          ) : (
            <>
              <p className="biblio-toolbar__count">{t.lore.events(events.length)}</p>
              <ul className="biblio-revisions__list">
                {events.map((event) => (
                  <li key={event.id}>
                    <strong>{event.name}</strong>
                    {event.happenedAt && <span> · {event.happenedAt}</span>}
                    {event.summary && <span className="biblio-inspector__note">{event.summary}</span>}
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>

        <aside className="img-biblio__inspector" aria-label={t.lore.detail}>
          <h2 className="biblio-zone__title">{t.lore.detail}</h2>
          {!selected ? (
            <p className="biblio-inspector__empty">{t.biblioteca.selectHint}</p>
          ) : (
            <div className="biblio-inspector__body">
              <h3 className="biblio-inspector__name">{selected.name}</h3>
              <p className="biblio-inspector__summary">{selected.summary}</p>
              <h4 className="biblio-inspector__section">{t.lore.graph}</h4>
              {edges.length === 0 ? (
                <p className="biblio-inspector__note">—</p>
              ) : (
                <ul className="img-meta">
                  {edges.map((edge) => (
                    <li key={edge.id}>
                      {edge.fromItemId === selected.id ? '→' : '←'}{' '}
                      {nameOf(edge.fromItemId === selected.id ? edge.toItemId : edge.fromItemId)}{' '}
                      <span className="biblio-tag">{edge.kind}</span>
                      {edge.label ? ` · ${edge.label}` : ''}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </aside>
      </div>
    </div>
  )
}
