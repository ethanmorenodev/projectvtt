import type { ReactNode } from 'react'
import './ui.css'

/**
 * Diálogo modal de gestión (COMPONENT_RULES §9: modales solo para
 * decisiones que bloquean o superficies de gestión breve). Cabecera con
 * título y cierre, cuerpo con secciones y pie de acciones. Textos por
 * props (ADR-038).
 */

export interface DialogProps {
  open: boolean
  title: string
  /** Etiqueta accesible del botón de cierre. */
  closeLabel: string
  onClose: () => void
  actions?: ReactNode
  children: ReactNode
}

export function Dialog({ open, title, closeLabel, onClose, actions, children }: DialogProps) {
  if (!open) {
    return null
  }

  return (
    <div className="pv-confirm__overlay" onClick={onClose}>
      <div
        className="pv-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="pv-dialog__head">
          <h2 className="pv-confirm__title">{title}</h2>
          <button
            type="button"
            className="pv-btn pv-btn--ghost pv-btn--icon"
            aria-label={closeLabel}
            title={closeLabel}
            onClick={onClose}
          >
            ✕
          </button>
        </header>
        <div className="pv-dialog__body">{children}</div>
        {actions ? <div className="pv-confirm__actions">{actions}</div> : null}
      </div>
    </div>
  )
}
