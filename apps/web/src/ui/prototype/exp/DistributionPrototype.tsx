import { useState } from 'react'
import type { ReactNode } from 'react'
import './distribution.css'

/**
 * Prototipo de reorganización global (sin tocar vistas reales).
 * Inspiración: Alchemy VTT — escenario protagonista, paneles en los bordes,
 * acciones rápidas abajo, búsqueda omni y modo enfoque. Se conservan todas
 * las áreas y funciones; solo cambia dónde están y cómo se agrupan.
 *
 * Estimated states: `#prototipo-distribucion` y variantes `-mesa`, `-recursos`,
 * `-herramientas`. Requiere aprobación del propietario antes de integrar.
 */

type Screen = 'inicio' | 'mesa' | 'biblioteca' | 'herramientas'

const RAIL_GROUPS: Array<{ label: string; items: Array<{ id: Screen | string; icon: string; label: string }> }> = [
  {
    label: 'Jugar',
    items: [
      { id: 'inicio', icon: 'house', label: 'Inicio' },
      { id: 'campanas', icon: 'castle', label: 'Campañas' },
      { id: 'espacio', icon: 'family-tree', label: 'Espacio de campaña' },
      { id: 'mesa', icon: 'gamepad', label: 'Mesa de juego' },
    ],
  },
  {
    label: 'Recursos',
    items: [
      { id: 'biblioteca', icon: 'open-book', label: 'Biblioteca' },
      { id: 'imagenes', icon: 'gaze', label: 'Imágenes' },
      { id: 'creditos', icon: 'ankh', label: 'Créditos' },
    ],
  },
  {
    label: 'Herramientas',
    items: [
      { id: 'herramientas', icon: 'cog', label: 'Herramientas' },
      { id: 'dm', icon: 'riot-shield', label: 'Flujo del DM' },
      { id: 'lore', icon: 'family-tree', label: 'Historia y lore' },
    ],
  },
]

const TOKENS = [
  { x: 26, y: 34, label: 'V', color: '#7e72c9', name: 'Vex · Elfa' },
  { x: 52, y: 58, label: 'G', color: '#7fbf8e', name: 'Grom · Enano' },
  { x: 74, y: 42, label: 'K', color: '#d27272', name: 'Kobold' },
]

const LIB_TYPES = [
  ['Razas', 18], ['Clases', 12], ['Subclases', 12], ['Trasfondos', 13],
  ['Habilidades', 29], ['Criaturas', 16], ['Condiciones', 15], ['Objetos', 21],
] as const

const TOOLS = [
  { icon: 'riot-shield', title: 'Flujo del DM', text: 'Propuestas, aprobaciones y permisos de contenido.' },
  { icon: 'family-tree', title: 'Historia y lore', text: 'Grafo de facciones, lugares y cronología de la campaña.' },
  { icon: 'gaze', title: 'Biblioteca de imágenes', text: 'Álbumes, licencias, atribuciones y reutilización.' },
  { icon: 'ankh', title: 'Créditos y licencias', text: 'Procedencia de paquetes, iconos e ilustraciones.' },
  { icon: 'woman-elf-face', title: 'Creador de personajes', text: 'Asistente guiado con contenido real del SRD.' },
  { icon: 'wizard-face', title: 'Clase propia', text: 'Crea y publica clases homebrew paso a paso.' },
  { icon: 'chest', title: 'Inventario visual', text: 'Equipamiento por huecos, efectos y comparación.' },
  { icon: 'cog', title: 'Gestión de campaña', text: 'Miembros, invitaciones, escenas y configuración.' },
]

function Rail({ screen, setScreen }: { screen: Screen; setScreen: (s: Screen) => void }) {
  return (
    <nav className="dist-rail" aria-label="Navegación principal">
      <div className="dist-rail__brand" aria-label="ProjectVTT">✦</div>
      {RAIL_GROUPS.map((group) => (
        <div className="dist-rail__group" key={group.label}>
          <span className="dist-rail__label">{group.label}</span>
          {group.items.map((item) => (
            <button
              type="button"
              key={item.id}
              className={`dist-rail__item${screen === item.id ? ' is-active' : ''}`}
              aria-label={item.label}
              aria-current={screen === item.id ? 'page' : undefined}
              title={item.label}
              onClick={() => setScreen(item.id as Screen)}
            >
              <img src={`/icons/${item.icon}.svg`} alt="" aria-hidden="true" />
            </button>
          ))}
        </div>
      ))}
      <button type="button" className="dist-rail__item dist-rail__help" aria-label="Ayuda y atajos" title="Ayuda y atajos">?</button>
    </nav>
  )
}

function Topbar({ title, crumb, focus, setFocus }: {
  title: string; crumb: string; focus: boolean; setFocus: (value: boolean) => void
}) {
  return (
    <header className="dist-topbar">
      <div className="dist-topbar__identity">
        <strong>ProjectVTT</strong>
        <span className="dist-topbar__crumb">{crumb}</span>
      </div>
      <button type="button" className="dist-topbar__search">
        <img src="/icons/magnifying-glass.svg" alt="" aria-hidden="true" />
        <span>Buscar en la partida…</span>
        <kbd>Ctrl K</kbd>
      </button>
      <div className="dist-topbar__actions">
        <button type="button" className={focus ? 'is-active' : ''} onClick={() => setFocus(!focus)}>
          <img src="/icons/gaze.svg" alt="" aria-hidden="true" /> Enfocar
        </button>
        <button type="button" className="dist-topbar__user"><span className="dist-dot" /> Ana <span aria-hidden="true">▾</span></button>
      </div>
      <span className="dist-sr-only">{title}</span>
    </header>
  )
}

function PageHead({ title, subtitle, actions }: { title: string; subtitle: string; actions?: ReactNode }) {
  return (
    <div className="dist-pagehead">
      <div>
        <h1>{title}</h1>
        <p>{subtitle}</p>
      </div>
      {actions && <div className="dist-pagehead__actions">{actions}</div>}
    </div>
  )
}

function InicioScreen() {
  return (
    <div className="dist-page">
      <PageHead title="Inicio" subtitle="Tu partida, a un vistazo" actions={
        <>
          <button type="button" className="dist-btn">Unirse con código</button>
          <button type="button" className="dist-btn dist-btn--primary">+ Nueva campaña</button>
        </>
      } />
      <div className="dist-start">
        <section className="dist-card dist-card--hero">
          <span className="dist-eyebrow">CONTINUAR JUGANDO</span>
          <div className="dist-hero__body">
            <div className="dist-hero__art"><img src="/icons/castle.svg" alt="" aria-hidden="true" /></div>
            <div>
              <h2>La tumba de Saltmarsh</h2>
              <p>Nivel 3 · 4 jugadores · Última sesión: hace 2 días</p>
              <div className="dist-hero__actions">
                <button type="button" className="dist-btn dist-btn--primary">Ir a la mesa</button>
                <button type="button" className="dist-btn">Espacio de campaña</button>
              </div>
            </div>
          </div>
        </section>
        <aside className="dist-card dist-card--side">
          <span className="dist-eyebrow">ACCESOS RÁPIDOS</span>
          <ul className="dist-quick">
            <li><img src="/icons/open-book.svg" alt="" /> Explorar el SRD <span>136 ítems</span></li>
            <li><img src="/icons/woman-elf-face.svg" alt="" /> Crear personaje</li>
            <li><img src="/icons/gaze.svg" alt="" /> Biblioteca de imágenes</li>
            <li><img src="/icons/riot-shield.svg" alt="" /> Bandeja del DM</li>
          </ul>
        </aside>
      </div>
      <section className="dist-card">
        <span className="dist-eyebrow">TUS CAMPAÑAS</span>
        <div className="dist-cards">
          <article className="dist-campaign"><div className="dist-campaign__art" /><strong>La tumba de Saltmarsh</strong><span>4 jugadores · Nivel 3</span></article>
          <article className="dist-campaign dist-campaign--new"><strong>+ Crear campaña</strong><span>Empieza una partida nueva</span></article>
        </div>
      </section>
    </div>
  )
}

function MesaScreen({ focus }: { focus: boolean }) {
  const [tabL, setTabL] = useState<'diario' | 'notas'>('diario')
  const [tabR, setTabR] = useState<'grupo' | 'pnjs' | 'detalles'>('grupo')
  return (
    <div className={`dist-table${focus ? ' dist-table--focus' : ''}`}>
      <div className="dist-table__top">
        <div><strong>La tumba de Saltmarsh</strong><span> · Cámara del dragón</span></div>
        <div className="dist-table__party">
          <span className="dist-dot" /> 3 en línea
          <span className="dist-table__avatar">V</span><span className="dist-table__avatar">G</span>
        </div>
      </div>
      <div className="dist-table__grid">
        <aside className="dist-edge dist-edge--left" aria-label="Diario y notas">
          <div className="dist-tabs" role="tablist">
            <button role="tab" aria-selected={tabL === 'diario'} onClick={() => setTabL('diario')}>Diario</button>
            <button role="tab" aria-selected={tabL === 'notas'} onClick={() => setTabL('notas')}>Notas</button>
          </div>
          <div className="dist-edge__body">
            {tabL === 'diario' ? (
              <>
                <p className="dist-log"><strong>Vex</strong> tira 1d20 + 5 → <b>17</b></p>
                <p className="dist-log"><strong>Grom</strong> ataca con hacha → <b>12</b></p>
                <p className="dist-log dist-log--system">El dragón respira fuego.</p>
                <p className="dist-log"><strong>Vex</strong> usa <em>Curar heridas</em>.</p>
              </>
            ) : (
              <p className="dist-note">Notas de la sesión: la puerta está cerrada con llave. El guardia menciona un símbolo de dragón.</p>
            )}
          </div>
        </aside>
        <main className="dist-stage" aria-label="Escenario">
          <div className="dist-stage__canvas">
            {TOKENS.map((token) => (
              <div key={token.label} className="dist-token" title={token.name}
                style={{ left: `${token.x}%`, top: `${token.y}%`, borderColor: token.color, background: `${token.color}33` }}>
                {token.label}
              </div>
            ))}
            <span className="dist-stage__measure">25 ft</span>
          </div>
          <div className="dist-stage__hint">Rueda: zoom · Arrastrar: mover vista · Clic derecho: medir</div>
        </main>
        <aside className="dist-edge dist-edge--right" aria-label="Grupo y detalles">
          <div className="dist-tabs" role="tablist">
            <button role="tab" aria-selected={tabR === 'grupo'} onClick={() => setTabR('grupo')}>Grupo</button>
            <button role="tab" aria-selected={tabR === 'pnjs'} onClick={() => setTabR('pnjs')}>PNJs</button>
            <button role="tab" aria-selected={tabR === 'detalles'} onClick={() => setTabR('detalles')}>Detalles</button>
          </div>
          <div className="dist-edge__body">
            {tabR === 'grupo' && (
              <ul className="dist-party">
                <li><span className="dist-table__avatar">V</span><div><strong>Vex</strong><small>Elfa · 24/31 PG</small></div></li>
                <li><span className="dist-table__avatar">G</span><div><strong>Grom</strong><small>Enano · 38/40 PG</small></div></li>
              </ul>
            )}
            {tabR === 'pnjs' && <p className="dist-note">2 PNJs en la escena. 1 oculto para los jugadores.</p>}
            {tabR === 'detalles' && (
              <div className="dist-details"><span>ESCENA</span><strong>Cámara del dragón</strong><p>Rejilla 32 px · 3 tokens</p></div>
            )}
          </div>
        </aside>
      </div>
      <div className="dist-dock" aria-label="Acciones rápidas">
        {[['dice', 'Tirar'], ['chat-bubble', 'Notas'], ['position-marker', 'Medir'], ['fog', 'Niebla'], ['woman-elf-face', 'Ficha'], ['chest', 'Inventario']].map(([icon, label]) => (
          <button type="button" key={label}><img src={`/icons/${icon}.svg`} alt="" aria-hidden="true" /><span>{label}</span></button>
        ))}
      </div>
    </div>
  )
}

function BibliotecaScreen() {
  const [selected, setSelected] = useState('Criaturas')
  return (
    <div className="dist-page">
      <PageHead title="Biblioteca" subtitle="Todo tu contenido, en un solo lugar" actions={
        <>
          <button type="button" className="dist-btn">Biblioteca de imágenes</button>
          <button type="button" className="dist-btn dist-btn--primary">+ Nuevo contenido</button>
        </>
      } />
      <div className="dist-lib">
        <nav className="dist-lib__tree" aria-label="Tipos de contenido">
          <span className="dist-eyebrow">TIPOS</span>
          {LIB_TYPES.map(([name, count]) => (
            <button type="button" key={name} className={selected === name ? 'is-active' : ''} onClick={() => setSelected(name)}>
              <span>{name}</span><span className="dist-count">{count}</span>
            </button>
          ))}
        </nav>
        <section className="dist-lib__grid" aria-label="Elementos">
          <div className="dist-lib__tools">
            <input aria-label="Buscar contenido" placeholder="Buscar por nombre, etiqueta o tipo…" />
            <button type="button">Ámbito: Todos ▾</button>
            <button type="button">Filtros <span className="dist-count">2</span></button>
          </div>
          <div className="dist-tiles">
            {['Adult Red Dragon', 'Goblin', 'Lich', 'Orc', 'Owlbear', 'Skeleton', 'Troll', 'Vampire'].map((name) => (
              <button type="button" key={name} className="dist-tile">
                <span className="dist-tile__art"><img src="/icons/dragon-head.svg" alt="" aria-hidden="true" /></span>
                <strong>{name}</strong><small>Global · SRD 5.1</small>
              </button>
            ))}
          </div>
        </section>
        <aside className="dist-lib__inspector" aria-label="Detalle del elemento">
          <span className="dist-eyebrow">INSPECTOR</span>
          <h2>Adult Red Dragon</h2>
          <p>Criatura · Global · Publicado</p>
          <dl><dt>CA</dt><dd>19</dd><dt>PG</dt><dd>256</dd><dt>CR</dt><dd>17</dd><dt>Velocidad</dt><dd>30 ft</dd></dl>
          <button type="button" className="dist-btn">Ver propiedades</button>
        </aside>
      </div>
    </div>
  )
}

function HerramientasScreen() {
  return (
    <div className="dist-page">
      <PageHead title="Herramientas" subtitle="Todo lo que puedes hacer, agrupado y a la vista" />
      <div className="dist-tools">
        {TOOLS.map((tool) => (
          <article className="dist-tool" key={tool.title}>
            <img src={`/icons/${tool.icon}.svg`} alt="" aria-hidden="true" />
            <strong>{tool.title}</strong>
            <p>{tool.text}</p>
            <button type="button" className="dist-btn">Abrir →</button>
          </article>
        ))}
      </div>
    </div>
  )
}

export function DistributionPrototype({ variant = 'inicio' }: { variant?: Screen }) {
  const [screen, setScreen] = useState<Screen>(variant)
  const [focus, setFocus] = useState(false)
  const crumb: Record<Screen, string> = {
    inicio: 'Inicio', mesa: 'Campañas / La tumba de Saltmarsh / Mesa',
    biblioteca: 'Biblioteca / Explorador', herramientas: 'Herramientas',
  }
  const title = crumb[screen]
  return (
    <div className="dist-app">
      <a className="dist-skip" href="#dist-main">Saltar al contenido principal</a>
      <Rail screen={screen} setScreen={setScreen} />
      <div className="dist-main-wrap">
        <Topbar title={title} crumb={crumb[screen]} focus={focus} setFocus={setFocus} />
        <main id="dist-main" className="dist-main">
          {screen === 'inicio' && <InicioScreen />}
          {screen === 'mesa' && <MesaScreen focus={focus} />}
          {screen === 'biblioteca' && <BibliotecaScreen />}
          {screen === 'herramientas' && <HerramientasScreen />}
        </main>
      </div>
    </div>
  )
}
