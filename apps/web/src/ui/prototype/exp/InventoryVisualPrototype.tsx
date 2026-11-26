import { useState } from 'react'
import './experience.css'

/**
 * Prototipo interactivo EXP-2 (solo desarrollo): gestión de equipo con la
 * imagen del personaje aportada por el usuario en el centro, miniaturas en el
 * inventario, huecos configurables a la derecha y acciones por botón además
 * de arrastre. Datos y efectos de MUESTRA. Pendiente de aprobación visual.
 */

interface Item {
  id: string
  name: string
  icon: string
  slot: string
  note: string
  ac?: number
  damage?: string
}

const ITEMS: Item[] = [
  { id: 'espada', name: 'Espada larga', icon: '/icons/broadsword.svg', slot: 'Arma principal', damage: '1d8', note: 'Arma marcial · 3 lb' },
  { id: 'arco', name: 'Arco corto', icon: '/icons/bow-arrow.svg', slot: 'Arma secundaria', damage: '1d6', note: 'A distancia · 2 lb' },
  { id: 'armadura', name: 'Cota de malla', icon: '/icons/riot-shield.svg', slot: 'Armadura', ac: 16, note: 'Armadura pesada · 55 lb' },
  { id: 'hacha', name: 'Hacha de mano', icon: '/icons/wood-axe.svg', slot: 'Arma principal', damage: '1d6', note: 'Arma marcial · 3 lb' },
  { id: 'amuleto', name: 'Amuleto de fuego', icon: '/icons/chakram.svg', slot: 'Accesorio', note: 'Resistencia al fuego · 1 lb' },
  { id: 'botas', name: 'Botas de viaje', icon: '/icons/chest.svg', slot: 'Botas', note: 'Velocidad +5 ft · 2 lb' },
]

const SLOTS = ['Arma principal', 'Arma secundaria', 'Armadura', 'Casco', 'Guantes', 'Botas', 'Accesorio'] as const

export function InventoryVisualPrototype() {
  const [selected, setSelected] = useState<Item | null>(null)
  const [equipped, setEquipped] = useState<Record<string, Item>>({})
  const [comparing, setComparing] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const equip = (item: Item) => {
    setEquipped((current) => ({ ...current, [item.slot]: item }))
    setMessage(`${item.name} equipado en ${item.slot}.`)
  }
  const unequip = (slot: string) => {
    setEquipped((current) => {
      const next = { ...current }
      delete next[slot]
      return next
    })
    setMessage(`${slot} desequipado.`)
  }
  const acTotal = 10 + (equipped['Armadura']?.ac ? equipped['Armadura'].ac! - 10 : 0)
  const speed = 30 + (equipped['Botas'] ? 5 : 0)

  return (
    <div className="exp">
      <header className="exp__top">
        <div className="exp__brand"><span className="exp__mark">✦</span> ProjectVTT</div>
        <div className="exp__breadcrumb"><strong>Ficha de Elenar</strong><span>Equipo y perfil</span></div>
        <div className="exp__top-actions">
          <button type="button" className="pv-btn pv-btn--ghost">Perfil</button>
          <button type="button" className="pv-btn pv-btn--ghost">Habilidades</button>
          <button type="button" className="pv-btn pv-btn--primary">Equipo</button>
        </div>
      </header>

      <div className="exp__inventory">
        <section className="exp__bag" aria-label="Objetos del inventario">
          <div className="exp__panel-head"><h2>Mochila</h2><span>{ITEMS.length} objetos</span></div>
          <p className="exp__hint">Selecciona un objeto para verlo, compararlo o equiparlo.</p>
          <div className="exp__items">
            {ITEMS.map((item) => (
              <button key={item.id} type="button"
                className={`exp__item${selected?.id === item.id ? ' is-selected' : ''}`}
                onClick={() => { setSelected(item); setComparing(false) }}>
                <img src={item.icon} alt="" />
                <span>{item.name}</span>
                <small>{item.slot}</small>
              </button>
            ))}
          </div>
          {selected ? (
            <div className="exp__inspector">
              <span className="exp__label">INSPECTOR</span>
              <strong>{selected.name}</strong>
              <p>{selected.note}</p>
              {selected.damage ? <p>Daño {selected.damage}</p> : null}
              {selected.ac ? <p>Clase de armadura {selected.ac}</p> : null}
              <div className="exp__row-actions">
                <button type="button" className="pv-btn pv-btn--primary" onClick={() => equip(selected)}>Equipar</button>
                <button type="button" className="pv-btn pv-btn--ghost" onClick={() => setComparing(true)}>Comparar</button>
              </div>
              {comparing ? (
                <div className="exp__compare">
                  <span className="exp__label">COMPARACIÓN</span>
                  <div><span>Equipado ahora</span><strong>{equipped[selected.slot]?.name ?? 'Nada'}</strong></div>
                  <div><span>Este objeto</span><strong>{selected.name}</strong></div>
                  <p>{selected.damage ? `Daño ${selected.damage}` : selected.ac ? `CA ${selected.ac}` : selected.note}</p>
                </div>
              ) : null}
            </div>
          ) : null}
        </section>

        <section className="exp__center" aria-label="Personaje">
          <div className="exp__portrait-frame">
            <img src="/icons/rogue.svg" alt="" className="exp__portrait-silhouette" />
            <span className="exp__portrait-caption">Imagen de muestra · el jugador aporta la suya</span>
          </div>
          <div className="exp__effects">
            <span className="exp__label">EFECTOS DEL EQUIPO</span>
            <div className="exp__stat"><span>Clase de armadura</span><strong>{acTotal}</strong></div>
            <div className="exp__stat"><span>Velocidad</span><strong>{speed} ft</strong></div>
            <div className="exp__stat"><span>Arma principal</span><strong>{equipped['Arma principal']?.damage ?? '—'}</strong></div>
            <p className="exp__notice">La ilustración no cambia de armadura: los efectos sí se aplican a las estadísticas.</p>
          </div>
        </section>

        <aside className="exp__slots" aria-label="Objetos equipados">
          <div className="exp__panel-head"><h2>Equipado</h2><span>Por hueco del sistema</span></div>
          <div className="exp__slot-list">
            {SLOTS.map((slot) => {
              const item = equipped[slot]
              return (
                <div className={`exp__slot${item ? ' is-filled' : ''}`} key={slot}>
                  <span className="exp__slot-name">{slot}</span>
                  {item ? (
                    <>
                      <img src={item.icon} alt="" />
                      <span className="exp__slot-item">{item.name}</span>
                      <button type="button" onClick={() => unequip(slot)}>Desequipar</button>
                    </>
                  ) : (
                    <span className="exp__slot-empty">Vacío</span>
                  )}
                </div>
              )
            })}
          </div>
          <p className="exp__notice" role="status">{message ?? 'Arrastra un objeto o usa «Equipar». Ambas formas funcionan.'}</p>
        </aside>
      </div>
    </div>
  )
}
