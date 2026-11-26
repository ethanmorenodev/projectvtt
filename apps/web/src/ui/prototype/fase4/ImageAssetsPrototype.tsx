import { useMemo, useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import {
  ROLE_LABELS,
  SAMPLE_IMAGES,
  SAMPLE_PACKS,
  attributionNote,
  type ImageRole,
  type ProtoImage,
} from './imageAssetsData'
import '../../../views/imageLibrary.css'

/**
 * Prototipo de biblioteca de imágenes y créditos (PMB-4.7, solo desarrollo).
 * Rutas: `#prototipo-imagenes[-vacio]` y `#prototipo-creditos`.
 * Datos de muestra: dos recursos locales reales y marcadores de posición;
 * el backend y la app real NO se tocan en esta fase.
 */

type RoleFilter = 'todos' | ImageRole

export function ImageLibraryPrototype({ variant = 'normal' }: { variant?: 'normal' | 'vacio' }) {
  const images = variant === 'vacio' ? [] : SAMPLE_IMAGES
  const [role, setRole] = useState<RoleFilter>('todos')
  const [album, setAlbum] = useState('todos')
  const [tag, setTag] = useState('todos')
  const [license, setLicense] = useState('todos')
  const [query, setQuery] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(
    variant === 'vacio' ? null : 'img-dwarf')
  const [lightboxOpen, setLightboxOpen] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)

  const albums = [...new Set(images.flatMap((image) => image.albums))].sort()
  const tags = [...new Set(images.flatMap((image) => image.tags))].sort()
  const licenses = [...new Set(images.map((image) => image.license))].sort()

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase()
    return images.filter((image) => {
      if (role !== 'todos' && image.role !== role) return false
      if (album !== 'todos' && !image.albums.includes(album)) return false
      if (tag !== 'todos' && !image.tags.includes(tag)) return false
      if (license !== 'todos' && image.license !== license) return false
      return !q || image.name.toLowerCase().includes(q)
    })
  }, [images, role, album, tag, license, query])

  const selected = images.find((image) => image.id === selectedId) ?? null

  const clearFilters = () => {
    setRole('todos')
    setAlbum('todos')
    setTag('todos')
    setLicense('todos')
    setQuery('')
  }

  return (
    <PrototypeShell active="biblioteca">
      <div className="img-biblio">
        <header className="img-biblio__head">
          <div>
            <h1 className="biblio__title">Biblioteca de imágenes</h1>
            <p className="biblio__subtitle">
              Iconos, miniaturas e ilustraciones · reutilización por referencia
            </p>
          </div>
          <button type="button" className="biblio__create" onClick={() =>
            setNotice('La importación de imágenes se construye en la integración.')}>
            Importar imagen
          </button>
        </header>

        <div className="img-biblio__body">
          <nav className="img-biblio__filters" aria-label="Filtros de imágenes">
            <h2 className="biblio-zone__title">Filtros</h2>
            <div className="img-facet" role="group" aria-label="Papel">
              <h3>Papel</h3>
              {(['todos', 'icono', 'miniatura', 'ilustracion'] as RoleFilter[]).map((value) => (
                <button key={value} type="button" aria-pressed={role === value}
                  onClick={() => setRole(value)}>
                  {value === 'todos' ? 'Todos' : ROLE_LABELS[value]}
                </button>
              ))}
            </div>
            <div className="img-facet" role="group" aria-label="Álbum">
              <h3>Álbum</h3>
              {['todos', ...albums].map((value) => (
                <button key={value} type="button" aria-pressed={album === value}
                  onClick={() => setAlbum(value)}>
                  {value === 'todos' ? 'Todos' : value}
                </button>
              ))}
            </div>
            <div className="img-facet" role="group" aria-label="Etiqueta">
              <h3>Etiqueta</h3>
              {['todos', ...tags].map((value) => (
                <button key={value} type="button" aria-pressed={tag === value}
                  onClick={() => setTag(value)}>
                  {value === 'todos' ? 'Todas' : value}
                </button>
              ))}
            </div>
            <div className="img-facet" role="group" aria-label="Licencia">
              <h3>Licencia</h3>
              {['todos', ...licenses].map((value) => (
                <button key={value} type="button" aria-pressed={license === value}
                  onClick={() => setLicense(value)}>
                  {value === 'todos' ? 'Todas' : value}
                </button>
              ))}
            </div>
            <button type="button" className="biblio-empty__action" onClick={clearFilters}>
              Limpiar filtros
            </button>
          </nav>

          <section className="img-biblio__grid" aria-label="Rejilla de imágenes">
            <div className="biblio-toolbar">
              <input
                className="biblio-toolbar__search"
                aria-label="Buscar imágenes"
                placeholder="Buscar imágenes…"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
              <span className="biblio-toolbar__count">{visible.length} imagen(es)</span>
            </div>
            {visible.length === 0 ? (
              <div className="biblio-empty">
                <p className="biblio-empty__title">No hay imágenes con estos filtros.</p>
                <p className="biblio-empty__text">
                  Importa una imagen o prueba con otros filtros.
                </p>
              </div>
            ) : (
              <ul className="img-grid__tiles">
                {visible.map((image) => (
                  <li key={image.id}>
                    <button
                      type="button"
                      className={`img-card ${selectedId === image.id ? 'is-selected' : ''}`}
                      onClick={() => setSelectedId(image.id)}
                    >
                      <span className="img-card__thumb" aria-hidden="true">
                        {image.src ? (
                          <img src={image.src} alt="" />
                        ) : (
                          <span>{image.glyph ?? image.name.slice(0, 1)}</span>
                        )}
                      </span>
                      <span className="img-card__name">{image.name}</span>
                      <span className="img-card__meta">
                        <span className="biblio-tag">{ROLE_LABELS[image.role]}</span>
                        <span className="biblio-tag">{image.license}</span>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <aside className="img-biblio__inspector" aria-label="Detalle de imagen">
            <h2 className="biblio-zone__title">Detalle</h2>
            {!selected ? (
              <p className="biblio-inspector__empty">
                Selecciona una imagen de la rejilla para ver su detalle.
              </p>
            ) : (
              <div className="biblio-inspector__body">
                <h3 className="biblio-inspector__name">{selected.name}</h3>
                <p className="biblio-inspector__summary">
                  {ROLE_LABELS[selected.role]} · {selected.albums.join(', ')}
                </p>
                <h4 className="biblio-inspector__section">Etiquetas</h4>
                <div className="biblio-inspector__tags">
                  {selected.tags.map((label) => (
                    <span key={label} className="biblio-tag">{label}</span>
                  ))}
                </div>
                <h4 className="biblio-inspector__section">Procedencia y licencia</h4>
                <ul className="img-meta">
                  <li>Autor: <strong>{selected.author}</strong></li>
                  <li>Origen: <strong>{selected.origin}</strong></li>
                  <li>Licencia: <strong>{selected.license}</strong></li>
                  {selected.licenseUrl && (
                    <li>
                      <a href={selected.licenseUrl} target="_blank" rel="noreferrer">
                        Ver licencia
                      </a>
                    </li>
                  )}
                </ul>
                <p className="biblio-inspector__summary">{selected.attribution}</p>
                <div className="img-actions">
                  <button type="button" onClick={() => setLightboxOpen(true)}>
                    Ampliar
                  </button>
                  <button type="button" onClick={() =>
                    setNotice(`«${selected.name}» se reutiliza por referencia (imageId).`)}>
                    Usar en elemento
                  </button>
                  <button type="button" onClick={() =>
                    setNotice('La imagen se sustituye conservando su imageId: las referencias no se rompen.')}>
                    Sustituir imagen
                  </button>
                </div>
                <p className="biblio-inspector__note">
                  La ilustración grande se muestra solo bajo petición; las listas usan miniaturas cuadradas.
                </p>
              </div>
            )}
            {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}
          </aside>
        </div>
      </div>

      {lightboxOpen && selected && (
        <div className="pv-confirm__overlay" onClick={() => setLightboxOpen(false)}>
          <div className="img-lightbox" role="dialog" aria-label="Vista ampliada"
            onClick={(event) => event.stopPropagation()}>
            <h2>{selected.name}</h2>
            <div className="img-lightbox__stage">
              {selected.src ? (
                <img src={selected.src} alt={selected.name} />
              ) : (
                <span aria-hidden="true">{selected.glyph}</span>
              )}
            </div>
            <p>{selected.attribution}</p>
            <button type="button" className="pv-btn pv-btn--ghost"
              onClick={() => setLightboxOpen(false)}>Cerrar</button>
          </div>
        </div>
      )}
    </PrototypeShell>
  )
}

export function CreditsPrototype() {
  const [exported, setExported] = useState(false)

  return (
    <PrototypeShell active="campanas">
      <div className="img-credits">
        <h1>Créditos y licencias</h1>
        <p className="biblio__subtitle">
          Procedencia registrada de paquetes e imágenes (ADR-036)
        </p>

        <section aria-label="Paquetes de contenido">
          <h2 className="biblio-zone__title">Paquetes</h2>
          {SAMPLE_PACKS.map((pack) => (
            <div className="img-credit" key={pack.key}>
              <strong>{pack.name}</strong>
              <span className="biblio-tag">{pack.license}</span>
              <p>{pack.attribution}</p>
              <a href={pack.licenseUrl} target="_blank" rel="noreferrer">Ver licencia</a>
            </div>
          ))}
        </section>

        <section aria-label="Imágenes de la biblioteca">
          <h2 className="biblio-zone__title">Imágenes</h2>
          {SAMPLE_IMAGES.map((image) => (
            <div className="img-credit" key={image.id}>
              <strong>{image.name}</strong>
              <span className="biblio-tag">{ROLE_LABELS[image.role]}</span>
              <span className="biblio-tag">{image.license}</span>
              <p>{image.attribution}</p>
            </div>
          ))}
        </section>

        <button type="button" className="pv-btn pv-btn--primary" onClick={() => setExported(true)}>
          Exportar nota de atribución
        </button>
        {exported && (
          <textarea
            aria-label="Nota de atribución"
            readOnly
            value={attributionNote()}
            className="img-credits__export"
          />
        )}
        <p className="biblio-inspector__note">
          No se elimina ninguna atribución obligatoria: los paquetes importados conservan sus metadatos.
        </p>
      </div>
    </PrototypeShell>
  )
}

export type { ProtoImage }
