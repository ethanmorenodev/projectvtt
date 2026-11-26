import type { ReactNode } from 'react'
import './ui.css'

/**
 * Badge de estado/etiqueta (promovido de los prototipos PMB-2.1/2.3,
 * decisión del propietario 2026-11-09). Variantes semánticas; nunca
 * compite con el texto principal. Reglas: COMPONENT_RULES §14.
 */

export type BadgeTone = 'accent' | 'success' | 'warning' | 'danger' | 'neutral'

export interface BadgeProps {
  tone?: BadgeTone
  children: ReactNode
}

export function Badge({ tone = 'neutral', children }: BadgeProps) {
  return <span className={`pv-badge pv-badge--${tone}`}>{children}</span>
}
