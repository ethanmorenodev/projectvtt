import type { ReactNode } from 'react'
import './ui.css'

/**
 * Lista densa (COMPONENT_RULES §4): **fila fija de dos líneas** que nunca
 * crece por envoltura (decisión del propietario 2026-11-09). Línea 1:
 * nombre (elipsis + tooltip) y badge de estado. Línea 2: subtítulo
 * (contexto, trunca primero) y metadatos en una sola línea que no
 * envuelve, con la columna temporal alineada a la derecha. Orden de
 * sacrificio documentado en COMPONENT_RULES §4.
 */

export function List({ children }: { children: ReactNode }) {
  return <ul className="pv-list">{children}</ul>
}

export interface ListRowProps {
  title: string
  /** Estado/etiqueta de la línea 1 (junto al nombre). */
  badge?: ReactNode
  /** Contexto de la línea 2 (descripción); trunca primero. */
  subtitle?: ReactNode
  /** Datos de la línea 2 (derecha): una sola línea, nunca envuelve. */
  meta?: ReactNode
  /** Miniatura cuadrada (thumb-xs 48 px) para reconocer el elemento de un vistazo. */
  thumb?: ReactNode
  selected?: boolean
  actions?: ReactNode
  onClick?: () => void
}

export function ListRow({
  title,
  badge,
  subtitle,
  meta,
  thumb,
  selected,
  actions,
  onClick,
}: ListRowProps) {
  return (
    <li>
      <div
        className={['pv-row', selected ? 'pv-row--selected' : null].filter(Boolean).join(' ')}
        role={onClick ? 'button' : undefined}
        tabIndex={onClick ? 0 : undefined}
        aria-current={selected || undefined}
        onClick={onClick}
        onKeyDown={(event) => {
          if (onClick && (event.key === 'Enter' || event.key === ' ')) {
            event.preventDefault()
            onClick()
          }
        }}
      >
        {thumb ? <div className="pv-row__thumb">{thumb}</div> : null}
        <div className="pv-row__main">
          <div className="pv-row__line">
            <div className="pv-row__ident">
              <span className="pv-row__title" title={title}>
                {title}
              </span>
              {badge}
            </div>
            {/* Sin subtítulo: los metadatos acompañan al título (una fila
                de una sola línea visual, p. ej. actividad con tiempo). */}
            {!subtitle && meta ? <div className="pv-row__meta">{meta}</div> : null}
          </div>
          {subtitle ? (
            <div className="pv-row__line">
              <span className="pv-row__subtitle">{subtitle}</span>
              {meta ? <div className="pv-row__meta">{meta}</div> : null}
            </div>
          ) : null}
        </div>
        {actions ? (
          // Las acciones no deben disparar el onClick de la fila.
          <div className="pv-row__actions" onClick={(event) => event.stopPropagation()}>
            {actions}
          </div>
        ) : null}
      </div>
    </li>
  )
}
