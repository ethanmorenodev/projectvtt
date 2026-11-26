import { useEffect, useRef } from 'react'
import { AppWindow, useWindows } from '../windows'
import type { ToolDescriptor } from '../windows'

/**
 * Integración de las herramientas del MVP en el sistema de ventanas
 * (PMB-1.6): host de ventanas + lanzador en el HUD. Los paneles
 * (Chat/Combate/Ficha/Notas/Historial) conservan su lógica intacta; solo
 * cambia su contenedor. Sin textos embebidos (ADR-038).
 */

export interface ToolsHostProps {
  tools: ToolDescriptor[]
  /** Si se abre todo al entrar cuando no hay layout guardado. */
  openByDefault?: boolean
  labels: { minimize: string; close: string }
}

/** Ventanas de herramientas + bandeja de minimizadas sobre el tablero. */
export function ToolsHost({ tools, openByDefault, labels }: ToolsHostProps) {
  const { windows, open, focus } = useWindows()
  const initialized = useRef(false)

  // Primera visita sin layout guardado: las herramientas se abren
  // dispuestas; si hay layout persistido, se respeta tal cual.
  useEffect(() => {
    if (initialized.current) {
      return
    }
    initialized.current = true
    if (openByDefault && windows.length === 0) {
      tools.forEach((tool) => open(tool.id))
    }
    // Solo al montar: el layout del usuario manda después.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const minimized = windows.filter((win) => win.minimized)

  return (
    <div className="pv-window-host tools-host">
      {windows.map((win) => (
        <AppWindow
          key={win.id}
          window={win}
          title={tools.find((tool) => tool.id === win.toolId)?.title ?? win.toolId}
          icon={tools.find((tool) => tool.id === win.toolId)?.icon}
          labels={labels}
        >
          {tools.find((tool) => tool.id === win.toolId)?.content(win.params)}
        </AppWindow>
      ))}
      {minimized.length > 0 ? (
        <div className="pv-window-tray">
          {minimized.map((win) => (
            <button
              key={win.id}
              type="button"
              className="pv-window-tray__item"
              onClick={() => focus(win.id)}
            >
              {tools.find((tool) => tool.id === win.toolId)?.title ?? win.toolId}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}

export interface ToolLauncherProps {
  tools: ToolDescriptor[]
  /** Etiqueta del grupo de botones. */
  title: string
}

/** Botones de apertura de herramientas en el HUD. */
export function ToolLauncher({ tools, title }: ToolLauncherProps) {
  const { windows, open, focus } = useWindows()

  return (
    <div className="hud-row tool-launcher" role="group" aria-label={title}>
      {tools.map((tool) => {
        const instance = windows.find((win) => win.toolId === tool.id)
        return (
          <button
            key={tool.id}
            type="button"
            className={instance ? 'link tool-launcher__active' : 'link'}
            aria-label={tool.title}
            title={tool.title}
            aria-pressed={Boolean(instance)}
            onClick={() => (instance ? focus(instance.id) : open(tool.id))}
          >
            {tool.icon}
            <span className="tool-launcher__label">{tool.title}</span>
          </button>
        )
      })}
    </div>
  )
}
