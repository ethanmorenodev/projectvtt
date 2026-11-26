import { useMemo, useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import { SAMPLE_ASSETS, type ProtoAsset } from './replaceData'
import '../../../views/imageLibrary.css'

/**
 * Prototipo de sustitución de recursos (PMB-4.10, solo desarrollo):
 * el `imageId` permanece estable y las referencias no se rompen; se añaden
 * filtros por paquete/licencia y variantes derivadas. Datos de muestra.
 */

export function ReplaceAssetPrototype({ variant = 'normal' }: { variant?: 'normal' | 'vacio' }) {
  const [assets, setAssets] = useState<ProtoAsset[]>(
    () => (variant === 'vacio' ? [] : SAMPLE_ASSETS.map((item) => ({ ...item }))))
  const [pack, setPack] = useState('todos')
  const [license, setLicense] = useState('todas')
  const [selectedId, setSelectedId] = useState<string | null>(
    variant === 'vacio' ? null : 'img-dwarf')
  const [replaceOpen, setReplaceOpen] = useState(false)
  const [newName, setNewName] = useState('')
  const [notice, setNotice] = useState<string | null>(null)

  const packs = [...new Set(assets.map((item) => item.packKey ?? 'Propio'))].sort()
  const licenses = [...new Set(assets.map((item) => item.license))].sort()
  const visible = useMemo(() => assets.filter((item) =>
    (pack === 'todos' || (item.packKey ?? 'Propio') === pack)
    && (license === 'todas' || item.license === license)), [assets, pack, license])
  const selected = assets.find((item) => item.id === selectedId) ?? null

  const replace = () => {
    if (!selected || !newName.trim()) return
    const id = selected.id
    const references = selected.references.length
    setAssets((current) => current.map((item) => item.id === id
      ? {
          ...item,
          fileName: newName.trim(),
          sizeBytes: 220160,
          variants: ['thumb-sm', 'thumb-md', 'thumb-lg'],
        }
      : item))
    setReplaceOpen(false)
    setNewName('')
    setNotice(
      `Archivo «${newName.trim()}» sustituido: imageId ${id} inmutable; ${references} referencias intactas.`)
  }

  return (
    <PrototypeShell active="biblioteca">
      <div className="img-biblio">
        <header className="img-biblio__head">
          <div>
            <h1 className="biblio__title">Sustituir recursos sin romper referencias</h1>
            <p className="biblio__subtitle">
              Filtros por paquete/licencia · imageId estable · variantes derivadas
            </p>
          </div>
        </header>
        {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}

        <div className="img-biblio__body">
          <nav className="img-biblio__filters" aria-label="Filtros de recursos">
            <h2 className="biblio-zone__title">Filtros</h2>
            <div className="img-facet" role="group" aria-label="Paquete">
              <h3>Paquete</h3>
              {['todos', ...packs].map((value) => (
                <button key={value} type="button" aria-pressed={pack === value}
                  onClick={() => setPack(value)}>
                  {value === 'todos' ? 'Todos' : value}
                </button>
              ))}
            </div>
            <div className="img-facet" role="group" aria-label="Licencia">
              <h3>Licencia</h3>
              {['todas', ...licenses].map((value) => (
                <button key={value} type="button" aria-pressed={license === value}
                  onClick={() => setLicense(value)}>
                  {value === 'todas' ? 'Todas' : value}
                </button>
              ))}
            </div>
          </nav>

          <section className="img-biblio__grid" aria-label="Rejilla de recursos">
            {visible.length === 0 ? (
              <div className="biblio-empty">
                <p className="biblio-empty__title">No hay recursos con estos filtros.</p>
              </div>
            ) : (
              <ul className="img-grid__tiles">
                {visible.map((item) => (
                  <li key={item.id}>
                    <button type="button"
                      className={`img-card ${selectedId === item.id ? 'is-selected' : ''}`}
                      onClick={() => setSelectedId(item.id)}>
                      <span className="img-card__thumb" aria-hidden="true">
                        {item.fileName.slice(0, 1).toUpperCase()}
                      </span>
                      <span className="img-card__name">{item.fileName}</span>
                      <span className="img-card__meta">
                        <span className="biblio-tag">{item.license}</span>
                        <span className="biblio-tag">{item.packKey ?? 'Propio'}</span>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <aside className="img-biblio__inspector" aria-label="Detalle del recurso">
            <h2 className="biblio-zone__title">Detalle</h2>
            {!selected ? (
              <p className="biblio-inspector__empty">Selecciona un recurso.</p>
            ) : (
              <div className="biblio-inspector__body">
                <h3 className="biblio-inspector__name">{selected.fileName}</h3>
                <p className="biblio-inspector__summary">
                  imageId: <code>{selected.id}</code>
                </p>
                <h4 className="biblio-inspector__section">Referencias</h4>
                <p className="biblio-inspector__note">
                  Usado por {selected.references.length} elemento(s)
                </p>
                <ul className="img-meta">
                  {selected.references.map((name) => <li key={name}>{name}</li>)}
                </ul>
                <h4 className="biblio-inspector__section">Variantes</h4>
                <div className="biblio-inspector__tags">
                  {selected.variants.length === 0 ? (
                    <span className="biblio-tag">sin variantes</span>
                  ) : selected.variants.map((value) => (
                    <span key={value} className="biblio-tag">{value}</span>
                  ))}
                </div>
                <button type="button" className="pv-btn pv-btn--primary"
                  onClick={() => setReplaceOpen(true)}>
                  Sustituir archivo
                </button>
                <p className="biblio-inspector__note">
                  La sustitución conserva el imageId: las fichas y escenas siguen apuntando al mismo recurso.
                </p>
              </div>
            )}
          </aside>
        </div>
      </div>

      {replaceOpen && selected && (
        <div className="pv-confirm__overlay" onClick={() => setReplaceOpen(false)}>
          <div className="pv-confirm" role="dialog" aria-label="Sustituir archivo"
            onClick={(event) => event.stopPropagation()}>
            <h2 className="pv-confirm__title">Sustituir «{selected.fileName}»</h2>
            <label className="img-replace-field">
              Nuevo archivo
              <input value={newName} placeholder="nuevo-enano.png"
                onChange={(event) => setNewName(event.target.value)} />
            </label>
            <p className="biblio-inspector__note">
              Se sobrescribe el mismo almacenamiento; el id y las {selected.references.length} referencias no cambian.
            </p>
            <div className="pv-confirm__actions">
              <button type="button" className="pv-btn pv-btn--ghost"
                onClick={() => setReplaceOpen(false)}>Cancelar</button>
              <button type="button" className="pv-btn pv-btn--primary" onClick={replace}>
                Sustituir
              </button>
            </div>
          </div>
        </div>
      )}
    </PrototypeShell>
  )
}
