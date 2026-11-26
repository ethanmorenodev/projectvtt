/** Datos de MUESTRA del prototipo de herramientas de mapa (PMB-7.5). */

export interface ProtoLayer {
  id: string
  name: string
  visible: boolean
  opacity: number
  kind: 'mapa' | 'niebla' | 'anotaciones'
}

export interface ProtoAnnotation {
  id: string
  x: number
  y: number
  text: string
}

export const GRID_COLUMNS = 20
export const GRID_ROWS = 12
/** D&D 5e: 1 cuadrado = 5 pies (REGLA del sistema, mostrada en la UI). */
export const FEET_PER_SQUARE = 5

export const SAMPLE_LAYERS: ProtoLayer[] = [
  { id: 'mapa', name: 'Mapa base', visible: true, opacity: 1, kind: 'mapa' },
  { id: 'niebla', name: 'Niebla de guerra', visible: true, opacity: 0.65, kind: 'niebla' },
  { id: 'anotaciones', name: 'Anotaciones', visible: true, opacity: 1, kind: 'anotaciones' },
]

export const SAMPLE_ANNOTATIONS: ProtoAnnotation[] = [
  { id: 'a1', x: 3, y: 2, text: 'Entrada norte' },
  { id: 'a2', x: 12, y: 7, text: 'Trampa' },
]
