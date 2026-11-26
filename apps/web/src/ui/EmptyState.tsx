import type { ReactNode } from 'react'
import './ui.css'

/**
 * Estado vacío con guía (COMPONENT_RULES §9). Dos formatos:
 * - compacto: icono + título + frase + acción;
 * - **hero** (maqueta objetivo 2026-11-10): arte grande + título
 *   destacado + descripción centrada, para áreas enteras.
 */

export interface EmptyStateProps {
  /** Arte grande (variante hero): ilustración del área. */
  art?: ReactNode
  icon?: ReactNode
  title: string
  text?: string
  action?: ReactNode
}

export function EmptyState({ art, icon, title, text, action }: EmptyStateProps) {
  if (art) {
    return (
      <div className="pv-empty pv-empty--hero">
        <div className="pv-empty__art">{art}</div>
        <span className="pv-empty__hero-title">{title}</span>
        {text ? <span className="pv-empty__hero-text">{text}</span> : null}
        {action}
      </div>
    )
  }

  return (
    <div className="pv-empty">
      {icon ? <div className="pv-empty__icon">{icon}</div> : null}
      <span className="pv-empty__title">{title}</span>
      {text ? <span className="pv-empty__text">{text}</span> : null}
      {action}
    </div>
  )
}