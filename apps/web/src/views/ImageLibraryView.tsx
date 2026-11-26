import { useEffect, useMemo, useState } from 'react'
import { apiFetch, apiFetchBlob } from '../api/client'
import { t } from '../i18n'
import type { ImageAssetDto } from '../api/types'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Icon } from '../ui/Icon'
import { Button } from '../ui'
import './imageLibrary.css'

/** Papel visual de un asset (ADR-035 §3.5). */
type RoleFilter = 'todos' | 'icono' | 'miniatura' | 'ilustracion'

function roleLabel(role: string): string {
  const labels = t.imagenes.roles as Record<string, string>
  return labels[role] ?? role
}

/** El endpoint de archivo exige sesión: se carga como blob con el token. */
function Thumb({ id, name }: { id: string; name: string }) {
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    let cancelled = false
    let objectUrl: string | null = null
    void apiFetchBlob(`/api/v1/images/${id}/file`)
      .then((blob) => {
        if (!cancelled) {
          objectUrl = URL.createObjectURL(blob)
          setUrl(objectUrl)
        }
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [id])
  if (url && !failed) {
    return <img src={url} alt="" onError={() => setFailed(true)} />
  }
  return <span aria-hidden="true">{name.slice(0, 1).toUpperCase()}</span>
}

/**
 * Biblioteca de imágenes real (PMB-4.7): facetas, rejilla de miniaturas,
 * inspector con procedencia/licencia, lightbox bajo petición y alta de
 * imágenes con validación en servidor.
 */
export function ImageLibraryView() {
  const [images, setImages] = useState<ImageAssetDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [role, setRole] = useState<RoleFilter>('todos')
  const [album, setAlbum] = useState('todos')
  const [tag, setTag] = useState('todos')
  const [license, setLicense] = useState('todos')
  const [pack, setPack] = useState('todos')
  const [query, setQuery] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [lightbox, setLightbox] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [replaceOpen, setReplaceOpen] = useState(false)
  const [replaceFile, setReplaceFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [uploadOpen, setUploadOpen] = useState(false)
  const [upload, setUpload] = useState({
    file: null as File | null,
    role: 'miniatura',
    albums: '',
    tags: '',
    license: '',
    attribution: '',
  })

  const reload = async () => {
    try {
      const params = new URLSearchParams()
      if (role !== 'todos') params.set('role', role)
      if (album !== 'todos') params.set('album', album)
      if (tag !== 'todos') params.set('tag', tag)
      if (license !== 'todos') params.set('license', license)
      if (pack !== 'todos') params.set('packKey', pack)
      if (query.trim()) params.set('query', query.trim())
      setImages(await apiFetch<ImageAssetDto[]>(`/api/v1/images?${params.toString()}`))
      setError(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.biblioteca.error)
      setImages(null)
    }
  }

  useEffect(() => {
    void reload()
    // La recarga la dispara el cambio de filtros; `reload` no es estable.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [role, album, tag, license, pack, query])

  const albums = useMemo(
    () => [...new Set((images ?? []).flatMap((image) => image.albums))].sort(),
    [images])
  const tags = useMemo(
    () => [...new Set((images ?? []).flatMap((image) => image.tags))].sort(),
    [images])
  const licenses = useMemo(
    () => [...new Set((images ?? []).map((image) => image.license))].sort(),
    [images])
  const packs = useMemo(
    () => [...new Set((images ?? []).map((image) => image.packKey ?? t.imagenes.own))].sort(),
    [images])
  const selected = (images ?? []).find((image) => image.id === selectedId) ?? null

  const clearFilters = () => {
    setRole('todos')
    setAlbum('todos')
    setTag('todos')
    setLicense('todos')
    setPack('todos')
    setQuery('')
  }

  const submitUpload = async () => {
    if (!upload.file) {
      return
    }
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', upload.file)
      form.append('role', upload.role)
      form.append('albums', upload.albums)
      form.append('tags', upload.tags)
      form.append('license', upload.license)
      form.append('attribution', upload.attribution)
      await apiFetch<ImageAssetDto>('/api/v1/images', { method: 'POST', body: form })
      setUpload({ file: null, role: 'miniatura', albums: '', tags: '', license: '', attribution: '' })
      setUploadOpen(false)
      setNotice(t.imagenes.upload + ': ' + upload.file.name)
      await reload()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.imagenes.uploadError)
    } finally {
      setBusy(false)
    }
  }

  const remove = async () => {
    if (!selected) return
    setDeleteOpen(false)
    try {
      await apiFetch<void>(`/api/v1/images/${selected.id}`, { method: 'DELETE' })
      setSelectedId(null)
      await reload()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.biblioteca.error)
    }
  }

  const submitReplace = async () => {
    if (!selected || !replaceFile) return
    setReplaceOpen(false)
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', replaceFile)
      const dto = await apiFetch<ImageAssetDto>(
        `/api/v1/images/${selected.id}/replace`, { method: 'POST', body: form })
      setReplaceFile(null)
      setNotice(t.imagenes.replaced(dto.fileName, dto.id, dto.referenceCount))
      await reload()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.imagenes.uploadError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="img-biblio">
      <header className="img-biblio__head">
        <div>
          <h1 className="biblio__title">{t.imagenes.title}</h1>
          <p className="biblio__subtitle">{t.imagenes.subtitle}</p>
        </div>
        <Button variant="primary" onClick={() => setUploadOpen(true)}>
          {t.imagenes.upload}
        </Button>
      </header>

      {error ? (
        <div className="pv-list-error" role="alert">
          <p>{error}</p>
          <button type="button" className="pv-btn pv-btn--secondary" onClick={() => void reload()}>
            {t.common.retry}
          </button>
        </div>
      ) : null}
      {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}

      {uploadOpen && (
      <section className="img-upload img-upload--panel" aria-label={t.imagenes.import}>
        <header className="img-upload__head">
          <h2>{t.imagenes.upload}</h2>
          <button type="button" onClick={() => setUploadOpen(false)}
            aria-label={t.imagenes.closeUpload}>×</button>
        </header>
        <p className="img-upload__hint">{t.imagenes.uploadHint}</p>
        <div className="img-upload__fields">
        <input
          id="img-upload-input"
          type="file"
          aria-label={t.imagenes.file}
          accept="image/png,image/jpeg,image/webp,image/svg+xml"
          onChange={(event) => setUpload({ ...upload, file: event.target.files?.[0] ?? null })}
        />
        <select
          aria-label={t.imagenes.role}
          value={upload.role}
          onChange={(event) => setUpload({ ...upload, role: event.target.value })}
        >
          {Object.entries(t.imagenes.roles).map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <input
          aria-label={t.imagenes.album}
          placeholder={t.imagenes.album}
          value={upload.albums}
          onChange={(event) => setUpload({ ...upload, albums: event.target.value })}
        />
        <input
          aria-label={t.imagenes.tag}
          placeholder={t.imagenes.tag}
          value={upload.tags}
          onChange={(event) => setUpload({ ...upload, tags: event.target.value })}
        />
        <input
          aria-label={t.imagenes.license}
          placeholder={t.imagenes.license}
          value={upload.license}
          onChange={(event) => setUpload({ ...upload, license: event.target.value })}
        />
        <button type="button" disabled={busy || !upload.file} onClick={() => void submitUpload()}>
          {t.imagenes.upload}
        </button>
        </div>
      </section>
      )}

      <div className="img-biblio__body">
        <nav className="img-biblio__filters" aria-label={t.imagenes.filters}>
          <h2 className="biblio-zone__title">{t.imagenes.filters}</h2>
          <div className="img-facet" role="group" aria-label={t.imagenes.role}>
            <h3>{t.imagenes.role}</h3>
            {(['todos', 'icono', 'miniatura', 'ilustracion'] as RoleFilter[]).map((value) => (
              <button key={value} type="button" aria-pressed={role === value}
                onClick={() => setRole(value)}>
                {value === 'todos' ? t.imagenes.all : roleLabel(value)}
              </button>
            ))}
          </div>
          <div className="img-facet" role="group" aria-label={t.imagenes.album}>
            <h3>{t.imagenes.album}</h3>
            {['todos', ...albums].map((value) => (
              <button key={value} type="button" aria-pressed={album === value}
                onClick={() => setAlbum(value)}>
                {value === 'todos' ? t.imagenes.all : value}
              </button>
            ))}
          </div>
          <div className="img-facet" role="group" aria-label={t.imagenes.tag}>
            <h3>{t.imagenes.tag}</h3>
            {['todos', ...tags].map((value) => (
              <button key={value} type="button" aria-pressed={tag === value}
                onClick={() => setTag(value)}>
                {value === 'todos' ? t.imagenes.allTags : value}
              </button>
            ))}
          </div>
          <div className="img-facet" role="group" aria-label={t.imagenes.license}>
            <h3>{t.imagenes.license}</h3>
            {['todos', ...licenses].map((value) => (
              <button key={value} type="button" aria-pressed={license === value}
                onClick={() => setLicense(value)}>
                {value === 'todos' ? t.imagenes.allTags : value}
              </button>
            ))}
          </div>
          <div className="img-facet" role="group" aria-label={t.imagenes.pack}>
            <h3>{t.imagenes.pack}</h3>
            {['todos', ...packs].map((value) => (
              <button key={value} type="button" aria-pressed={pack === value}
                onClick={() => setPack(value)}>
                {value === 'todos' ? t.imagenes.all : value}
              </button>
            ))}
          </div>
          <button type="button" className="biblio-empty__action" onClick={clearFilters}>
            {t.imagenes.clear}
          </button>
        </nav>

        <section className="img-biblio__grid" aria-label="Rejilla de imágenes">
          <div className="biblio-toolbar">
            <input
              className="biblio-toolbar__search"
              aria-label={t.imagenes.search}
              placeholder={t.imagenes.search}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <span className="biblio-toolbar__count">
              {t.imagenes.count((images ?? []).length)}
            </span>
          </div>
          {error ? null : images === null ? (
            <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
          ) : images.length === 0 ? (
            <div className="biblio-empty">
              <Icon name="open-book" />
              <p className="biblio-empty__title">{t.imagenes.empty}</p>
              <p className="biblio-empty__text">{t.imagenes.emptyHint}</p>
              <Button variant="primary" onClick={() => setUploadOpen(true)}>
                {t.imagenes.upload}
              </Button>
            </div>
          ) : (
            <ul className="img-grid__tiles">
              {images.map((image) => (
                <li key={image.id}>
                  <button
                    type="button"
                    className={`img-card ${selectedId === image.id ? 'is-selected' : ''}`}
                    onClick={() => setSelectedId(image.id)}
                  >
                    <span className="img-card__thumb">
                      <Thumb id={image.id} name={image.fileName} />
                    </span>
                    <span className="img-card__name">{image.fileName}</span>
                    <span className="img-card__meta">
                      <span className="biblio-tag">{roleLabel(image.role)}</span>
                      <span className="biblio-tag">{image.license}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <aside className="img-biblio__inspector" aria-label={t.imagenes.detail}>
          <h2 className="biblio-zone__title">{t.imagenes.detail}</h2>
          {!selected ? (
            <p className="biblio-inspector__empty">{t.imagenes.detailEmpty}</p>
          ) : (
            <div className="biblio-inspector__body">
              <h3 className="biblio-inspector__name">{selected.fileName}</h3>
              <p className="biblio-inspector__summary">
                {roleLabel(selected.role)} · {selected.albums.join(', ')}
              </p>
              <h4 className="biblio-inspector__section">{t.imagenes.tags}</h4>
              <div className="biblio-inspector__tags">
                {selected.tags.map((label) => (
                  <span key={label} className="biblio-tag">{label}</span>
                ))}
              </div>
              <h4 className="biblio-inspector__section">{t.imagenes.provenance}</h4>
              <ul className="img-meta">
                <li>{t.imagenes.author}: <strong>{selected.author}</strong></li>
                <li>{t.imagenes.origin}: <strong>{selected.origin}</strong></li>
                <li>{t.imagenes.license}: <strong>{selected.license}</strong></li>
                {selected.licenseUrl && (
                  <li>
                    <a href={selected.licenseUrl} target="_blank" rel="noreferrer">
                      {t.imagenes.licenseLink}
                    </a>
                  </li>
                )}
              </ul>
              {selected.attribution.map((line) => (
                <p className="biblio-inspector__summary" key={line}>{line}</p>
              ))}
              <h4 className="biblio-inspector__section">{t.imagenes.references}</h4>
              <p className="biblio-inspector__note">
                {t.imagenes.referenceCount(selected.referenceCount)}
              </p>
              <h4 className="biblio-inspector__section">{t.imagenes.variants}</h4>
              <div className="biblio-inspector__tags">
                {selected.variants.length === 0 ? (
                  <span className="biblio-tag">—</span>
                ) : selected.variants.map((variant) => (
                  <span key={variant} className="biblio-tag">{variant}</span>
                ))}
              </div>
              <div className="img-actions">
                <button type="button" onClick={() => setLightbox(true)}>{t.imagenes.enlarge}</button>
                <button type="button" onClick={() => setNotice(t.imagenes.reuseNote(selected.fileName))}>
                  {t.imagenes.useInItem}
                </button>
                <button type="button" onClick={() => setReplaceOpen(true)}>
                  {t.imagenes.replaceFile}
                </button>
                <button type="button" onClick={() => setDeleteOpen(true)}>
                  {t.imagenes.delete}
                </button>
              </div>
              <p className="biblio-inspector__note">{t.imagenes.lightboxHint}</p>
              <p className="biblio-inspector__note">{t.imagenes.replaceNote}</p>
            </div>
          )}
        </aside>
      </div>

      {lightbox && selected && (
        <div className="pv-confirm__overlay" onClick={() => setLightbox(false)}>
          <div className="img-lightbox" role="dialog" aria-label={t.imagenes.lightbox}
            onClick={(event) => event.stopPropagation()}>
            <h2>{selected.fileName}</h2>
            <div className="img-lightbox__stage"><Thumb id={selected.id} name={selected.fileName} /></div>
            {selected.attribution.map((line) => <p key={line}>{line}</p>)}
            <button type="button" className="pv-btn pv-btn--ghost" onClick={() => setLightbox(false)}>
              {t.imagenes.close}
            </button>
          </div>
        </div>
      )}

      {selected && (
        <ConfirmDialog
          open={deleteOpen}
          title={t.imagenes.delete}
          text={selected.fileName}
          confirmLabel={t.imagenes.delete}
          cancelLabel={t.editor.actions.cancel}
          onConfirm={() => void remove()}
          onCancel={() => setDeleteOpen(false)}
        />
      )}

      {selected && replaceOpen && (
        <div className="pv-confirm__overlay" onClick={() => setReplaceOpen(false)}>
          <div className="pv-confirm" role="dialog"
            aria-label={t.imagenes.replaceTitle(selected.fileName)}
            onClick={(event) => event.stopPropagation()}>
            <h2 className="pv-confirm__title">
              {t.imagenes.replaceTitle(selected.fileName)}
            </h2>
            <label className="img-replace-field">
              {t.imagenes.replaceNewFile}
              <input type="file"
                accept="image/png,image/jpeg,image/webp,image/svg+xml"
                onChange={(event) => setReplaceFile(event.target.files?.[0] ?? null)}
              />
            </label>
            <p className="biblio-inspector__note">
              {t.imagenes.replaceMessage(selected.referenceCount)}
            </p>
            <p className="biblio-inspector__note">{t.imagenes.replaceNoteOld}</p>
            <div className="pv-confirm__actions">
              <button type="button" className="pv-btn pv-btn--ghost"
                onClick={() => setReplaceOpen(false)}>
                {t.imagenes.close}
              </button>
              <button type="button" className="pv-btn pv-btn--primary"
                disabled={!replaceFile || busy} onClick={() => void submitReplace()}>
                {t.imagenes.replaceFile}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
