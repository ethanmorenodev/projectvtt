import { useEffect, useRef, useState } from 'react'
import { t } from '../i18n'
import { Button } from './Button'
import './OnboardingTour.css'

/** Bandera de primer ingreso: se escribe al terminar o al omitir el recorrido. */
export const TOUR_STORAGE_KEY = 'pv-tour-done'

function isDone(): boolean {
  try {
    return window.localStorage.getItem(TOUR_STORAGE_KEY) === '1'
  } catch {
    // Almacenamiento bloqueado (modo privado, política del navegador):
    // el recorrido se muestra igual, pero no podrá recordarse.
    return false
  }
}

function markDone(): void {
  try {
    window.localStorage.setItem(TOUR_STORAGE_KEY, '1')
  } catch {
    // Sin persistencia la app sigue funcionando: solo se cierra el recorrido.
  }
}

/**
 * Recorrido guiado de primer ingreso (UI nueva, prototipo aprobado).
 * Se monta dentro del shell autenticado: nunca cubre el login. El overlay
 * es temporal; «Empezar» y «Omitir» lo cierran y marcan la bandera.
 */
export function OnboardingTour() {
  const [open, setOpen] = useState(() => !isDone())
  const [step, setStep] = useState(0)
  const dialogRef = useRef<HTMLDivElement>(null)
  const steps = t.tour.steps
  const current = steps[step]
  const isLast = step === steps.length - 1

  useEffect(() => {
    if (open) {
      dialogRef.current?.focus()
    }
  }, [open, step])

  if (!open) {
    return null
  }

  function close() {
    markDone()
    setOpen(false)
  }

  return (
    <div className="pv-tour__overlay">
      <div
        ref={dialogRef}
        className="pv-tour"
        role="dialog"
        aria-modal="true"
        aria-label={t.tour.dialogLabel}
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="pv-tour__head">
          <span className="pv-tour__eyebrow">{t.tour.eyebrow} · {current.area}</span>
          <h2 className="pv-tour__title">{current.title}</h2>
        </header>
        <p className="pv-tour__text">{current.text}</p>
        <footer className="pv-tour__foot">
          <span className="pv-tour__progress" aria-live="polite">
            {step + 1} / {steps.length}
          </span>
          <div className="pv-tour__actions">
            <Button variant="ghost" onClick={close}>{t.tour.skip}</Button>
            <Button variant="secondary" disabled={step === 0} onClick={() => setStep((value) => value - 1)}>
              {t.tour.previous}
            </Button>
            {isLast ? (
              <Button variant="primary" onClick={close}>{t.tour.start}</Button>
            ) : (
              <Button variant="primary" onClick={() => setStep((value) => value + 1)}>
                {t.tour.next}
              </Button>
            )}
          </div>
        </footer>
      </div>
    </div>
  )
}
