import { useCallback, useEffect, useState, useSyncExternalStore } from 'react'
import { apiFetchBlob, getApiToken } from './client'

/**
 * Portadas de campaña (PMB-2.4): la imagen se descarga autenticada y se
 * expone como objectURL. Caché por campaña; sin portada → null (la UI
 * muestra la imagen alternativa con la inicial).
 *
 * La caché tiene una revisión por campaña: al subir/borrar se incrementa y
 * los hooks suscritos vuelven a leer la imagen. Sin esto, reemplazar una
 * portada (hasCover sigue en true) dejaba ver la imagen antigua.
 */

const cache = new Map<string, string>()
const revisions = new Map<string, number>()
const listeners = new Map<string, Set<() => void>>()

function revisionOf(campaignId: string): number {
  return revisions.get(campaignId) ?? 0
}

function subscribe(campaignId: string, listener: () => void): () => void {
  let set = listeners.get(campaignId)
  if (!set) {
    set = new Set()
    listeners.set(campaignId, set)
  }
  set.add(listener)
  return () => { set?.delete(listener) }
}

export function clearCoverCache(): void {
  for (const url of cache.values()) {
    URL.revokeObjectURL(url)
  }
  cache.clear()
}

/** Hook: URL de portada de la campaña (null si no tiene o aún carga). */
export function useCampaignCover(campaignId: string, hasCover: boolean): string | null {
  const subscribeToCampaign = useCallback(
    (notify: () => void) => subscribe(campaignId, notify), [campaignId])
  const getRevision = useCallback(() => revisionOf(campaignId), [campaignId])
  const revision = useSyncExternalStore(subscribeToCampaign, getRevision)
  const [url, setUrl] = useState<string | null>(() => cache.get(campaignId) ?? null)

  useEffect(() => {
    if (!hasCover) {
      setUrl(null)
      return
    }
    const cached = cache.get(campaignId)
    if (cached) {
      setUrl(cached)
      return
    }
    let cancelled = false
    void apiFetchBlob(`/api/v1/campaigns/${campaignId}/cover`)
      .then((blob) => {
        const objectUrl = URL.createObjectURL(blob)
        cache.set(campaignId, objectUrl)
        if (!cancelled) {
          setUrl(objectUrl)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setUrl(null)
        }
      })
    return () => {
      cancelled = true
    }
  }, [campaignId, hasCover, revision])

  return url
}

/** Invalida la caché tras subir/borrar una portada y avisa a sus lectores. */
export function invalidateCover(campaignId: string): void {
  const url = cache.get(campaignId)
  if (url) {
    URL.revokeObjectURL(url)
    cache.delete(campaignId)
  }
  revisions.set(campaignId, revisionOf(campaignId) + 1)
  listeners.get(campaignId)?.forEach((notify) => notify())
}

/** Sube o reemplaza la portada (multipart con validación en servidor). */
export async function uploadCover(campaignId: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file, file.name)
  const response = await fetch(`/api/v1/campaigns/${campaignId}/cover`, {
    method: 'PUT',
    headers: getApiToken() ? { Authorization: `Bearer ${getApiToken()}` } : undefined,
    body: form,
  })
  if (!response.ok) {
    throw new Error('No se pudo subir la portada.')
  }
  invalidateCover(campaignId)
}

/** Elimina la portada (idempotente). Un error del servidor se propaga. */
export async function deleteCover(campaignId: string): Promise<void> {
  const response = await fetch(`/api/v1/campaigns/${campaignId}/cover`, {
    method: 'DELETE',
    headers: getApiToken() ? { Authorization: `Bearer ${getApiToken()}` } : undefined,
  })
  if (!response.ok) {
    throw new Error('No se pudo quitar la portada.')
  }
  invalidateCover(campaignId)
}
