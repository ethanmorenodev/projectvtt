/**
 * Paquetes de contenido `.pvttpkg` (EXP-43): consumo web de los endpoints
 * ya existentes en el backend. El servidor es la autoridad: valida el zip,
 * aplica la estrategia de conflictos y devuelve el informe; la UI solo
 * elige la estrategia y muestra lo que llega.
 *
 * Bearer: `apiFetch`/`apiFetchBlob` usan el token fijado por AuthContext
 * (el mismo que expone `getApiToken`), igual que el patrón de `covers.ts`.
 */

import { apiFetch, apiFetchBlob } from './client'
import type { ContentPackDto } from './types'

/** Estrategias aceptadas por el backend (case-insensitive; se envían tal cual). */
export type ImportStrategy = 'Keep' | 'Update' | 'Copy'

/** `PackageImportReportDto` del backend (ADR-036). */
export interface PackageImportReport {
  packKey: string
  strategy: string
  types: number
  created: number
  updated: number
  skipped: number
  conflicts: string[]
}

export async function listPacks(): Promise<ContentPackDto[]> {
  return apiFetch<ContentPackDto[]>('/api/v1/content/packs')
}

/** Descarga el zip generado por el servidor como `<key>.pvttpkg`. */
export async function exportPack(key: string): Promise<void> {
  const blob = await apiFetchBlob(
    `/api/v1/content/packs/${encodeURIComponent(key)}/export`,
  )
  const objectUrl = URL.createObjectURL(blob)
  try {
    const link = document.createElement('a')
    link.href = objectUrl
    link.download = `${key}.pvttpkg`
    document.body.appendChild(link)
    link.click()
    link.remove()
  } finally {
    URL.revokeObjectURL(objectUrl)
  }
}

/** Importa un paquete. Los errores del servidor (p. ej. 403 de operador) se propagan. */
export async function importPack(
  file: File,
  strategy: ImportStrategy,
): Promise<PackageImportReport> {
  const form = new FormData()
  form.append('file', file, file.name)
  form.append('strategy', strategy)
  return apiFetch<PackageImportReport>('/api/v1/content/packs/import', {
    method: 'POST',
    body: form,
  })
}
