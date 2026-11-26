import { useCallback, useRef, useState } from 'react'
import type { PointerEvent as ReactPointerEvent, ReactNode } from 'react'
import { useWindows } from './WindowContext'
import type { WindowState } from './layout'
import './windows.css'

/**
 * Ventana flotante del sistema (ADR-033): cabecera arrastrable,
 * redimensionado por bordes/esquina, minimizar/cerrar y foco con z-order.
 * Las etiquetas de las acciones llegan por props (ADR-038).
 */

export interface AppWindowProps {
  window: WindowState
  title: string
  /** Icono opcional de la herramienta (PMB-8.6b). */
  icon?: ReactNode
  children: ReactNode
  labels: { minimize: string; close: string }
}

type DragMode = 'move' | 'resize' | null

export function AppWindow({ window: win, title, icon, children, labels }: AppWindowProps) {
  const { focus, move, resize, minimize, close } = useWindows()
  const mode = useRef<DragMode>(null)
  const origin = useRef({ px: 0, py: 0, x: 0, y: 0, w: 0, h: 0 })
  const [dragging, setDragging] = useState(false)

  const begin = useCallback(
    (event: ReactPointerEvent, kind: Exclude<DragMode, null>) => {
      if (event.button !== 0) {
        return
      }
      focus(win.id)
      mode.current = kind
      setDragging(true)
      origin.current = {
        px: event.clientX,
        py: event.clientY,
        x: win.x,
        y: win.y,
        w: win.width,
        h: win.height,
      }
      event.currentTarget.setPointerCapture?.(event.pointerId)
      event.preventDefault()
    },
    [focus, win],
  )

  const drag = useCallback(
    (event: ReactPointerEvent) => {
      if (!mode.current) {
        return
      }
      const dx = event.clientX - origin.current.px
      const dy = event.clientY - origin.current.py
      if (mode.current === 'move') {
        move(win.id, origin.current.x + dx, origin.current.y + dy)
      } else {
        resize(win.id, origin.current.w + dx, origin.current.h + dy)
      }
    },
    [move, resize, win.id],
  )

  const end = useCallback((event: ReactPointerEvent) => {
    if (mode.current) {
      mode.current = null
      setDragging(false)
      event.currentTarget.releasePointerCapture?.(event.pointerId)
    }
  }, [])

  // Minimizada: la ventana vive en la bandeja hasta que `focus` la restaura.
  if (win.minimized) {
    return null
  }

  return (
    <section
      className={`pv-window${dragging ? ' pv-window--dragging' : ''}`}
      style={{ left: win.x, top: win.y, width: win.width, height: win.height, zIndex: win.z }}
      onPointerDown={() => focus(win.id)}
      role="dialog"
      aria-label={title}
    >
      <header
        className="pv-window__header"
        onPointerDown={(event) => begin(event, 'move')}
        onPointerMove={drag}
        onPointerUp={end}
        onPointerCancel={end}
      >
        <span className="pv-window__title">
          {icon ? <span className="pv-window__icon" aria-hidden="true">{icon}</span> : null}
          {title}
        </span>
        <span className="pv-window__actions">
          <button
            type="button"
            className="pv-window__button"
            aria-label={labels.minimize}
            title={labels.minimize}
            onPointerDown={(event) => event.stopPropagation()}
            onClick={() => minimize(win.id, true)}
          >
            –
          </button>
          <button
            type="button"
            className="pv-window__button"
            aria-label={labels.close}
            title={labels.close}
            onPointerDown={(event) => event.stopPropagation()}
            onClick={() => close(win.id)}
          >
            ✕
          </button>
        </span>
      </header>
      <div className="pv-window__body">{children}</div>
      <div
        className="pv-window__resize"
        onPointerDown={(event) => begin(event, 'resize')}
        onPointerMove={drag}
        onPointerUp={end}
        onPointerCancel={end}
        aria-hidden="true"
      />
    </section>
  )
}
