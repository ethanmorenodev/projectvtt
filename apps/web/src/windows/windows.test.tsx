import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { AppWindow } from './AppWindow'
import { WindowProvider, useWindows } from './WindowContext'
import {
  deserializeLayout,
  layoutStorageKey,
  serializeLayout,
  windowReducer,
} from './layout'
import type { ToolDescriptor } from './layout'

// Vitest no usa globals: sin auto-limpieza de RTL, los renderizados se
// acumularían entre tests del mismo fichero. El localStorage se limpia
// para que la persistencia de un test no contamine al siguiente.
afterEach(() => {
  cleanup()
  window.localStorage.clear()
})

const chatTool: ToolDescriptor = {
  id: 'chat',
  title: 'Chat',
  singleton: true,
  minWidth: 200,
  minHeight: 120,
  defaultWidth: 320,
  defaultHeight: 240,
  content: () => <span>chat</span>,
}

const fichaTool: ToolDescriptor = {
  id: 'ficha',
  title: 'Ficha',
  defaultWidth: 400,
  defaultHeight: 300,
  content: () => <span>ficha</span>,
}

describe('windowReducer', () => {
  it('abre con tamaño por defecto y foco en la ventana más alta', () => {
    const one = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    const two = windowReducer(one, { type: 'open', id: 'w2', tool: fichaTool })
    expect(two).toHaveLength(2)
    expect(two[0].width).toBe(320)
    expect(two[1].z).toBeGreaterThan(two[0].z)
  })

  it('las herramientas singleton reutilizan su ventana', () => {
    const one = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    const again = windowReducer(one, { type: 'open', id: 'w2', tool: chatTool })
    expect(again).toHaveLength(1)
  })

  it('focus eleva la ventana y restaura la minimizada', () => {
    let state = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    state = windowReducer(state, { type: 'open', id: 'w2', tool: fichaTool })
    state = windowReducer(state, { type: 'minimize', id: 'w1', minimized: true })
    state = windowReducer(state, { type: 'focus', id: 'w1' })
    const w1 = state.find((w) => w.id === 'w1')!
    expect(w1.minimized).toBe(false)
    expect(w1.z).toBeGreaterThan(state.find((w) => w.id === 'w2')!.z)
  })

  it('focus restaura una minimizada aunque ya sea la más alta', () => {
    let state = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    state = windowReducer(state, { type: 'minimize', id: 'w1', minimized: true })
    state = windowReducer(state, { type: 'focus', id: 'w1' })
    expect(state[0].minimized).toBe(false)
  })

  it('redimensionar respeta el mínimo de la herramienta', () => {
    const state = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    const resized = windowReducer(state, { type: 'resize', id: 'w1', width: 50, height: 10 })
    expect(resized[0].width).toBe(200)
    expect(resized[0].height).toBe(120)
  })

  it('mover no deja coordenadas negativas y close elimina', () => {
    let state = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    state = windowReducer(state, { type: 'move', id: 'w1', x: -40, y: -5 })
    expect(state[0].x).toBe(0)
    expect(state[0].y).toBe(0)
    state = windowReducer(state, { type: 'close', id: 'w1' })
    expect(state).toHaveLength(0)
  })

  it('serializa y restaura el layout ignorando datos corruptos', () => {
    const state = windowReducer([], { type: 'open', id: 'w1', tool: chatTool })
    const raw = serializeLayout(state)
    expect(deserializeLayout(raw)[0].toolId).toBe('chat')
    expect(deserializeLayout('no es json')).toEqual([])
    expect(deserializeLayout('{"a":1}')).toEqual([])
    expect(deserializeLayout(null)).toEqual([])
  })
})

function Harness() {
  const { windows, open, minimize, focus } = useWindows()
  const first = windows[0]?.id
  return (
    <div>
      <button type="button" onClick={() => open('chat')}>
        Abrir chat
      </button>
      <button type="button" onClick={() => open('chat')}>
        Abrir chat otra vez
      </button>
      <button type="button" onClick={() => first && minimize(first, true)}>
        Minimizar
      </button>
      <button type="button" onClick={() => first && focus(first)}>
        Restaurar
      </button>
      {windows.map((win) => (
        <AppWindow
          key={win.id}
          window={win}
          title={win.toolId === 'chat' ? 'Chat' : 'Ficha'}
          labels={{ minimize: 'Minimizar ventana', close: 'Cerrar ventana' }}
        >
          <span>contenido de {win.toolId}</span>
        </AppWindow>
      ))}
    </div>
  )
}

describe('AppWindow y WindowProvider', () => {
  it('abre, minimiza y restaura desde la bandeja', () => {
    render(
      <WindowProvider context="table" tools={[chatTool]}>
        <Harness />
      </WindowProvider>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Abrir chat' }))
    expect(screen.getByRole('dialog', { name: 'Chat' })).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Minimizar ventana' }))
    expect(screen.queryByRole('dialog', { name: 'Chat' })).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Restaurar' }))
    expect(screen.getByRole('dialog', { name: 'Chat' })).toBeTruthy()
  })

  it('el botón cerrar elimina la ventana', () => {
    render(
      <WindowProvider context="table" tools={[chatTool]}>
        <Harness />
      </WindowProvider>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Abrir chat' }))
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar ventana' }))
    expect(screen.queryByRole('dialog', { name: 'Chat' })).toBeNull()
  })

  it('persiste el layout por contexto y lo restaura', () => {
    const key = layoutStorageKey('table', 'u1')
    window.localStorage.setItem(
      key,
      JSON.stringify([
        { id: 'guardada', toolId: 'chat', x: 10, y: 20, width: 300, height: 200, z: 3, minimized: false },
      ]),
    )
    render(
      <WindowProvider context="table" userId="u1" tools={[chatTool]}>
        <Harness />
      </WindowProvider>,
    )
    const dialog = screen.getByRole('dialog', { name: 'Chat' }) as HTMLElement
    expect(dialog.style.left).toBe('10px')
    expect(dialog.style.top).toBe('20px')
  })

  it('el arrastre de la cabecera mueve la ventana', () => {
    render(
      <WindowProvider context="table" tools={[chatTool]}>
        <Harness />
      </WindowProvider>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Abrir chat' }))
    const header = screen.getByRole('dialog', { name: 'Chat' }).querySelector('.pv-window__header')!
    fireEvent.pointerDown(header, { button: 0, clientX: 100, clientY: 50 })
    fireEvent.pointerMove(header, { clientX: 160, clientY: 90 })
    fireEvent.pointerUp(header)
    const dialog = screen.getByRole('dialog', { name: 'Chat' }) as HTMLElement
    // Posición inicial (24,24) + delta (60,40).
    expect(dialog.style.left).toBe('84px')
    expect(dialog.style.top).toBe('64px')
  })
})
