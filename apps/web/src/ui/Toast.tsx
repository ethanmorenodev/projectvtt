import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { Button } from './Button'
import './ui.css'

/**
 * Toasts (COMPONENT_RULES §9): abajo-derecha, máximo 3 apilados, 4 s salvo
 * errores (persistentes). `aria-live` para lectores de pantalla. Sin
 * textos embebidos: cada toast recibe sus cadenas (ADR-038).
 */

export type ToastTone = 'info' | 'success' | 'warning' | 'error'

export interface ToastInput {
  title: string
  text?: string
  tone?: ToastTone
  /** ms hasta auto-cierre; 0 = persistente. Por defecto 4000 (0 en errores). */
  duration?: number
}

interface ToastItem extends ToastInput {
  id: number
}

interface ToastApi {
  show: (toast: ToastInput) => void
  dismiss: (id: number) => void
}

const ToastContext = createContext<ToastApi | null>(null)

export function useToast(): ToastApi {
  const api = useContext(ToastContext)
  if (!api) {
    throw new Error('useToast requiere un ToastProvider')
  }
  return api
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(1)

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const show = useCallback(
    (input: ToastInput) => {
      const id = nextId.current++
      const duration = input.duration ?? (input.tone === 'error' ? 0 : 4000)
      setToasts((current) => [...current, { ...input, id }].slice(-3))
      if (duration > 0) {
        window.setTimeout(() => dismiss(id), duration)
      }
    },
    [dismiss],
  )

  const api = useMemo(() => ({ show, dismiss }), [show, dismiss])

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="pv-toasts" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`pv-toast pv-toast--${toast.tone ?? 'info'}`}>
            <div className="pv-toast__body">
              <div className="pv-toast__title">{toast.title}</div>
              {toast.text ? <div className="pv-toast__text">{toast.text}</div> : null}
            </div>
            <Button variant="ghost" aria-label="Cerrar aviso" onClick={() => dismiss(toast.id)}>
              ✕
            </Button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
