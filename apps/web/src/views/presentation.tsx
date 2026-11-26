import { useCampaignCover } from '../api/covers'
import { useSceneCover } from '../api/sceneCovers'
import './presentation.css'

/**
 * Piezas visuales compartidas por las pantallas integradas (PMB-2.2/2.4):
 * miniatura con portada o imagen alternativa (inicial, protagonismo
 * reducido — decisión 4) y tiempo relativo normalizado «hace X»
 * (decisión 5).
 */

export function relativeTime(iso: string | null | undefined): string {
  if (!iso) {
    return ''
  }
  const diff = Date.now() - new Date(iso).getTime()
  if (Number.isNaN(diff)) {
    return ''
  }
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1) {
    return 'hace un momento'
  }
  if (minutes < 60) {
    return `hace ${minutes} min`
  }
  const hours = Math.floor(minutes / 60)
  if (hours < 24) {
    return `hace ${hours} h`
  }
  const days = Math.floor(hours / 24)
  if (days < 7) {
    return `hace ${days} día${days > 1 ? 's' : ''}`
  }
  const weeks = Math.floor(days / 7)
  if (weeks < 5) {
    return `hace ${weeks} semana${weeks > 1 ? 's' : ''}`
  }
  const months = Math.floor(days / 30)
  if (months < 12) {
    return `hace ${months} mes${months > 1 ? 'es' : ''}`
  }
  const years = Math.floor(days / 365)
  return `hace ${years} año${years > 1 ? 's' : ''}`
}

/** Pluralización simple para los contadores de la UI («1 participante»). */
export function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`
}

export interface CampaignThumbProps {
  name: string
  campaignId: string
  hasCover: boolean
  /** 48 (listas) o 64 (destacadas) — regla de miniaturas aprobada. */
  size?: 48 | 64
}

export interface SceneThumbProps {
  name: string
  sceneId: string
  hasCover: boolean
  size?: 48 | 64
}

/** Miniatura de escena: portada subida o inicial, como `CampaignThumb`. */
export function SceneThumb({ name, sceneId, hasCover, size = 48 }: SceneThumbProps) {
  const coverUrl = useSceneCover(sceneId, hasCover)
  return coverUrl ? (
    <div className="pv-cover" style={{ width: size, height: size }}>
      <img src={coverUrl} alt="" />
    </div>
  ) : (
    <div className="pv-cover pv-cover--fallback" style={{ width: size, height: size }} aria-hidden="true">
      {name.trim().charAt(0).toUpperCase()}
    </div>
  )
}

export function CampaignThumb({ name, campaignId, hasCover, size = 48 }: CampaignThumbProps) {
  // La portada se descarga autenticada (caché por campaña).
  const coverUrl = useCampaignCover(campaignId, hasCover)
  return coverUrl ? (
    <div className="pv-cover" style={{ width: size, height: size }}>
      <img src={coverUrl} alt="" />
    </div>
  ) : (
    <div className="pv-cover pv-cover--fallback" style={{ width: size, height: size }} aria-hidden="true">
      {name.trim().charAt(0).toUpperCase()}
    </div>
  )
}
