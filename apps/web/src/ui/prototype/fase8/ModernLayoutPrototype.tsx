import './modernLayout.css'

/**
 * Prototipo de rediseño visual (PMB-8.6c, solo desarrollo): mesa inspirada
 * en VTT modernos (Foundry V13 / Carolingian UI / Owlbear Rodeo):
 * tablero protagonista, rail compacto de iconos, navegación horizontal de
 * escenas, panel derecho con pestañas y barra inferior de acciones rápidas.
 * Datos de MUESTRA. Requiere aprobación antes de tocar la app real.
 */

const RAIL = [
  ['tablero', 'gamepad', 'Tablero'], ['ficha', 'woman-elf-face', 'Ficha'],
  ['combate', 'attack-gauge', 'Combate'], ['chat', 'chat-bubble', 'Chat'],
  ['mapa', 'position-marker', 'Mapa'], ['biblioteca', 'open-book', 'Biblioteca'],
] as const

const SCENES = [
  ['Escena actual', true], ['Bosque', false], ['Mazmorra', false],
] as const

const TOKENS = [
  { x: 22, y: 30, label: 'E', color: '#7e72c9' },
  { x: 46, y: 52, label: 'G', color: '#7fbf8e' },
  { x: 68, y: 38, label: 'M', color: '#d27272' },
] as const

const ACTIONS = [
  ['dice', 'Tirar'], ['position-marker', 'Anotar'],
  ['magnifying-glass', 'Medir'], ['fog', 'Niebla'],
] as const

export function ModernLayoutPrototype() {
  return (
    <div className="modern">
      <header className="modern__top">
        <div className="modern__brand">
          <span className="modern__mark">✦</span>
          <span>ProjectVTT</span>
        </div>
        <nav className="modern__scenes" aria-label="Escenas">
          {SCENES.map(([name, active]) => (
            <button key={name} type="button" className={active ? 'is-active' : ''}>
              {name}
            </button>
          ))}
          <button type="button" className="modern__add" aria-label="Nueva escena">＋</button>
        </nav>
        <div className="modern__status"><span className="modern__live" /> En vivo · Ana</div>
      </header>

      <div className="modern__rail" aria-label="Herramientas">
        {RAIL.map(([id, icon, label], index) => (
          <button key={id} type="button"
            className={index === 0 ? 'is-active' : ''}
            aria-label={label} title={label}>
            <img src={`/icons/${icon}.svg`} alt="" aria-hidden="true" />
          </button>
        ))}
      </div>

      <main className="modern__board" aria-label="Tablero">
        <div className="modern__canvas">
          {TOKENS.map((token, index) => (
            <div key={index} className="modern__token"
              style={{ left: `${token.x}%`, top: `${token.y}%`,
                borderColor: token.color, background: `${token.color}33` }}>
              {token.label}
            </div>
          ))}
          <div className="modern__ruler" aria-hidden="true">35 ft · 7 cuadrados</div>
        </div>
        <div className="modern__hint">
          Rueda: zoom · Arrastrar fondo: mover vista · Arrastrar token: moverlo
        </div>
        <span className="modern__board-label">BOSQUE DE SCALDAR · EN VIVO</span>
      </main>

      <aside className="modern__panel" aria-label="Inspector">
        <div className="modern__tabs" role="tablist">
          <button type="button" role="tab" aria-selected="true">Detalles</button>
          <button type="button" role="tab" aria-selected="false">Capas</button>
          <button type="button" role="tab" aria-selected="false">Historial</button>
        </div>
        <div className="modern__panel-body">
          <div className="modern__section">
            <span className="modern__label">ESCENA</span>
            <strong className="modern__title">Bosque de Scaldar</strong>
            <p className="modern__meta">Rejilla cuadrada · 32 px · 3 tokens</p>
          </div>
          <div className="modern__section">
            <span className="modern__label">CAPAS</span>
            <div className="modern__layer">
              <span><img src="/icons/gaze.svg" alt="" /> Mapa base</span><span>100%</span>
            </div>
            <div className="modern__layer">
              <span><img src="/icons/gaze.svg" alt="" /> Niebla</span><span>65%</span>
            </div>
            <div className="modern__layer">
              <span><img src="/icons/sight-disabled.svg" alt="" /> Anotaciones</span><span>0%</span>
            </div>
          </div>
          <div className="modern__section modern__empty">
            Selecciona un token para ver sus propiedades.
          </div>
        </div>
      </aside>

      <footer className="modern__dock" aria-label="Acciones rápidas">
        {ACTIONS.map(([icon, label]) => (
          <button key={label} type="button" aria-label={label} title={label}>
            <img src={`/icons/${icon}.svg`} alt="" aria-hidden="true" />
            <small>{label}</small>
          </button>
        ))}
        <span className="modern__dock-sep" />
        <button type="button" className="modern__dock-primary">＋ Añadir token</button>
      </footer>
    </div>
  )
}
