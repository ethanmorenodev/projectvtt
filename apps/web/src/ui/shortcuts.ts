/** Atajos compartidos entre la ayuda y la vista que los implementa. */

export interface ShortcutHint {
  keys: string[]
  label: string
}

/** Teclas de las acciones rápidas de la mesa (solo activas en la mesa). */
export const MESA_KEYS = {
  roll: 't',
  annotate: 'n',
  measure: 'm',
  fog: 'f',
} as const
