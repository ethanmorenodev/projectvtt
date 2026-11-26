import { useCallback, useSyncExternalStore } from 'react'

/**
 * Router propio con URLs reales e historial (PMB-1.4). Sin dependencias:
 * la tabla de rutas es pequeña y necesitamos control total del historial
 * (recarga y botón "atrás" funcionan — criterio de PMB-1.4). Si las rutas
 * crecieran en complejidad, se reevaluaría una librería.
 */

export type ParsedRoute =
  | { kind: 'home' }
  | { kind: 'login' }
  | { kind: 'campaigns' }
  | { kind: 'campaign'; campaignId: string }
  | { kind: 'scene'; campaignId: string; sceneId: string }
  | { kind: 'table'; campaignId: string }
  | { kind: 'character-create'; campaignId: string }
  | { kind: 'character-draft'; campaignId: string; actorId: string }
  | { kind: 'inicio' }
  | { kind: 'biblioteca' }
  | { kind: 'herramientas' }
  | { kind: 'ajustes' }
  | { kind: 'imagenes' }
  | { kind: 'dm' }
  | { kind: 'lore' }
  | { kind: 'inventario'; actorId: string }
  | { kind: 'clase-crear' }
  | { kind: 'gestion' }
  | { kind: 'creditos' }
  | { kind: 'not-found' }

const ROUTE_EVENT = 'pv:navigate'

/** Convierte un pathname en ruta de aplicación. */
export function parseRoute(path: string): ParsedRoute {
  const clean = path.replace(/\/+$/, '') || '/'
  if (clean === '/' || clean === '/campaigns') {
    return clean === '/' ? { kind: 'home' } : { kind: 'campaigns' }
  }
  if (clean === '/login') {
    return { kind: 'login' }
  }
  const mesaParts = clean.split('/').filter(Boolean)
  if (mesaParts[0] === 'campaigns' && mesaParts[2] === 'mesa' && mesaParts.length === 3 && isId(mesaParts[1])) {
    return { kind: 'table', campaignId: mesaParts[1] }
  }
  if (clean === '/inicio') {
    return { kind: 'inicio' }
  }
  if (clean === '/biblioteca') {
    return { kind: 'biblioteca' }
  }
  if (clean === '/herramientas') {
    return { kind: 'herramientas' }
  }
  if (clean === '/ajustes') {
    return { kind: 'ajustes' }
  }
  if (clean === '/gestion') {
    return { kind: 'gestion' }
  }
  if (clean === '/imagenes') {
    return { kind: 'imagenes' }
  }
  if (clean === '/dm') {
    return { kind: 'dm' }
  }
  if (clean === '/lore') {
    return { kind: 'lore' }
  }
  if (clean === '/clases/crear') return { kind: 'clase-crear' }
  const inventoryParts = clean.split('/').filter(Boolean);
  if (inventoryParts[0] === 'inventario' && inventoryParts.length === 2 && isId(inventoryParts[1])) {
    return { kind: 'inventario', actorId: inventoryParts[1] };
  }
  if (clean === '/creditos') {
    return { kind: 'creditos' }
  }
  const parts = clean.split('/').filter(Boolean)
  if (parts[0] === 'campaigns' && parts.length === 2 && isId(parts[1])) {
    return { kind: 'campaign', campaignId: parts[1] }
  }
  if (
    parts[0] === 'campaigns' &&
    parts[2] === 'scenes' &&
    parts.length === 4 &&
    isId(parts[1]) &&
    isId(parts[3])
  ) {
    return { kind: 'scene', campaignId: parts[1], sceneId: parts[3] }
  }
  // Creador de personajes (PMB-3.5): nuevo o borrador guardado.
  if (parts[0] === 'campaigns' && parts[2] === 'characters' && parts.length === 4 && isId(parts[1])) {
    if (parts[3] === 'new') {
      return { kind: 'character-create', campaignId: parts[1] }
    }
    if (isId(parts[3])) {
      return { kind: 'character-draft', campaignId: parts[1], actorId: parts[3] }
    }
  }
  return { kind: 'not-found' }
}

function isId(value: string | undefined): value is string {
  return Boolean(value) && /^[\w-]+$/.test(value!)
}

/** Ruta absoluta para construir enlaces y navegaciones. */
export function routePath(route: ParsedRoute): string {
  switch (route.kind) {
    case 'home':
    case 'campaigns':
      return '/campaigns'
    case 'login':
      return '/login'
    case 'campaign':
      return `/campaigns/${route.campaignId}`
    case 'scene':
      return `/campaigns/${route.campaignId}/scenes/${route.sceneId}`
    case 'table':
      return `/campaigns/${route.campaignId}/mesa`
    case 'character-create':
      return `/campaigns/${route.campaignId}/characters/new`
    case 'character-draft':
      return `/campaigns/${route.campaignId}/characters/${route.actorId}`
    case 'inicio':
      return '/inicio'
    case 'biblioteca':
      return '/biblioteca'
    case 'herramientas':
      return '/herramientas'
    case 'ajustes':
      return '/ajustes'
    case 'imagenes':
      return '/imagenes'
    case 'dm':
      return '/dm'
    case 'lore':
      return '/lore'
    case 'inventario':
      return `/inventario/${route.actorId}`
    case 'clase-crear':
      return '/clases/crear'
    case 'gestion':
      return '/gestion'
    case 'creditos':
      return '/creditos'
    default:
      return '/campaigns'
  }
}

/** Navega con el historial del navegador (push o replace). */
export function navigate(path: string, replace = false): void {
  if (replace) {
    window.history.replaceState(null, '', path)
  } else {
    window.history.pushState(null, '', path)
  }
  window.dispatchEvent(new Event(ROUTE_EVENT))
}

function subscribe(callback: () => void): () => void {
  window.addEventListener('popstate', callback)
  window.addEventListener(ROUTE_EVENT, callback)
  return () => {
    window.removeEventListener('popstate', callback)
    window.removeEventListener(ROUTE_EVENT, callback)
  }
}

function snapshot(): string {
  return window.location.pathname
}

/** Localización actual reactiva (pushState, replaceState y botón "atrás"). */
export function useLocation(): { path: string; route: ParsedRoute; navigate: typeof navigate } {
  const path = useSyncExternalStore(subscribe, snapshot)
  return { path, route: parseRoute(path), navigate }
}

/** Navegación programática estable para handlers. */
export function useNavigate(): (route: ParsedRoute, replace?: boolean) => void {
  return useCallback((route: ParsedRoute, replace = false) => navigate(routePath(route), replace), [])
}
