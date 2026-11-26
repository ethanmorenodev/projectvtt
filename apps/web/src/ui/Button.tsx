import type { ButtonHTMLAttributes, ReactNode } from 'react'
import './ui.css'

/**
 * Botón con jerarquía (COMPONENT_RULES §2): una acción primaria por
 * superficie; secundarias, fantasma y peligro para el resto. Sin textos
 * embebidos: las etiquetas llegan por props (ADR-038, i18n-ready).
 */

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'danger-solid'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  icon?: ReactNode
}

export function Button({ variant = 'secondary', icon, children, className, ...rest }: ButtonProps) {
  return (
    <button
      type="button"
      className={['pv-btn', `pv-btn--${variant}`, className].filter(Boolean).join(' ')}
      {...rest}
    >
      {icon}
      {children}
    </button>
  )
}

export interface IconButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  /** Etiqueta accesible y tooltip (obligatorio: icono solo nunca es ambiguo). */
  label: string
  icon: ReactNode
}

export function IconButton({ label, icon, className, ...rest }: IconButtonProps) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      className={['pv-btn', 'pv-btn--ghost', 'pv-btn--icon', className].filter(Boolean).join(' ')}
      {...rest}
    >
      {icon}
    </button>
  )
}
