import { useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import './ficha.css'

/**
 * Prototipo del rediseño de la Ficha clásica (barrido de paneles MVP) —
 * solo desarrollo (`#prototipo-ficha[-vacio|-extremo]`). Recompone la
 * hoja de trabajo (misma función: crear, editar atributos/PG, inventario,
 * efectos, tiradas) con el **lenguaje visual aprobado del Hub** (paneles
 * con títulos de sección, atributos en tarjeta, chips de estado). La
 * ficha y el Hub siguen compartiendo estado (CHARACTERS.md §3): esto es
 * solo su acabado.
 */

type Variant = 'normal' | 'vacio' | 'extremo'

const ATTRS = [
  { key: 'Fue', score: 17, mod: '+3' },
  { key: 'Des', score: 14, mod: '+2' },
  { key: 'Con', score: 16, mod: '+3' },
  { key: 'Int', score: 10, mod: '+0' },
  { key: 'Sab', score: 12, mod: '+1' },
  { key: 'Car', score: 8, mod: '−1' },
]

const ITEMS = [
  { name: 'Herramientas de ladrón', qty: 1 },
  { name: 'Poción de curación', qty: 2 },
  { name: 'Antorcha', qty: 5 },
]

const EFFECTS = ['Bendecido', 'Envenenado']

const ROLLS = [
  { who: 'ana', what: 'Furtividad', result: '1d20+5 → 23' },
  { who: 'ana', what: 'Ataque con daga', result: '1d20+5 → 8' },
]

export function FichaPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const [editing, setEditing] = useState(false)
  const empty = variant === 'vacio'
  const long = variant === 'extremo'
  const name = long
    ? 'Guardia de élite de la ciudadela de Scaldar con armadura ceremonial'
    : 'Elenar'

  return (
    <PrototypeShell active="mesa">
      <div className="ficha-overlay">
        <section className="ficha" aria-label="Ficha">
          {empty ? (
            <>
              <header className="ficha__head">
                <h1 className="ficha__name">Fichas</h1>
              </header>
              <div className="ficha__body">
                <div className="ficha-create">
                  <label className="ficha-field">
                    Nombre del personaje
                    <input className="pv-input" placeholder="Nombre" />
                  </label>
                  <button type="button" className="pv-btn pv-btn--primary">
                    Crear
                  </button>
                </div>
                <p className="ficha-empty">Aún no hay actores en esta campaña.</p>
              </div>
            </>
          ) : (
            <>
              <header className="ficha__head">
                <div className="ficha__portrait" aria-hidden="true">
                  {name.slice(0, 1)}
                </div>
                <div className="ficha__ident">
                  <h1 className="ficha__name">{name}</h1>
                  <span className="ficha__meta">Personaje · Nivel 1 · dnd5e</span>
                </div>
                <button
                  type="button"
                  className={`pv-btn ${editing ? 'pv-btn--primary' : 'pv-btn--secondary'}`}
                  aria-pressed={editing}
                  onClick={() => setEditing((current) => !current)}
                >
                  {editing ? 'Guardar' : 'Editar ficha'}
                </button>
              </header>

              <div className="ficha__body">
                <section className="ficha-panel">
                  <h2 className="ficha-panel__title">Atributos</h2>
                  <div className="ficha-attrs">
                    {ATTRS.map((attr) => (
                      <div key={attr.key} className="ficha-attr">
                        {editing ? (
                          <input
                            className="pv-input"
                            type="number"
                            defaultValue={attr.score}
                            aria-label={attr.key}
                          />
                        ) : (
                          <strong>{attr.score}</strong>
                        )}
                        <span>{attr.key}</span>
                        <span className="ficha-attr__mod">{attr.mod}</span>
                      </div>
                    ))}
                  </div>
                </section>

                <section className="ficha-panel">
                  <h2 className="ficha-panel__title">Estado</h2>
                  {editing ? (
                    <div className="ficha-hp">
                      <label className="ficha-field">
                        Puntos de vida actuales
                        <input className="pv-input" type="number" defaultValue={9} />
                      </label>
                      <label className="ficha-field">
                        Puntos de vida máximos
                        <input className="pv-input" type="number" defaultValue={9} />
                      </label>
                    </div>
                  ) : (
                    <p className="ficha-text">Puntos de golpe: 9/9</p>
                  )}
                  <div className="ficha-chips">
                    {empty ? null : EFFECTS.map((effect) => (
                      <span key={effect} className="ficha-chip">
                        {effect}
                        <button type="button" aria-label={`Retirar ${effect}`}>
                          ×
                        </button>
                      </span>
                    ))}
                  </div>
                </section>

                <section className="ficha-panel">
                  <h2 className="ficha-panel__title">Inventario</h2>
                  <ul className="ficha-list">
                    {(long ? [...ITEMS, ...ITEMS, ...ITEMS] : ITEMS).map((item, index) => (
                      <li key={`${item.name}-${index}`}>
                        <span>{long ? `${item.name} de la guardia nocturna del puerto` : item.name}</span>
                        <span className="ficha-list__qty">×{item.qty}</span>
                      </li>
                    ))}
                  </ul>
                  <div className="ficha-add">
                    <input className="pv-input" placeholder="Ej. Antorcha" aria-label="Nombre del nuevo ítem" />
                    <input className="pv-input" type="number" defaultValue={1} aria-label="Cantidad del nuevo ítem" />
                    <button type="button" className="pv-btn pv-btn--secondary">Añadir</button>
                  </div>
                </section>

                <section className="ficha-panel">
                  <h2 className="ficha-panel__title">Tiradas</h2>
                  <ul className="ficha-list">
                    {ROLLS.map((roll) => (
                      <li key={roll.what}>
                        <span>{roll.what}</span>
                        <span className="ficha-list__qty">{roll.result}</span>
                      </li>
                    ))}
                  </ul>
                </section>
              </div>
            </>
          )}
        </section>
      </div>
    </PrototypeShell>
  )
}
