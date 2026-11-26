import { useState } from 'react'
import './alchemyV2.css'

/**
 * Prototipo V2 (solo desarrollo): raíl oculto revelado por flecha/hover/foco,
 * cinco destinos + «Más», cabecera sin marca duplicada e Inicio más denso.
 * Datos de muestra. Requiere aprobación antes de integrar.
 */

const PRIMARY = [
  { icon: 'house', label: 'Inicio' },
  { icon: 'castle', label: 'Campañas' },
  { icon: 'gamepad', label: 'Mesa' },
  { icon: 'open-book', label: 'Biblioteca' },
  { icon: 'cog', label: 'Herramientas' },
] as const

const MORE = [
  { icon: 'family-tree', label: 'Espacio de campaña' },
  { icon: 'gaze', label: 'Imágenes' },
  { icon: 'ankh', label: 'Créditos' },
  { icon: 'riot-shield', label: 'Flujo del DM' },
  { icon: 'gears', label: 'Gestión' },
] as const

const CARDS = [
  { name: 'La tumba de Saltmarsh', meta: 'Nivel 3 · 4 jugadores', art: 'icon_dragon.png', tone: 'purple' },
  { name: 'Chicago by Night', meta: 'Nivel 2 · 5 jugadores', art: 'icon_potionred.png', tone: 'red' },
  { name: 'Bosque de Scaldar', meta: 'Nivel 5 · 3 jugadores', art: 'icon_aurared.png', tone: 'blue' },
  { name: 'Cámara del dragón', meta: 'Nivel 7 · 4 jugadores', art: 'icon_swordblood.png', tone: 'gold' },
] as const

const COLLAGE = [
  { file: 'icon_dragon.png', x: 4, y: -12, size: 240, rotate: -12 },
  { file: 'icon_fireball.png', x: 74, y: -16, size: 220, rotate: 10 },
  { file: 'icon_magicsword.png', x: 42, y: 54, size: 200, rotate: -7 },
  { file: 'icon_helmetgold.png', x: 86, y: 44, size: 160, rotate: 14 },
  { file: 'icon_bookholy.png', x: 16, y: 60, size: 180, rotate: 8 },
] as const

export function AlchemyV2Prototype() {
  const [railOpen, setRailOpen] = useState(false)
  const [pinned, setPinned] = useState(false)
  const [moreOpen, setMoreOpen] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [active, setActive] = useState('Inicio')

  const closeRail = () => {
    if (!pinned) {
      setRailOpen(false)
      setMoreOpen(false)
    }
  }

  return (
    <div className="v2">
      <div className="v2__brand" aria-label="ProjectVTT">✦</div>

      <header className="v2__top">
        <span className="v2__crumb">{active}</span>
        <div className="v2__actions">
          <button type="button" className="v2__icon" aria-label="Buscar" title="Buscar (Ctrl K)">
            <img src="/icons/magnifying-glass.svg" alt="" aria-hidden="true" />
          </button>
          <div className="v2__create-wrap">
            <button type="button" className="v2__create" aria-expanded={createOpen}
              onClick={() => setCreateOpen((open) => !open)}>
              Crear ▾
            </button>
            {createOpen && (
              <div className="v2__menu" role="menu">
                <button type="button" role="menuitem">Nueva campaña</button>
                <button type="button" role="menuitem">Clase propia</button>
                <button type="button" role="menuitem">Nuevo contenido</button>
              </div>
            )}
          </div>
          <button type="button" className="v2__avatar" aria-label="Cerrar sesión (Ana)" title="Ana · Cerrar sesión">A</button>
        </div>
      </header>

      {/* Disparador siempre visible: flecha en el borde. */}
      <button type="button" className="v2__edge"
        aria-label="Mostrar navegación" aria-expanded={railOpen} aria-controls="v2-rail"
        title="Mostrar navegación"
        onMouseEnter={() => setRailOpen(true)}
        onFocus={() => setRailOpen(true)}
        onClick={() => setRailOpen((open) => !open)}>
        <span aria-hidden="true">{railOpen ? '‹' : '›'}</span>
      </button>

      <nav id="v2-rail"
        className={`v2__rail${railOpen ? ' is-open' : ''}`}
        aria-label="Navegación principal"
        onMouseEnter={() => setRailOpen(true)}
        onMouseLeave={closeRail}
        onKeyDown={(event) => { if (event.key === 'Escape') closeRail() }}>
        {PRIMARY.map((item) => (
          <button key={item.label} type="button"
            className={active === item.label ? 'is-active' : ''}
            aria-label={item.label} title={item.label}
            aria-current={active === item.label ? 'page' : undefined}
            onClick={() => { setActive(item.label); setMoreOpen(false) }}>
            <img src={`/icons/${item.icon}.svg`} alt="" aria-hidden="true" />
          </button>
        ))}
        <div className="v2__rail-sep" />
        <button type="button" className={moreOpen ? 'is-active' : ''}
          aria-label="Más destinos" aria-expanded={moreOpen} title="Más destinos"
          onClick={() => setMoreOpen((open) => !open)}>
          <span aria-hidden="true">···</span>
        </button>
        {moreOpen && (
          <div className="v2__more">
            {MORE.map((item) => (
              <button key={item.label} type="button" aria-label={item.label} title={item.label}
                onClick={() => setActive(item.label)}>
                <img src={`/icons/${item.icon}.svg`} alt="" aria-hidden="true" />
              </button>
            ))}
          </div>
        )}
        <button type="button" className={`v2__pin${pinned ? ' is-on' : ''}`}
          aria-label={pinned ? 'Desfijar navegación' : 'Fijar navegación'}
          aria-pressed={pinned} title={pinned ? 'Desfijar' : 'Fijar'}
          onClick={() => setPinned((value) => !value)}>
          <span aria-hidden="true">⌖</span>
        </button>
      </nav>

      <main className="v2__main">
        <div className="v2__label">Destacado</div>
        <section className="v2__hero" aria-label="Contenido destacado">
          <div className="v2__collage" aria-hidden="true">
            {COLLAGE.map((art) => (
              <img key={art.file} src={`/art/fantasy/${art.file}`} alt=""
                style={{
                  left: `${art.x}%`, top: `${art.y}%`, width: art.size, height: art.size,
                  transform: `rotate(${art.rotate}deg)`,
                }}
              />
            ))}
            <div className="v2__vignette" />
          </div>
          <div className="v2__hero-content">
            <span>Biblioteca universal · SRD 5.1</span>
            <h1>D&D 5e</h1>
            <div className="v2__rule" />
            <p>136 elementos reales para jugar hoy.</p>
            <button type="button" className="v2__cta">Explorar biblioteca</button>
          </div>
        </section>

        <div className="v2__row">
          <h2>Continuar jugando</h2>
          <div className="v2__small-actions">
            <button type="button" aria-label="Anterior">‹</button>
            <button type="button" aria-label="Siguiente">›</button>
          </div>
        </div>
        <section className="v2__cards" aria-label="Campañas recientes">
          {CARDS.map((card) => (
            <article key={card.name} className={`v2__card v2__card--${card.tone}`}>
              <div className="v2__cover"><img src={`/art/fantasy/${card.art}`} alt="" aria-hidden="true" /></div>
              <strong>{card.name}</strong>
              <span>{card.meta}</span>
            </article>
          ))}
        </section>

        <section className="v2__strip" aria-label="Actividad e invitaciones">
          <div>
            <h3>Actividad reciente</h3>
            <p>Sin actividad reciente. Tus tiradas y cambios aparecerán aquí.</p>
          </div>
          <div>
            <h3>Unirse a una campaña</h3>
            <div className="v2__join">
              <input aria-label="Código de invitación" placeholder="Código de invitación" />
              <button type="button">Unirse</button>
            </div>
          </div>
        </section>
      </main>
    </div>
  )
}
