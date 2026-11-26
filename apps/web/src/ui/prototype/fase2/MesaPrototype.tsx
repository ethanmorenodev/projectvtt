import { useState } from 'react'
import { Badge, Button, EmptyState, IconButton } from '../../index'
import { AppWindow, WindowProvider, useWindows } from '../../../windows'
import type { ToolDescriptor } from '../../../windows'
import { PrototypeShell } from './AreasFase2'
import type { Variant } from './AreasFase2'
import './fase2.css'
import './mesa.css'

/**
 * Prototipo de la Mesa de juego (PMB-2.8) — solo desarrollo, datos
 * estáticos. Distribución flexible del plan §7.1: tablero central a
 * sangre, raíl izquierdo (escenas/capas/herramientas/biblioteca),
 * inspector contextual derecho y barra inferior con herramientas
 * frecuentes; las herramientas vivas (chat/combate/ficha) usan el
 * sistema de ventanas real (PMB-1.5). Variantes: `normal` (con
 * selección), `vacio` (escena sin tokens) y `extremo` (muchos tokens y
 * nombres largos). **No integrar hasta aprobación explícita.**
 */

interface Token {
  name: string
  x: number
  y: number
  color: string
  actor?: string
}

const TOOL_ICONS = ['⬠', '✎', '📏', '🛡', '💬', '⚔']

const demoTools: ToolDescriptor[] = [
  {
    id: 'chat',
    title: 'Chat',
    singleton: true,
    defaultWidth: 320,
    defaultHeight: 260,
    content: () => (
      <ul className="proto-tool-body">
        <li>Ana: ¿Entramos en combate?</li>
        <li>Luis: Saco la espada.</li>
        <li>Ana: Iniciativa: 18.</li>
      </ul>
    ),
  },
  {
    id: 'combate',
    title: 'Combate',
    singleton: true,
    defaultWidth: 320,
    defaultHeight: 300,
    content: () => (
      <ul className="proto-tool-body">
        <li>Ronda 2 — turno de Elenar</li>
        <li>1. Elenar (18)</li>
        <li>2. Guardia del puerto (12)</li>
        <li>3. Brom (7)</li>
      </ul>
    ),
  },
  {
    id: 'ficha',
    title: 'Ficha',
    singleton: true,
    defaultWidth: 340,
    defaultHeight: 340,
    content: () => (
      <ul className="proto-tool-body">
        <li>Elenar — nivel 3</li>
        <li>Fuerza 12 · Destreza 16 · Constitución 14</li>
        <li>PV 27/31 · CA 15</li>
        <li>Efectos: bendecido (+2)</li>
      </ul>
    ),
  },
]

function tokensFor(variant: Variant | undefined): Token[] {
  if (variant === 'vacio') {
    return []
  }
  if (variant === 'extremo') {
    return Array.from({ length: 14 }, (_, index) => ({
      name: index === 0
        ? 'Guardia de élite de la ciudadela de Scaldar con armadura ceremonial'
        : `Token ${index + 1}`,
      x: 60 + (index % 7) * 130,
      y: 70 + Math.floor(index / 7) * 140,
      color: index % 3 === 0 ? '#8f84d8' : index % 3 === 1 ? '#7fbf8e' : '#d6a75c',
      actor: index % 2 ? `Actor ${index + 1}` : undefined,
    }))
  }
  return [
    { name: 'Elenar', x: 220, y: 160, color: '#8f84d8', actor: 'Elenar' },
    { name: 'Guardia del puerto', x: 430, y: 240, color: '#d6a75c', actor: 'Guardia' },
    { name: 'Brom', x: 320, y: 330, color: '#7fbf8e', actor: 'Brom' },
    { name: 'Syl', x: 540, y: 130, color: '#79a9d6' },
    { name: 'Trampa', x: 640, y: 360, color: '#d27272' },
  ]
}

function ToolsDock() {
  const { open, windows } = useWindows()
  return (
    <>
      {demoTools.map((tool) => (
        <Button key={tool.id} variant="secondary" onClick={() => open(tool.id)}>
          {tool.title}
        </Button>
      ))}
      <span className="proto-mesa__status">{windows.filter((w) => !w.minimized).length} ventana(s)</span>
    </>
  )
}

function ToolsWindows() {
  const { windows, focus } = useWindows()
  const minimized = windows.filter((w) => w.minimized)
  return (
    <div className="pv-window-host mesa-window-host">
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
      {minimized.length > 0 ? (
        <div className="pv-window-tray">
          {minimized.map((win) => (
            <button key={win.id} type="button" className="pv-window-tray__item" onClick={() => focus(win.id)}>
              {demoTools.find((tool) => tool.id === win.toolId)?.title ?? win.toolId}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}

/** PMB-2.8 — Mesa de juego: el tablero manda, las herramientas alrededor. */
export function MesaPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const [selected, setSelected] = useState<string | null>(variant === 'normal' ? 'Elenar' : null)
  const tokens = tokensFor(variant)
  const selectedToken = tokens.find((token) => token.name === selected)

  return (
    <PrototypeShell active="mesa">
      <WindowProvider context="table" tools={demoTools}>
        <div className="mesa">
          <nav className="mesa-rail">
          <div className="proto-subnav__title">Escenas</div>
          {['Puerto de Scaldar', 'La ciudadela', 'Cloacas'].map((scene, index) => (
            <div key={scene} className={`proto-subnav__item${index === 0 ? ' proto-subnav__item--active' : ''}`}>
              {scene}
            </div>
          ))}
          <div className="proto-subnav__title">Capas</div>
          {['Suelo', 'Mobiliario', 'Iluminación', 'Secreta (GM)'].map((layer) => (
            <div key={layer} className="proto-subnav__item">
              {layer}
            </div>
          ))}
          <div className="proto-subnav__title">Herramientas</div>
          <div className="mesa-tools">
            {TOOL_ICONS.map((icon, index) => (
              <IconButton
                key={icon}
                icon={<span aria-hidden="true">{icon}</span>}
                label={['Seleccionar', 'Dibujar', 'Medir', 'Niebla', 'Anotar', 'Tokens'][index]}
              />
            ))}
          </div>
          <div className="proto-subnav__title">Biblioteca</div>
          <div className="proto-subnav__item">Abrir biblioteca →</div>
        </nav>

        <div className="mesa-board">
          <div className="mesa-map" aria-hidden="true">
            <span>Mapa: Puerto de Scaldar</span>
          </div>
          {tokens.map((token) => (
            <button
              key={token.name}
              type="button"
              className={`mesa-token${selected === token.name ? ' mesa-token--selected' : ''}`}
              style={{ left: token.x, top: token.y, background: token.color }}
              onClick={() => setSelected(token.name)}
              aria-label={`Token ${token.name}`}
            >
              {token.name}
            </button>
          ))}
          {tokens.length === 0 ? (
            <div className="mesa-empty">
              <EmptyState
                title="Escena sin tokens"
                text="Crea un token con «Añadir token» o arrastra contenido desde la biblioteca."
                action={<Button variant="primary">Añadir token</Button>}
              />
            </div>
          ) : null}
        </div>

        <aside className="mesa-inspector">
          <div className="proto-subnav__title">Inspector</div>
          {selectedToken ? (
            <ul className="proto-tool-body">
              <li>
                <strong>{selectedToken.name}</strong>
                {selectedToken.actor ? <Badge tone="accent">Con actor</Badge> : <Badge tone="neutral">Sin actor</Badge>}
              </li>
              <li>Posición: ({selectedToken.x}, {selectedToken.y}) · grid hex 48</li>
              <li>Versión 4 · movido por ana</li>
              <li>
                <Button variant="ghost">Vincular actor</Button>
                <Button variant="ghost">Eliminar</Button>
              </li>
            </ul>
          ) : (
            <EmptyState title="Nada seleccionado" text="Selecciona un token o elemento para ver sus propiedades." />
          )}
        </aside>

        <div className="mesa-bottom">
          <Button variant="primary">Añadir token</Button>
          <label className="mesa-check">
            <input type="checkbox" defaultChecked /> Ajustar al grid
          </label>
          <label className="mesa-check">
            <input type="checkbox" /> Editar niebla
          </label>
          <Button variant="secondary">Importar mapa</Button>
          <label className="proto-mesa__status">Celda 48</label>
          <label className="proto-mesa__status">Hexagonal</label>
          <Button variant="secondary">Guardar</Button>
          <span style={{ flex: 1 }} />
          <div className="mesa-dock">
            <ToolsDock />
          </div>
        </div>

        {/* Ventanas flotantes sobre toda la mesa (sistema PMB-1.5). */}
        <ToolsWindows />
        </div>
      </WindowProvider>
    </PrototypeShell>
  )
}
