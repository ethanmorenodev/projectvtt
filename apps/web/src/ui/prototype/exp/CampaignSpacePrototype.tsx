import { useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { Badge } from '../../Badge'
import { EmptyState } from '../../EmptyState'
import { List, ListRow } from '../../List'
import './campaignSpacePrototype.css'

/**
 * Prototipo del consejo de diseño para el Espacio de campaña (2026-11-24).
 * Solo desarrollo, datos de muestra. No toca la vista real ni el backend.
 * Dirección: «Tablero del director» con una sola acción dominante.
 * Corregido tras la crítica visible del consejo (morado, píldora, ARIA y ruido).
 */

const GROUPS = [
  {
    label: 'Sesión',
    items: [
      { id: 'resumen', label: 'Resumen' },
      { id: 'mesa', label: 'Mesa de juego' },
      { id: 'escenas', label: 'Escenas y mapas' },
    ],
  },
  {
    label: 'Reparto',
    items: [
      { id: 'personajes', label: 'Personajes' },
      { id: 'npc', label: 'NPC' },
    ],
  },
  {
    label: 'Mundo',
    items: [
      { id: 'criaturas', label: 'Criaturas' },
      { id: 'facciones', label: 'Facciones', soon: true },
      { id: 'localizaciones', label: 'Localizaciones', soon: true },
      { id: 'lore', label: 'Historia y lore' },
    ],
  },
  {
    label: 'Biblioteca',
    items: [
      { id: 'biblio', label: 'Biblioteca de campaña' },
      { id: 'homebrew', label: 'Contenido homebrew' },
    ],
  },
  {
    label: 'Gestión',
    items: [
      { id: 'permisos', label: 'Participantes y permisos' },
      { id: 'config', label: 'Configuración' },
    ],
  },
] as const

const SCENES = [
  { id: 's1', name: 'Puerto de Scaldar · Plaza del Mercado', art: 'icon_dragon.png', meta: 'hace 2 días', active: true },
  { id: 's2', name: 'Cámara del dragón', art: 'icon_fireball.png', meta: 'hace 5 días', active: false },
  { id: 's3', name: 'Las catacumbas', art: 'icon_crystalblue.png', meta: 'hace 1 semana', active: false },
] as const

const CHARACTERS = [
  { id: 'c1', name: 'Vex', art: 'icon_magicsword.png', owner: 'Ana', level: 3 },
  { id: 'c2', name: 'Grom', art: 'icon_axe.png', owner: 'Luis', level: 3 },
  { id: 'c3', name: 'Kira', art: 'icon_ringmagic.png', owner: 'Marta', level: 3 },
] as const

const HISTORY = [
  { time: 'hace 1 h', text: 'Ficha «Vex» creada' },
  { time: 'hace 2 h', text: 'Ficha «Grom» creada' },
  { time: 'hace 2 días', text: 'Escena «Puerto de Scaldar» creada' },
] as const

type Selection = { kind: 'scene' | 'character'; id: string } | null
type Tab = 'detalles' | 'notas' | 'historial'
const TABS: Array<{ id: Tab; label: string }> = [
  { id: 'detalles', label: 'Detalles' },
  { id: 'notas', label: 'Notas' },
  { id: 'historial', label: 'Historial' },
]

const SECTION_LABELS: Record<string, string> = Object.fromEntries(
  GROUPS.flatMap((group) => group.items.map((item) => [item.id, item.label])),
)

export function CampaignSpacePrototype() {
  // `#prototipo-espacio-campana-vacio` permite capturar el estado vacío sin
  // un botón de depuración que estorbe en la composición.
  const empty = window.location.hash.includes('-vacio')
  const [section, setSection] = useState<string>('resumen')
  const [selected, setSelected] = useState<Selection>({ kind: 'scene', id: 's1' })
  const [tab, setTab] = useState<Tab>('detalles')
  const tabRefs = useRef<Array<HTMLButtonElement | null>>([])

  const scene = selected?.kind === 'scene' ? SCENES.find((item) => item.id === selected.id) : undefined
  const character = selected?.kind === 'character'
    ? CHARACTERS.find((item) => item.id === selected.id)
    : undefined
  const playingScene = scene ?? SCENES[0]

  const onTabKey = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return
    event.preventDefault()
    const next = (index + (event.key === 'ArrowRight' ? 1 : TABS.length - 1)) % TABS.length
    setTab(TABS[next].id)
    tabRefs.current[next]?.focus()
  }

  const sceneRows = empty ? [] : SCENES.map((item) => (
    <ListRow
      key={item.id}
      title={item.name}
      badge={item.active ? <Badge tone="neutral">En curso</Badge> : undefined}
      subtitle={item.active ? 'Escena en curso' : 'Lista para jugar'}
      meta={<span className="ccp__time">{item.meta}</span>}
      thumb={<img className="ccp-thumb" src={`/art/fantasy/${item.art}`} alt="" aria-hidden="true" />}
      selected={selected?.kind === 'scene' && selected.id === item.id}
      onClick={() => { setSelected({ kind: 'scene', id: item.id }); setTab('detalles') }}
    />
  ))

  const characterRows = empty ? [] : CHARACTERS.map((item) => (
    <ListRow
      key={item.id}
      title={item.name}
      badge={<Badge tone="neutral">Nivel {item.level}</Badge>}
      subtitle={item.owner}
      thumb={<img className="ccp-thumb" src={`/art/fantasy/${item.art}`} alt="" aria-hidden="true" />}
      selected={selected?.kind === 'character' && selected.id === item.id}
      onClick={() => { setSelected({ kind: 'character', id: item.id }); setTab('detalles') }}
    />
  ))

  const emptyState = (kind: 'escenas' | 'personajes' | 'otro') => (
    <EmptyState
      icon={<span aria-hidden="true">✦</span>}
      title="Todavía no hay nada aquí"
      text={kind === 'otro'
        ? 'Elige otra sección del índice para continuar.'
        : kind === 'escenas'
          ? 'Crea la primera escena para abrir la mesa.'
          : 'Crea el primer personaje de la campaña.'}
      action={kind === 'otro' ? undefined : (
        <button type="button" className="ccp__primary ccp__primary--small">
          {kind === 'escenas' ? 'Crear escena' : 'Crear personaje'}
        </button>
      )}
    />
  )

  const sceneList = <List>{sceneRows}</List>
  const characterList = <List>{characterRows}</List>

  return (
    <div className="ccp">
      <nav className="ccp__rail" aria-label="Secciones de la campaña">
        {GROUPS.map((group) => (
          <div className="ccp__group" key={group.label}>
            <h3>{group.label}</h3>
            {group.items.map((item) => (
              <button
                key={item.id}
                type="button"
                className={[
                  'ccp__nav',
                  section === item.id ? 'is-active' : '',
                  'soon' in item && item.soon ? 'is-soon' : '',
                ].filter(Boolean).join(' ')}
                aria-current={section === item.id ? 'page' : undefined}
                onClick={() => setSection(item.id)}
              >
                {item.label}
                {'soon' in item && item.soon ? <span className="ccp__soon">Próximamente</span> : null}
              </button>
            ))}
          </div>
        ))}
      </nav>

      <div className="ccp__main">
        <header className="ccp__head">
          <img className="ccp__cover" src="/art/fantasy/icon_dragon.png" alt="" aria-hidden="true" />
          <div className="ccp__identity">
            <span className="ccp__eyebrow">CAMPAÑA · D&D 5e</span>
            <div className="ccp__titleline">
              <h1>La caída de Scaldar</h1>
              <Badge tone="neutral">Activa</Badge>
            </div>
            <p>
              Campaña de D&D 5e en Puerto de Scaldar. Los jugadores investigan una serie de
              desapariciones ligadas al culto del Draco en la ciudad baja.
            </p>
          </div>
          <div className="ccp__head-actions">
            <button type="button" className="ccp__primary">
              Ir a la mesa · {playingScene.name.split(' · ')[0]}
            </button>
            <button type="button" className="ccp__secondary">Gestionar</button>
          </div>
        </header>

        {section === 'resumen' ? (
          <>
            <div className="ccp__figures" aria-label="Resumen de la campaña">
              <div><strong>3</strong><span>escenas</span></div>
              <div><strong>3</strong><span>personajes</span></div>
              <div><strong>3</strong><span>jugadores</span></div>
              <div><strong>hace 1 h</strong><span>última actividad</span></div>
            </div>
            <div className="ccp__sections">
              <section>
                <h2>Escenas</h2>
                {sceneRows.length === 0 ? emptyState('escenas') : sceneList}
              </section>
              <section>
                <h2>Personajes</h2>
                {characterRows.length === 0 ? emptyState('personajes') : characterList}
              </section>
            </div>
            <section className="ccp__activity">
              <h2>Actividad reciente</h2>
              <ul>
                {HISTORY.map((entry) => (
                  <li key={entry.text}>
                    <strong>{entry.time}</strong>
                    <span>{entry.text}</span>
                  </li>
                ))}
              </ul>
            </section>
          </>
        ) : null}

        {section === 'escenas' ? (
          <section className="ccp__page">
            <h2>Escenas y mapas</h2>
            {sceneRows.length === 0 ? emptyState('escenas') : sceneList}
          </section>
        ) : null}

        {section === 'personajes' ? (
          <section className="ccp__page">
            <h2>Personajes</h2>
            {characterRows.length === 0 ? emptyState('personajes') : characterList}
          </section>
        ) : null}

        {section !== 'resumen' && section !== 'escenas' && section !== 'personajes' ? (
          <section className="ccp__page">
            <h2>{SECTION_LABELS[section] ?? section}</h2>
            {emptyState('otro')}
          </section>
        ) : null}
      </div>

      <aside className="ccp__inspector" aria-label="Inspector">
        <h2 className="ccp__inspector-title">Inspector</h2>
        {!selected ? (
          <p className="ccp__hint">Selecciona una escena o un personaje para ver sus detalles, notas e historial.</p>
        ) : (
          <>
            <div className="ccp__tabs" role="tablist" aria-label="Inspector de la selección">
              {TABS.map((item, index) => (
                <button
                  key={item.id}
                  ref={(element) => { tabRefs.current[index] = element }}
                  type="button"
                  role="tab"
                  id={`ccp-tab-${item.id}`}
                  aria-controls={`ccp-panel-${item.id}`}
                  aria-selected={tab === item.id}
                  tabIndex={tab === item.id ? 0 : -1}
                  onClick={() => setTab(item.id)}
                  onKeyDown={(event) => onTabKey(event, index)}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <div id={`ccp-panel-${tab}`} role="tabpanel" aria-labelledby={`ccp-tab-${tab}`}
              className="ccp__inspector-body">
              {tab === 'detalles' ? (
                <>
                  <h3>{scene?.name ?? character?.name}</h3>
                  {scene ? (
                    <dl>
                      <dt>Estado</dt><dd>En curso</dd>
                      <dt>Grid</dt><dd>Cuadrada · 32 px</dd>
                    </dl>
                  ) : (
                    <dl>
                      <dt>Jugador</dt><dd>{character?.owner}</dd>
                      <dt>Nivel</dt><dd>{character?.level}</dd>
                    </dl>
                  )}
                </>
              ) : null}
              {tab === 'notas' ? (
                <>
                  <h3>Notas del GM</h3>
                  <p className="ccp__note">El culto se reúne al anochecer. La llave está con el tabernero.</p>
                  <p className="ccp__hint">Solo el DM y los propietarios ven estas notas.</p>
                </>
              ) : null}
              {tab === 'historial' ? (
                <>
                  <h3>Historial</h3>
                  <ul className="ccp__timeline">
                    {HISTORY.map((entry) => (
                      <li key={entry.text}><strong>{entry.time}</strong> {entry.text}</li>
                    ))}
                  </ul>
                </>
              ) : null}
            </div>
          </>
        )}
      </aside>
    </div>
  )
}
