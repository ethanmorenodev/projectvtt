import { useState } from 'react'
import type { ChangeEvent } from 'react'
import { apiFetch } from '../api/client'
import type { ActorDto } from '../api/types'
import { uploadImage, useImageUrl } from '../api/portraits'
import { t } from '../i18n'
import './actorPortrait.css'

/** Retrato de un personaje con subida real. El servidor valida el archivo y
 *  el PATCH usa el testigo de versión, así que no se duplica ninguna regla. */
export function ActorPortrait({ actor, editable, onUpdated, className = '' }: {
  actor: Pick<ActorDto, 'id' | 'name' | 'portraitId' | 'version'>
  editable: boolean
  onUpdated?: (actor: ActorDto) => void
  className?: string
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const url = useImageUrl(actor.portraitId)

  async function change(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    setBusy(true)
    setError(null)
    try {
      const image = await uploadImage(file)
      const updated = await apiFetch<ActorDto>(`/api/v1/actors/${actor.id}`, {
        method: 'PATCH',
        body: { version: actor.version, portraitId: image.id },
      })
      onUpdated?.(updated)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.portrait.error)
    } finally {
      setBusy(false)
    }
  }

  return (
    <span className={`actor-portrait ${className}`.trim()}>
      {url ? (
        <img className="actor-portrait__img" src={url} alt="" />
      ) : (
        <span className="actor-portrait__initial" aria-hidden="true">
          {actor.name.trim().charAt(0).toUpperCase()}
        </span>
      )}
      {editable ? (
        <label className="actor-portrait__change" title={t.portrait.change}>
          <span aria-hidden="true">{busy ? '…' : '✎'}</span>
          <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml"
            aria-label={`${t.portrait.change}: ${actor.name}`}
            onChange={(event) => void change(event)} />
        </label>
      ) : null}
      {error ? <span className="actor-portrait__error" role="alert">{error}</span> : null}
    </span>
  )
}
