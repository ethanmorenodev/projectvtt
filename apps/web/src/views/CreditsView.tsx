import { useEffect, useState } from 'react'
import { apiFetch, apiFetchBlob } from '../api/client'
import { t } from '../i18n'
import type { ContentPackDto, ImageAssetDto } from '../api/types'
import './imageLibrary.css'

/** Créditos y licencias (PMB-4.7, ADR-036): procedencia y exportación. */
export function CreditsView() {
  const [packs, setPacks] = useState<ContentPackDto[]>([])
  const [images, setImages] = useState<ImageAssetDto[]>([])
  const [note, setNote] = useState<string | null>(null)

  useEffect(() => {
    void apiFetch<ContentPackDto[]>('/api/v1/content/packs').then(setPacks).catch(() => undefined)
    void apiFetch<ImageAssetDto[]>('/api/v1/images').then(setImages).catch(() => undefined)
  }, [])

  const exportNote = async () => {
    try {
      setNote(await (await apiFetchBlob('/api/v1/credits/export')).text())
    } catch {
      setNote(null)
    }
  }

  return (
    <div className="img-credits">
      <header className="img-credits__head">
        <div>
          <h1 className="biblio__title">{t.creditos.title}</h1>
          <p className="biblio__subtitle">{t.creditos.subtitle}</p>
        </div>
        <button type="button" className="pv-btn pv-btn--primary" onClick={() => void exportNote()}>
          {t.creditos.export}
        </button>
      </header>

      <section aria-label={t.creditos.packs}>
        <h2 className="biblio-zone__title">{t.creditos.packs}</h2>
        <div className="img-credits__grid">
          {packs.map((pack) => (
            <article className="img-credit" key={pack.id}>
              <div className="img-credit__head">
                <strong>{pack.name}</strong>
                <span className="biblio-tag">{pack.license}</span>
              </div>
              <span className="img-credit__meta">v{pack.version} · {pack.system}</span>
              {pack.attribution.map((line) => <p key={line}>{line}</p>)}
              {pack.licenseUrl && (
                <a href={pack.licenseUrl} target="_blank" rel="noreferrer">
                  {t.creditos.licenseLink} →
                </a>
              )}
            </article>
          ))}
        </div>
      </section>

      <section aria-label={t.creditos.images}>
        <h2 className="biblio-zone__title">{t.creditos.images}</h2>
        {images.length === 0 ? (
          <p className="biblio-inspector__empty">{t.creditos.noImages}</p>
        ) : (
          <div className="img-credits__grid">
            {images.map((image) => (
              <article className="img-credit" key={image.id}>
                <div className="img-credit__head">
                  <strong>{image.fileName}</strong>
                  <span className="biblio-tag">{image.license || '—'}</span>
                </div>
                {image.attribution.map((line) => <p key={line}>{line}</p>)}
                {image.licenseUrl && (
                  <a href={image.licenseUrl} target="_blank" rel="noreferrer">
                    {t.creditos.licenseLink} →
                  </a>
                )}
              </article>
            ))}
          </div>
        )}
      </section>

      <section aria-label={t.creditos.icons}>
        <h2 className="biblio-zone__title">{t.creditos.icons}</h2>
        <div className="img-credits__grid">
          <article className="img-credit">
            <div className="img-credit__head">
              <strong>{t.creditos.artLicense}</strong>
              <span className="biblio-tag">CC0 1.0</span>
            </div>
            <p>{t.creditos.artAttribution}</p>
            <a href="/art/LICENSES.md" target="_blank" rel="noreferrer">
              {t.creditos.licenseFiles} →
            </a>
          </article>
          <article className="img-credit">
            <div className="img-credit__head">
              <strong>{t.creditos.iconLicense}</strong>
              <span className="biblio-tag">CC BY 3.0</span>
            </div>
            <p>{t.creditos.iconsAttribution}</p>
            <a href="/icons/LICENSES.md" target="_blank" rel="noreferrer">
              {t.creditos.licenseFiles} →
            </a>
          </article>
        </div>
      </section>

      {note !== null && (
        <textarea className="img-credits__export" aria-label={t.creditos.exportLabel}
          readOnly value={note} />
      )}
      <p className="biblio-inspector__note">{t.creditos.legal}</p>
    </div>
  )
}
