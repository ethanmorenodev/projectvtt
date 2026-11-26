import { Button } from './Button'
import { Field, Input } from './Field'
import './ui.css'

/**
 * Modal de confirmación (COMPONENT_RULES §9): solo para decisiones que
 * bloquean o acciones destructivas. Las irreversibles pueden exigir
 * escribir una frase de confirmación (`confirmPhrase`). Textos por props.
 */

export interface ConfirmDialogProps {
  open: boolean
  title: string
  text?: string
  confirmLabel: string
  cancelLabel: string
  /** Peligro: el botón de confirmación usa fondo danger (solo aquí). */
  destructive?: boolean
  /** Si se indica, hay que teclear esta frase para confirmar (irreversible). */
  confirmPhrase?: string
  phraseLabel?: string
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({
  open,
  title,
  text,
  confirmLabel,
  cancelLabel,
  destructive,
  confirmPhrase,
  phraseLabel,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  if (!open) {
    return null
  }

  return (
    <div className="pv-confirm__overlay" onClick={onCancel}>
      <div
        className="pv-confirm"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
      >
        <h2 className="pv-confirm__title">{title}</h2>
        {text ? <p className="pv-confirm__text">{text}</p> : null}
        {confirmPhrase ? (
          <Field label={phraseLabel ?? confirmPhrase}>
            {({ id, ...aria }) => (
              <Input
                id={id}
                autoFocus
                placeholder={confirmPhrase}
                autoComplete="off"
                {...aria}
              />
            )}
          </Field>
        ) : null}
        <div className="pv-confirm__actions">
          <Button variant="ghost" onClick={onCancel}>
            {cancelLabel}
          </Button>
          <Button variant={destructive ? 'danger-solid' : 'primary'} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  )
}
