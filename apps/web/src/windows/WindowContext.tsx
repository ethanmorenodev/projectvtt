import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import {
  deserializeLayout,
  layoutStorageKey,
  serializeLayout,
  windowReducer,
} from './layout'
import type { ToolDescriptor, WindowState } from './layout'

/**
 * Gestor de ventanas por contexto (mesa, biblioteca, editor…): registro de
 * herramientas, ciclo de vida de ventanas y persistencia local del layout
 * (ADR-033). Las herramientas se comunican por `on`/`emit` (bus tipado),
 * sin conocerse entre sí.
 */

export type ToolEventName = 'selection:changed' | 'content:open' | 'character:updated'
export type ToolEventPayload = Record<string, unknown>

interface WindowApi {
  windows: WindowState[]
  tools: ToolDescriptor[]
  open: (toolId: string, params?: unknown) => void
  close: (id: string) => void
  focus: (id: string) => void
  move: (id: string, x: number, y: number) => void
  resize: (id: string, width: number, height: number) => void
  minimize: (id: string, minimized: boolean) => void
  on: (event: ToolEventName, handler: (payload: ToolEventPayload) => void) => () => void
  emit: (event: ToolEventName, payload: ToolEventPayload) => void
}

const WindowContext = createContext<WindowApi | null>(null)

export function useWindows(): WindowApi {
  const api = useContext(WindowContext)
  if (!api) {
    throw new Error('useWindows requiere un WindowProvider')
  }
  return api
}

export interface WindowProviderProps {
  /** Contexto de persistencia (p. ej. "table", "library"). */
  context: string
  userId?: string
  tools: ToolDescriptor[]
  children: ReactNode
}

export function WindowProvider({ context, userId, tools, children }: WindowProviderProps) {
  const storageKey = layoutStorageKey(context, userId)
  const [windows, dispatch] = useReducer(windowReducer, undefined, () =>
    deserializeLayout(windowStorage.get(storageKey)),
  )
  const [counter, setCounter] = useState(0)
  const handlers = useRef(new Map<ToolEventName, Set<(payload: ToolEventPayload) => void>>())

  useEffect(() => {
    windowStorage.set(storageKey, serializeLayout(windows))
  }, [windows, storageKey])

  const open = useCallback(
    (toolId: string, params?: unknown) => {
      const tool = tools.find((entry) => entry.id === toolId)
      if (!tool) {
        return
      }
      setCounter((n) => n + 1)
      dispatch({ type: 'open', id: `${toolId}-${Date.now()}-${counter}`, tool, params })
    },
    [tools, counter],
  )

  const on = useCallback((event: ToolEventName, handler: (payload: ToolEventPayload) => void) => {
    const set = handlers.current.get(event) ?? new Set()
    set.add(handler)
    handlers.current.set(event, set)
    return () => set.delete(handler)
  }, [])

  const emit = useCallback((event: ToolEventName, payload: ToolEventPayload) => {
    for (const handler of handlers.current.get(event) ?? []) {
      handler(payload)
    }
  }, [])

  const api = useMemo<WindowApi>(
    () => ({
      windows,
      tools,
      open,
      close: (id) => dispatch({ type: 'close', id }),
      focus: (id) => dispatch({ type: 'focus', id }),
      move: (id, x, y) => dispatch({ type: 'move', id, x, y }),
      resize: (id, width, height) => dispatch({ type: 'resize', id, width, height }),
      minimize: (id, minimized) => dispatch({ type: 'minimize', id, minimized }),
      on,
      emit,
    }),
    [windows, tools, open, on, emit],
  )

  return <WindowContext.Provider value={api}>{children}</WindowContext.Provider>
}

/** Acceso seguro a localStorage (también en jsdom/pruebas). */
const windowStorage = {
  get(key: string): string | null {
    try {
      return window.localStorage.getItem(key)
    } catch {
      return null
    }
  },
  set(key: string, value: string): void {
    try {
      window.localStorage.setItem(key, value)
    } catch {
      // Sin almacenamiento persistente: el layout vive solo en la sesión.
    }
  },
}

/** Filas de la bandeja de minimizadas (para hosts que quieran mostrarlas). */
export function useMinimizedWindows(): WindowState[] {
  const { windows } = useWindows()
  return windows.filter((w) => w.minimized)
}
