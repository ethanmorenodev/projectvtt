/**
 * Sistema general de ventanas (PMB-1.5, ADR-033).
 * Extensión por `ToolDescriptor`: una herramienta nueva se registra y no
 * toca el gestor. Persistencia de layout por contexto y usuario.
 */
export { AppWindow } from './AppWindow'
export type { AppWindowProps } from './AppWindow'
export { WindowProvider, useWindows, useMinimizedWindows } from './WindowContext'
export type { WindowProviderProps, ToolEventName, ToolEventPayload } from './WindowContext'
export {
  windowReducer,
  serializeLayout,
  deserializeLayout,
  layoutStorageKey,
} from './layout'
export type { ToolDescriptor, WindowState, WindowAction } from './layout'
