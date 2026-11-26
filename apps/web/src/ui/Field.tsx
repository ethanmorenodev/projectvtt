import { useId } from 'react'
import type { InputHTMLAttributes, ReactNode } from 'react'
import './ui.css'

/**
 * Campo de formulario (COMPONENT_RULES §3): etiqueta encima, ayuda debajo
 * y error debajo con el primer error enfocado al enviar (lo gestiona el
 * formulario). Todos los textos por props (ADR-038).
 */

export interface FieldProps {
  label: string
  helper?: string
  error?: string
  children: (props: { id: string; 'aria-invalid': boolean; 'aria-describedby': string | undefined }) => ReactNode
}

export function Field({ label, helper, error, children }: FieldProps) {
  const id = useId()
  const describedBy = error ? `${id}-error` : helper ? `${id}-helper` : undefined
  return (
    <div className="pv-field">
      <label className="pv-field__label" htmlFor={id}>
        {label}
      </label>
      {children({ id, 'aria-invalid': Boolean(error), 'aria-describedby': describedBy })}
      {error ? (
        <span className="pv-field__error" id={`${id}-error`}>
          {error}
        </span>
      ) : helper ? (
        <span className="pv-field__helper" id={`${id}-helper`}>
          {helper}
        </span>
      ) : null}
    </div>
  )
}

export type InputProps = InputHTMLAttributes<HTMLInputElement>

export function Input(props: InputProps) {
  return <input className="pv-input" {...props} />
}
