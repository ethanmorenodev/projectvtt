import type { ReactNode } from 'react'
import './ui.css'

/**
 * Tarjeta compacta (COMPONENT_RULES §5): miniatura cuadrada + título de
 * una línea + metadatos + acciones. Nunca tarjetas gigantes.
 */

export interface CardProps {
  /** Miniatura cuadrada; si no hay imagen, un placeholder con icono. */
  thumb?: ReactNode
  title: string
  meta?: ReactNode
  actions?: ReactNode
  onClick?: () => void
}

export function Card({ thumb, title, meta, actions, onClick }: CardProps) {
  return (
    <div
      className="pv-card"
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      onKeyDown={(event) => {
        if (onClick && (event.key === 'Enter' || event.key === ' ')) {
          event.preventDefault()
          onClick()
        }
      }}
    >
      <div className="pv-card__thumb">{thumb}</div>
      <div className="pv-card__body">
        <span className="pv-card__title" title={title}>
          {title}
        </span>
        {meta ? <div className="pv-card__meta">{meta}</div> : null}
      </div>
      {actions ? <div className="pv-card__actions">{actions}</div> : null}
    </div>
  )
}
