import { useState } from 'react'
import './alchemyHome.css'

/**
 * Prototipo D1+D2 (inspiración Alchemy VTT, 2026-11-19). Solo desarrollo.
 * No usa ningún arte de Alchemy ni contenido protegido: el collage del hero
 * usa ilustraciones CC0 de OpenGameArt ya incluidas y acreditadas en
 * `public/art/LICENSES.md`. Las fuentes serif son del sistema (sin licencia
 * nueva). Requiere aprobación antes de integrar en InicioView/AppShell.
 */

const RAIL = [
  { id: 'inicio', icon: 'house', label: 'Inicio' },
  { id: 'campanas', icon: 'castle', label: 'Campañas' },
  { id: 'biblioteca', icon: 'open-book', label: 'Biblioteca' },
  { id: 'grupo', icon: 'family-tree', label: 'Grupo' },
  { id: 'ficha', icon: 'woman-elf-face', label: 'Personaje' },
] as const

const HERO_COLLAGE = [
  { file: 'icon_dragon.png', x: 6, y: -8, size: 250, rotate: -12, blur: 1, opacity: 0.55 },
  { file: 'icon_fireball.png', x: 72, y: -14, size: 230, rotate: 10, blur: 2, opacity: 0.5 },
  { file: 'icon_magicsword.png', x: 40, y: 52, size: 210, rotate: -7, blur: 1, opacity: 0.42 },
  { file: 'icon_helmetgold.png', x: 84, y: 48, size: 170, rotate: 14, blur: 2, opacity: 0.5 },
  { file: 'icon_bookholy.png', x: 18, y: 58, size: 190, rotate: 8, blur: 2, opacity: 0.4 },
  { file: 'icon_crystalblue.png', x: 58, y: -20, size: 160, rotate: -5, blur: 3, opacity: 0.45 },
] as const

const CAMPAIGNS = [
  { name: 'La tumba de Saltmarsh', meta: 'Nivel 3 · 4 jugadores', art: 'icon_dragon.png', tone: 'purple' },
  { name: 'Chicago by Night', meta: 'Nivel 2 · 5 jugadores', art: 'icon_potionred.png', tone: 'red' },
  { name: 'Bosque de Scaldar', meta: 'Nivel 5 · 3 jugadores', art: 'icon_aurared.png', tone: 'blue' },
  { name: 'Cámara del dragón', meta: 'Nivel 7 · 4 jugadores', art: 'icon_swordblood.png', tone: 'gold' },
] as const

export function AlchemyHomePrototype() {
  const [active, setActive] = useState<string>('inicio')
  const [slide, setSlide] = useState(0)
  const [createOpen, setCreateOpen] = useState(false)

  return (
    <div className="alch">
      {/* Marca mínima y acción principal, como en Alchemy. */}
      <div className="alch__brand" aria-label="ProjectVTT">✦</div>
      <button type="button" className="alch__create" aria-expanded={createOpen}
        onClick={() => setCreateOpen((open) => !open)}>
        Crear <span aria-hidden="true">▾</span>
      </button>
      {createOpen && (
        <div className="alch__create-menu" role="menu">
          <button type="button" role="menuitem">Nueva campaña</button>
          <button type="button" role="menuitem">Crear clase propia</button>
          <button type="button" role="menuitem">Nuevo contenido</button>
        </div>
      )}

      {/* Raíl flotante en cápsula: un solo destino por icono, con tooltip. */}
      <nav className="alch__rail" aria-label="Navegación principal">
        {RAIL.map((item) => (
          <button key={item.id} type="button"
            className={active === item.id ? 'is-active' : ''}
            aria-label={item.label} title={item.label}
            aria-current={active === item.id ? 'page' : undefined}
            onClick={() => setActive(item.id)}>
            <img src={`/icons/${item.icon}.svg`} alt="" aria-hidden="true" />
          </button>
        ))}
      </nav>
      <div className="alch__avatar" title="Tu perfil">A</div>

      <main className="alch__main">
        <div className="alch__section-label">Destacado</div>
        <section className="alch__hero" aria-label="Contenido destacado">
          <div className="alch__hero-art" aria-hidden="true">
            {HERO_COLLAGE.map((art) => (
              <img key={art.file} src={`/art/fantasy/${art.file}`} alt=""
                style={{
                  left: `${art.x}%`, top: `${art.y}%`,
                  width: art.size, height: art.size,
                  transform: `rotate(${art.rotate}deg)`,
                  filter: `blur(${art.blur}px)`, opacity: art.opacity,
                }}
              />
            ))}
            <div className="alch__hero-vignette" />
          </div>
          <div className="alch__hero-content">
            <span className="alch__hero-kicker">Biblioteca universal · SRD 5.1</span>
            <h1>D&D 5e</h1>
            <div className="alch__hero-rule" />
            <p>136 elementos reales: razas, clases, hechizos, criaturas y más.</p>
            <button type="button" className="alch__hero-cta">Explorar biblioteca</button>
          </div>
          <div className="alch__dots" aria-hidden="true">
            {[0, 1, 2].map((index) => (
              <button key={index} type="button"
                className={slide === index ? 'is-active' : ''}
                aria-label={`Destacado ${index + 1}`}
                onClick={() => setSlide(index)} />
            ))}
          </div>
        </section>

        <div className="alch__row-head">
          <h2>Continuar jugando</h2>
          <div className="alch__arrows">
            <button type="button" aria-label="Anterior">‹</button>
            <button type="button" aria-label="Siguiente">›</button>
          </div>
        </div>
        <section className="alch__cards" aria-label="Campañas recientes">
          {CAMPAIGNS.map((campaign) => (
            <article className={`alch__card alch__card--${campaign.tone}`} key={campaign.name}>
              <div className="alch__card-cover">
                <img src={`/art/fantasy/${campaign.art}`} alt="" aria-hidden="true" />
              </div>
              <strong>{campaign.name}</strong>
              <span>{campaign.meta}</span>
            </article>
          ))}
        </section>
      </main>
    </div>
  )
}
