import { useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import {
  SAMPLE_EQUIPMENT,
  SLOT_LABELS,
  SLOT_ORDER,
  type ProtoEquipment,
} from './inventoryData'
import '../../../views/inventory.css'

/**
 * Prototipo de inventario visual (PMB-6.3, solo desarrollo): silueta con
 * huecos de equipamiento (referencia BG3), mochila e inspector con
 * estadísticas/efectos. Datos de muestra; la app real no se toca.
 */

export function InventoryPrototype({ variant = 'normal' }: { variant?: 'normal' | 'vacio' }) {
  const [items, setItems] = useState<ProtoEquipment[]>(
    () => (variant === 'vacio'
      ? []
      : SAMPLE_EQUIPMENT.map((item) => ({ ...item }))))
  const [selectedId, setSelectedId] = useState<string | null>(
    variant === 'vacio' ? null : 'eq-longsword')
  const [targetSlot, setTargetSlot] = useState('main-hand')
  const [notice, setNotice] = useState<string | null>(null)

  const selected = items.find((item) => item.id === selectedId) ?? null
  const inSlot = (slotId: string) => items.find((item) => item.slotId === slotId) ?? null
  const backpack = items.filter((item) => item.slotId === null)

  const equip = () => {
    if (!selected || inSlot(targetSlot)) {
      setNotice('El hueco ya está ocupado.')
      return
    }
    setItems((current) => current.map((item) =>
      item.id === selected.id ? { ...item, slotId: targetSlot } : item))
    setNotice(`«${selected.name}» equipado en ${SLOT_LABELS[targetSlot]}.`)
  }

  const unequip = (id: string) => {
    const item = items.find((entry) => entry.id === id)
    if (!item) return
    setItems((current) => current.map((entry) =>
      entry.id === id ? { ...entry, slotId: null } : entry))
    setNotice(`«${item.name}» devuelto a la mochila.`)
  }

  return (
    <PrototypeShell active="campana">
      <div className="inv">
        <header className="inv__head">
          <div>
            <h1 className="biblio__title">Inventario visual</h1>
            <p className="biblio__subtitle">
              Silueta con huecos configurables · mochila · estadísticas y efectos
            </p>
          </div>
        </header>
        {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}

        <div className="inv__body">
          <section className="inv__backpack" aria-label="Mochila">
            <h2 className="biblio-zone__title">Mochila · {backpack.length} objeto(s)</h2>
            {backpack.length === 0 ? (
              <p className="biblio-inspector__empty">La mochila está vacía.</p>
            ) : (
              <ul className="inv__grid">
                {backpack.map((item) => (
                  <li key={item.id}>
                    <button type="button"
                      className={`inv-card ${selectedId === item.id ? 'is-selected' : ''}`}
                      onClick={() => setSelectedId(item.id)}>
                      <span className="inv-card__glyph" aria-hidden="true">{item.glyph}</span>
                      <span className="inv-card__name">{item.name}</span>
                      <span className="inv-card__meta">{item.weight} lb</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="inv__silhouette" aria-label="Silueta de equipamiento">
            <div className="inv-silhouette">
              <span className="inv-silhouette__figure" aria-hidden="true">🧍</span>
              {SLOT_ORDER.map((slotId, index) => {
                const item = inSlot(slotId)
                return (
                  <button key={slotId} type="button"
                    className={`inv-slot inv-slot--${index}`}
                    aria-label={`${SLOT_LABELS[slotId]}${item ? `: ${item.name}` : ''}`}
                    onClick={() => item && setSelectedId(item.id)}>
                    {item ? (
                      <>
                        <span aria-hidden="true">{item.glyph}</span>
                        <span className="inv-slot__name">{item.name}</span>
                      </>
                    ) : (
                      <span className="inv-slot__empty">{SLOT_LABELS[slotId]}</span>
                    )}
                  </button>
                )
              })}
            </div>
          </section>

          <aside className="img-biblio__inspector" aria-label="Detalle del objeto">
            <h2 className="biblio-zone__title">Detalle</h2>
            {!selected ? (
              <p className="biblio-inspector__empty">Selecciona un objeto.</p>
            ) : (
              <div className="biblio-inspector__body">
                <h3 className="biblio-inspector__name">{selected.name}</h3>
                <p className="biblio-inspector__summary">
                  {selected.weight} lb · {selected.value} po ·{' '}
                  <span className="biblio-tag">{selected.rarity}</span>
                </p>
                {selected.container && (
                  <p className="biblio-inspector__note">
                    Contenedor: {selected.container}
                  </p>
                )}
                <h4 className="biblio-inspector__section">Efectos</h4>
                {selected.effects.length === 0 ? (
                  <p className="biblio-inspector__note">Sin efectos declarados.</p>
                ) : (
                  <ul className="img-meta">
                    {selected.effects.map((effect) => <li key={effect}>{effect}</li>)}
                  </ul>
                )}
                <h4 className="biblio-inspector__section">Equipar</h4>
                <div className="inv-actions">
                  <select aria-label="Hueco" value={targetSlot}
                    onChange={(event) => setTargetSlot(event.target.value)}>
                    {SLOT_ORDER.map((slotId) => (
                      <option key={slotId} value={slotId}>{SLOT_LABELS[slotId]}</option>
                    ))}
                  </select>
                  <button type="button" onClick={equip}>Equipar</button>
                  {selected.slotId && (
                    <button type="button" onClick={() => unequip(selected.id)}>
                      Desequipar
                    </button>
                  )}
                </div>
              </div>
            )}
          </aside>
        </div>
      </div>
    </PrototypeShell>
  )
}
