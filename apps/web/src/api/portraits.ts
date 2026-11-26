import { useEffect, useState } from 'react'
import { apiFetchBlob, getApiToken } from './client'

/** Retratos de actor: blob autenticado por id de imagen (sin credenciales en URL). */

export function useImageUrl(imageId: string | null): string | null {
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    let objectUrl: string | null = null
    if (!imageId) {
      setUrl(null)
      return
    }
    void apiFetchBlob(`/api/v1/images/${imageId}/file`)
      .then((blob) => {
        if (!cancelled) {
          objectUrl = URL.createObjectURL(blob)
          setUrl(objectUrl)
        }
      })
      .catch(() => { if (!cancelled) setUrl(null) })
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [imageId])

  return url
}

export async function uploadImage(file: File): Promise<{ id: string }> {
  const form = new FormData()
  form.append('file', file)
  const response = await fetch('/api/v1/images', {
    method: 'POST',
    headers: getApiToken() ? { Authorization: `Bearer ${getApiToken()}` } : undefined,
    body: form,
  })
  if (!response.ok) throw new Error('No se pudo subir la imagen.')
  return (await response.json()) as { id: string }
}
