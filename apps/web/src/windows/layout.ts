import type { ReactNode } from 'react'

/**
 * Sistema general de ventanas (PMB-1.5, ADR-033 / docs/architecture/WINDOWS.md).
 * Una herramienta se registra como `ToolDescriptor` y el gestor la dibuja,
 * posiciona, redimensiona, minimiza y persiste — sin que cada herramienta
 * tenga su propio sistema.
 */

export interface ToolDescriptor {
  id: string
  title: string
  icon?: ReactNode
  minWidth?: number
  minHeight?: number
  defaultWidth?: number
  defaultHeight?: number
  /** Si es falsa, cada apertura crea una instancia nueva. */
  singleton?: boolean
  content: (params?: unknown) => ReactNode
}

export interface WindowState {
  /** Instancia (puede haber varias del mismo tool). */
  id: string
  toolId: string
  x: number
  y: number
  width: number
  height: number
  /** Límites fijados al abrir (del ToolDescriptor). */
  minWidth: number
  minHeight: number
  z: number
  minimized: boolean
  params?: unknown
}

export type WindowAction =
  | { type: 'open'; id: string; tool: ToolDescriptor; params?: unknown }
  | { type: 'close'; id: string }
  | { type: 'focus'; id: string }
  | { type: 'move'; id: string; x: number; y: number }
  | { type: 'resize'; id: string; width: number; height: number }
  | { type: 'minimize'; id: string; minimized: boolean }
  | { type: 'restore-layout'; windows: WindowState[] }

const TOP = 24
const LEFT = 24
const STEP = 28

function toolLimits(tool: ToolDescriptor) {
  return {
    minWidth: tool.minWidth ?? 280,
    minHeight: tool.minHeight ?? 180,
    defaultWidth: tool.defaultWidth ?? 360,
    defaultHeight: tool.defaultHeight ?? 280,
  }
}

/** Reductor puro de ventanas: todo el comportamiento vive aquí y se prueba. */
export function windowReducer(windows: WindowState[], action: WindowAction): WindowState[] {
  switch (action.type) {
    case 'open': {
      const { tool } = action
      if (tool.singleton) {
        const existing = windows.find((w) => w.toolId === tool.id)
        if (existing) {
          return windowReducer(windows, { type: 'focus', id: existing.id })
        }
      }
      const limits = toolLimits(tool)
      const cascade = windows.length * STEP
      const top = Math.max(...windows.map((w) => w.z), 0) + 1
      return [
        ...windows,
        {
          id: action.id,
          toolId: tool.id,
          x: LEFT + (cascade % 160),
          y: TOP + (cascade % 120),
          width: limits.defaultWidth,
          height: limits.defaultHeight,
          minWidth: limits.minWidth,
          minHeight: limits.minHeight,
          z: top,
          minimized: false,
          params: action.params,
        },
      ]
    }
    case 'close':
      return windows.filter((w) => w.id !== action.id)
    case 'focus': {
      const target = windows.find((w) => w.id === action.id)
      if (!target) {
        return windows
      }
      // En lo alto y visible: nada que hacer (pero una minimizada se
      // restaura aunque ya sea la más alta).
      const isTop = target.z === Math.max(...windows.map((w) => w.z))
      if (isTop && !target.minimized) {
        return windows
      }
      const top = Math.max(...windows.map((w) => w.z)) + 1
      return windows.map((w) => (w.id === action.id ? { ...w, z: top, minimized: false } : w))
    }
    case 'move':
      return windows.map((w) =>
        w.id === action.id ? { ...w, x: Math.max(0, action.x), y: Math.max(0, action.y) } : w,
      )
    case 'resize':
      return windows.map((w) => {
        if (w.id !== action.id) {
          return w
        }
        return {
          ...w,
          width: Math.max(w.minWidth, action.width),
          height: Math.max(w.minHeight, action.height),
        }
      })
    case 'minimize':
      return windows.map((w) => (w.id === action.id ? { ...w, minimized: action.minimized } : w))
    case 'restore-layout':
      return action.windows
    default:
      return windows
  }
}

/** Serializa el layout para persistirlo (localStorage por contexto y usuario). */
export function serializeLayout(windows: WindowState[]): string {
  return JSON.stringify(windows)
}

/** Restaura un layout; ignora entradas corruptas sin romper. */
export function deserializeLayout(raw: string | null): WindowState[] {
  if (!raw) {
    return []
  }
  try {
    const parsed = JSON.parse(raw) as unknown
    if (!Array.isArray(parsed)) {
      return []
    }
    return parsed
      .filter(
        (entry): entry is WindowState =>
          typeof entry === 'object' && entry !== null && 'id' in entry && 'toolId' in entry,
      )
      .map((entry) => ({
        ...entry,
        minWidth: entry.minWidth ?? 280,
        minHeight: entry.minHeight ?? 180,
      }))
  } catch {
    return []
  }
}

export function layoutStorageKey(context: string, userId?: string): string {
  return `pv-windows:${context}:${userId ?? 'anon'}`
}
