import { useState } from 'react'
import './alchemyTable.css'

/**
 * Prototipo de mesa «todo acoplado» inspirado en la captura de Alchemy.
 * Solo desarrollo. Datos de muestra. Requiere aprobación antes de integrar.
 */

const SCENES = [
  { name: 'Ceniza sobre los Muros', active: true, tokens: 4 },
  { name: 'El Puñetazo de Hierro', tokens: 2 },
  { name: 'El registro del cuartel', tokens: 3 },
  { name: 'La clínica y los Muros', tokens: 1 },
  { name: 'Portada · La Legión en Paso de Cenízaro', tokens: 0 },
  { name: 'La Fosa', tokens: 5 },
  { name: 'El monasterio del Trascendido', tokens: 2 },
  { name: 'Forja del Crepúsculo', tokens: 0 },
] as const

const DICE = ['d4', 'd6', 'd8', 'd10', 'd12', 'd20'] as const

export function AlchemyTablePrototype() {
  const [mode, setMode] = useState<'story' | 'tactical'>('tactical')
  const [tab, setTab] = useState<'scenes' | 'story' | 'tools'>('scenes')
  const [activeScene, setActiveScene] = useState('Ceniza sobre los Muros')
  const [filter, setFilter] = useState('')
  const [messages, setMessages] = useState<string[]>([])
  const [draft, setDraft] = useState('')
  const [rolls, setRolls] = useState<number[]>([])

  const send = () => {
    if (!draft.trim()) return
    setMessages((current) => [...current, draft.trim()])
    setDraft('')
  }
  const roll = (die: string) => {
    const sides = Number(die.slice(1))
    const value = 1 + Math.floor(Math.random() * sides)
    setRolls((current) => [value, ...current].slice(0, 3))
    setMessages((current) => [...current, `🎲 ${die} → ${value}`])
  }

  const scenes = SCENES.filter((scene) =>
    scene.name.toLowerCase().includes(filter.toLowerCase()))

  return (
    <div className="alt">
      <header className="alt__top">
        <div className="alt__brand">✦</div>
        <div className="alt__modes" role="group" aria-label="Vista">
          <button type="button" className={mode === 'story' ? 'is-on' : ''}
            onClick={() => setMode('story')}>STORY</button>
          <button type="button" className={mode === 'tactical' ? 'is-on' : ''}
            onClick={() => setMode('tactical')}>TACTICAL</button>
          <button type="button" className="alt__search" aria-label="Buscar">⌕</button>
        </div>
        <div className="alt__top-right"><span className="alt__dot" /> En vivo</div>
      </header>

      <aside className="alt__left" aria-label="Diario">
        <div className="alt__panel-head">
          <div className="alt__avatar">V</div>
          <div><strong>Vex</strong><small>Nivel 3 · 24/31 PG</small></div>
        </div>
        <div className="alt__tabs">
          <button type="button" className="is-on">Diario</button>
          <button type="button">Notas</button>
        </div>
        <div className="alt__messages">
          <div className="alt__connected">✦ CONNECTED<br /><strong>Bienvenido a ProjectVTT</strong></div>
          {rolls.map((value, index) => (
            <div className="alt__roll" key={index}>
              <div className="alt__dice-big">🎲</div>
              <strong>{value}</strong>
              <small>TIRADA</small>
            </div>
          ))}
          {messages.map((message, index) => (
            <p className="alt__message" key={index}>{message}</p>
          ))}
        </div>
        <form className="alt__composer" onSubmit={(event) => { event.preventDefault(); send() }}>
          <input aria-label="Mensaje" placeholder="Mensaje" value={draft}
            onChange={(event) => setDraft(event.target.value)} />
          <button type="submit" aria-label="Enviar mensaje">＋</button>
        </form>
      </aside>

      <main className="alt__stage" aria-label="Escenario">
        {mode === 'story' ? (
          <div className="alt__story">
            <span className="alt__story-kicker">ESCENA ACTUAL</span>
            <h1>{activeScene}</h1>
            <p>El grupo se detiene ante la entrada. Todo está en silencio.</p>
            <button type="button" onClick={() => setMode('tactical')}>Abrir vista táctica →</button>
          </div>
        ) : (
          <div className="alt__board">
            <span className="alt__board-label">CONNECTED · Welcome to ProjectVTT</span>
            <div className="alt__token alt__token--one">V</div>
            <div className="alt__token alt__token--two">G</div>
            <div className="alt__token alt__token--three">K</div>
          </div>
        )}
      </main>

      <nav className="alt__toolstrip" aria-label="Herramientas">
        {['house', 'gamepad', 'open-book', 'family-tree'].map((icon, index) => (
          <button key={icon} type="button" className={index === 1 ? 'is-on' : ''}
            aria-label={icon}>
            <img src={`/icons/${icon}.svg`} alt="" aria-hidden="true" />
          </button>
        ))}
      </nav>

      <aside className="alt__right" aria-label="Panel del DM">
        <header className="alt__dm">
          <div className="alt__dm-avatar">✦</div>
          <div><strong>Dungeon Master</strong><small>beterek</small></div>
        </header>
        <div className="alt__tabs alt__tabs--dm">
          <button type="button" className={tab === 'scenes' ? 'is-on' : ''}
            onClick={() => setTab('scenes')}>SCENES</button>
          <button type="button" className={tab === 'story' ? 'is-on' : ''}
            onClick={() => setTab('story')}>STORY</button>
          <button type="button" className={tab === 'tools' ? 'is-on' : ''}
            onClick={() => setTab('tools')}>TOOLS</button>
        </div>
        {tab === 'scenes' ? (
          <>
            <div className="alt__filter">
              <input aria-label="Filtrar escenas" placeholder="Filter Scenes"
                value={filter} onChange={(event) => setFilter(event.target.value)} />
            </div>
            <ul className="alt__scenes">
              {scenes.map((scene) => (
                <li key={scene.name}>
                  <button type="button"
                    className={activeScene === scene.name ? 'is-active' : ''}
                    onClick={() => setActiveScene(scene.name)}>
                    <span className="alt__thumb" aria-hidden="true" />
                    <span className="alt__scene-name">{scene.name}</span>
                    {activeScene === scene.name && <span className="alt__play" aria-hidden="true">▶</span>}
                  </button>
                </li>
              ))}
            </ul>
            <button type="button" className="alt__add" aria-label="Nueva escena">＋</button>
          </>
        ) : (
          <div className="alt__empty-tab">
            {tab === 'story' ? 'Notas e hitos de la historia.' : 'Herramientas del director de juego.'}
          </div>
        )}
      </aside>

      <footer className="alt__dock" aria-label="Bandeja de dados">
        {DICE.map((die) => (
          <button type="button" key={die} onClick={() => roll(die)} aria-label={`Tirar ${die}`}>
            <span aria-hidden="true">◈</span><small>{die}</small>
          </button>
        ))}
        <span className="alt__dock-sep" />
        <button type="button" aria-label="Audio">♫</button>
        <button type="button" aria-label="Micrófono">⌁</button>
      </footer>
    </div>
  )
}
