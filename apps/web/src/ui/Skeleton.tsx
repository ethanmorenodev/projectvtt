import './ui.css'

/**
 * Bloque de carga con la forma del contenido real (COMPONENT_RULES §9).
 * Respeta `prefers-reduced-motion` (la animación se anula en tokens.css).
 */

export interface SkeletonProps {
  width?: number | string
  height?: number | string
  radius?: number | string
}

export function Skeleton({ width = '100%', height = 16, radius }: SkeletonProps) {
  return (
    <div
      className="pv-skeleton"
      style={{ width, height, borderRadius: radius }}
      aria-hidden="true"
    />
  )
}
