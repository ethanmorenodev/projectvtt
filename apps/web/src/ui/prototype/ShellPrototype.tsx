import { useState } from 'react'
import { AppWindow, WindowProvider, useWindows } from '../../windows'
import type { ToolDescriptor } from '../../windows'
import {
  BibliotecaArea,
  CampanaArea,
  CampanasArea,
  GestionArea,
  InicioArea,
  MesaArea,
} from './Areas'
import './prototype.css'

/**
 * Prototipo del shell de aplicación (PMB-1.3) — solo desarrollo
 * (`#prototipo-shell`). Composición de navegación por áreas para revisión
 * visual; datos estáticos, sin lógica de negocio. **No integrar en la app
 * hasta la aprobación del propietario** (proceso visual obligatorio).
 */

type AreaId = 'inicio' | 'campanas' | 'campana' | 'mesa' | 'biblioteca' | 'gestion'

const AREAS: Array<{ id: AreaId; label: string; icon: string }> = [
  { id: 'inicio', label: 'Inicio', icon: '◈' },
  { id: 'campanas', label: 'Campañas', icon: '▤' },
  { id: 'campana', label: 'Espacio de campaña', icon: '▣' },
  { id: 'mesa', label: 'Mesa de juego', icon: '⬡' },
  { id: 'biblioteca', label: 'Biblioteca', icon: '▦' },
  { id: 'gestion', label: 'Gestión', icon: '⚙' },
]

const demoTools: ToolDescriptor[] = [
  { id: 'chat', title: 'Chat', icon: '💬', singleton: true, defaultWidth: 320, defaultHeight: 260, content: () => <ToolBody lines={['Ana: ¿Entramos en combate?', 'Luis: Saco la espada.', 'Ana: Iniciativa: 18.']} /> },
  { id: 'ficha', title: 'Ficha — Elenar', icon: '🧝', singleton: true, defaultWidth: 380, defaultHeight: 320, content: () => <ToolBody lines={['Fuerza 12 · Destreza 16 · Constitución 14', 'PV 27/31 · CA 15', 'Efectos: bendecido (+2)']} /> },
  { id: 'inspector', title: 'Inspector', icon: '⌕', singleton: true, defaultWidth: 300, defaultHeight: 280, content: () => <ToolBody lines={['Elemento: Guardia del puerto', 'Posición: 12, 8 (grid hex)', 'Versión 4 · movido por Ana']} /> },
]

function ToolBody({ lines }: { lines: string[] }) {
  return (
    <ul className="proto-tool-body">
      {lines.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  )
}

function MesaToolbar() {
  const { open, windows } = useWindows()
  return (
    <div className="proto-mesa__toolbar">
      {demoTools.map((tool) => (
        <button key={tool.id} type="button" className="pv-btn pv-btn--secondary" onClick={() => open(tool.id)}>
          {tool.icon} {tool.title}
        </button>
      ))}
      <span className="proto-mesa__status">{windows.filter((w) => !w.minimized).length} ventana(s) abierta(s)</span>
    </div>
  )
}

function MesaTray() {
  const { windows, focus } = useWindows()
  const minimized = windows.filter((w) => w.minimized)
  if (minimized.length === 0) {
    return null
  }
  return (
    <div className="pv-window-tray">
      {minimized.map((win) => (
        <button key={win.id} type="button" className="pv-window-tray__item" onClick={() => focus(win.id)}>
          {demoTools.find((tool) => tool.id === win.toolId)?.title ?? win.toolId}
        </button>
      ))}
    </div>
  )
}

function MesaWindows() {
  const { windows } = useWindows()
  return (
    <div className="pv-window-host proto-mesa__host">
      {windows.map((win) => (
        <AppWindow
          key={win.id}
          window={win}
          title={demoTools.find((tool) => tool.id === win.toolId)?.title ?? win.toolId}
          labels={{ minimize: 'Minimizar ventana', close: 'Cerrar ventana' }}
        >
          {demoTools.find((tool) => tool.id === win.toolId)?.content(win.params)}
        </AppWindow>
      ))}
      <MesaTray />
    </div>
  )
}

export function ShellPrototype() {
  const [area, setArea] = useState<AreaId>('inicio')

  return (
    <div className="proto-shell">
      <aside className="proto-rail">
        <div className="proto-rail__brand">ProjectVTT</div>
        {AREAS.map((entry) => (
          <button
            key={entry.id}
            type="button"
            className={`proto-rail__item${area === entry.id ? ' proto-rail__item--active' : ''}`}
            onClick={() => setArea(entry.id)}
          >
            <span aria-hidden="true">{entry.icon}</span>
            {entry.label}
          </button>
        ))}
        <div className="proto-rail__footer">Ana (GM) · Configuración</div>
      </aside>

      <div className="proto-main">
        <header className="proto-topbar">
          <input className="pv-input proto-search" placeholder="Buscar (Ctrl+K): campañas, escenas, personajes, contenido…" readOnly />
          <span className="proto-topbar__hint">⌘K</span>
        </header>

        <div className="proto-content">
          {area === 'inicio' && <InicioArea />}
          {area === 'campanas' && <CampanasArea />}
          {area === 'campana' && <CampanaArea />}
          {area === 'mesa' && (
            <MesaArea>
              <WindowProvider context="table" tools={demoTools}>
                <MesaToolbar />
                <MesaWindows />
              </WindowProvider>
            </MesaArea>
          )}
          {area === 'biblioteca' && <BibliotecaArea />}
          {area === 'gestion' && <GestionArea />}
        </div>
      </div>
    </div>
  )
}
