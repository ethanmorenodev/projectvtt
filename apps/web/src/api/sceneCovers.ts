import { useEffect, useState } from 'react'
import { apiFetchBlob, getApiToken } from './client'

/** Portadas de escena (blob autenticado en caché por escena). */

const cache = new Map<string, string>()

export function clearSceneCoverCache(): void {
  for (const url of cache.values()) URL.revokeObjectURL(url)
  cache.clear()
}

export function invalidateSceneCover(sceneId: string): void {
  const url = cache.get(sceneId)
  if (url) {
    URL.revokeObjectURL(url)
    cache.delete(sceneId)
  }
}

export async function uploadSceneCover(sceneId: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  const response = await fetch(`/api/v1/scenes/${sceneId}/cover`, {
    method: 'PUT',
    headers: getApiToken() ? { Authorization: `Bearer ${getApiToken()}` } : undefined,
    body: form,
  })
  if (!response.ok) throw new Error('No se pudo subir la portada de la escena.')
  invalidateSceneCover(sceneId)
}

export function useSceneCover(sceneId: string, hasCover: boolean): string | null {
  const [url, setUrl] = useState<string | null>(() => cache.get(sceneId) ?? null)

  useEffect(() => {
    if (!hasCover) {
      setUrl(null)
      return
    }
    const cached = cache.get(sceneId)
    if (cached) {
      setUrl(cached)
      return
    }
    let cancelled = false
    void apiFetchBlob(`/api/v1/scenes/${sceneId}/cover`)
      .then((blob) => {
        const objectUrl = URL.createObjectURL(blob)
        cache.set(sceneId, objectUrl)
        if (!cancelled) setUrl(objectUrl)
      })
      .catch(() => { if (!cancelled) setUrl(null) })
    return () => { cancelled = true }
  }, [sceneId, hasCover])

  return url
}
