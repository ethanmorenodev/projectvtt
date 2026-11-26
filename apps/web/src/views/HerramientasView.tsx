import { useEffect, useState } from 'react'
import { apiFetch } from '../api/client'
import type { ActorDto } from '../api/types'
import { t } from '../i18n'
import { useNavigate } from '../routing/router'
import type { ParsedRoute } from '../routing/router'
import './herramientas.css'

/**
 * Índice de herramientas (Fase A del rediseño de distribución): agrupa en un
 * solo lugar las rutas que antes solo se alcanzaban desde botones internos de
 * otras vistas. No añade funcionalidad: cada tarjeta navega a una ruta ya
 * existente y el servidor sigue validando permisos.
 */
interface HerramientasViewProps {
  campaignId?: string
  /** Personaje ya seleccionado en otra vista, si se conoce. */
  actorId?: string
}

export function HerramientasView({ campaignId, actorId }: HerramientasViewProps) {
  const go = useNavigate()
  const items = t.herramientas.items
  const [actors, setActors] = useState<ActorDto[]>([])
  const [selectedActorId, setSelectedActorId] = useState(actorId ?? '')

  // El inventario necesita un actor real: se elige de la campaña activa y el
  // servidor valida el acceso al abrir la ruta.
  useEffect(() => {
    setSelectedActorId(actorId ?? '')
    if (!campaignId) {
      setActors([])
      return
    }
    let cancelled = false
    void apiFetch<ActorDto[]>(`/api/v1/campaigns/${campaignId}/actors`)
      .then((loaded) => {
        if (!cancelled) setActors(Array.isArray(loaded) ? loaded : [])
      })
      .catch(() => {
        if (!cancelled) setActors([])
      })
    return () => {
      cancelled = true
    }
  }, [campaignId, actorId])

  const inventoryRoute: ParsedRoute | null = selectedActorId
    ? { kind: 'inventario', actorId: selectedActorId }
    : null

  const groups: Array<{
    key: keyof typeof t.herramientas.groups
    cards: Array<{
      icon: string
      title: string
      text: string
      route: ParsedRoute | null
      actorPicker?: boolean
    }>
  }> = [
    {
      key: 'direccion',
      cards: [
        { icon: 'riot-shield', ...items.dm, route: { kind: 'dm' } },
        { icon: 'family-tree', ...items.lore, route: { kind: 'lore' } },
      ],
    },
    {
      key: 'recursos',
      cards: [
        { icon: 'gaze', ...items.imagenes, route: { kind: 'imagenes' } },
        { icon: 'ankh', ...items.creditos, route: { kind: 'creditos' } },
      ],
    },
    {
      key: 'personajes',
      cards: [
        {
          icon: 'woman-elf-face',
          ...items.creador,
          route: campaignId
            ? { kind: 'character-create', campaignId }
            : { kind: 'campaigns' },
        },
        { icon: 'wizard-face', ...items.clase, route: { kind: 'clase-crear' } },
        {
          icon: 'chest',
          ...items.inventario,
          route: inventoryRoute,
          actorPicker: true,
        },
      ],
    },
    {
      key: 'configuracion',
      cards: [
        {
          icon: 'cog',
          ...items.gestion,
          route: campaignId
            ? { kind: 'campaign', campaignId }
            : { kind: 'campaigns' },
        },
        { icon: 'gears', ...items.audio, route: { kind: 'ajustes' } },
      ],
    },
  ]

  return (
    <div className="herramientas">
      <header className="herramientas__head">
        <h1 className="biblio__title">{t.herramientas.title}</h1>
        <p className="biblio__subtitle">{t.herramientas.subtitle}</p>
      </header>

      {groups.map((group) => (
        <section key={group.key} aria-label={t.herramientas.groups[group.key]}>
          <h2 className="biblio-zone__title">{t.herramientas.groups[group.key]}</h2>
          <div className="herramientas__cards">
            {group.cards.map((card) => (
              <article className="herramientas__card" key={card.title}>
                <img src={`/icons/${card.icon}.svg`} alt="" aria-hidden="true" />
                <strong>{card.title}</strong>
                <p>{card.text}</p>
                {card.actorPicker && (
                  <select
                    className="pv-input"
                    aria-label={t.inventario.title}
                    value={selectedActorId}
                    disabled={actors.length === 0}
                    onChange={(event) => setSelectedActorId(event.target.value)}
                  >
                    <option value="">—</option>
                    {actors.map((actor) => (
                      <option key={actor.id} value={actor.id}>
                        {actor.name}
                      </option>
                    ))}
                  </select>
                )}
                <button type="button" className="pv-btn pv-btn--ghost herramientas__open"
                  aria-label={`${t.herramientas.open}: ${card.title}`}
                  disabled={card.route === null}
                  title={card.route === null ? card.text : undefined}
                  onClick={() => {
                    if (card.route) go(card.route)
                  }}>
                  <span aria-hidden="true">→</span>
                </button>
              </article>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
