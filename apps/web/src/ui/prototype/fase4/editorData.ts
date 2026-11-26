/**
 * Datos del prototipo del editor universal (PMB-4.4) — reflejan el seed
 * real `backend/Api/Content/Seed/dnd5e-srd-5.1.json`: el TypeSchema de
 * `race` (abilityBonuses, speed, size, languages, traits — todos
 * obligatorios), los valores reales de Dwarf y Hill Dwarf, y su mapa de
 * herencia. El único item inventado es el homebrew de campaña, claramente
 * etiquetado. Solo desarrollo.
 */

import type { ProtoItem } from './bibliotecaData'

export type FieldKind = 'abilityList' | 'stringList' | 'number' | 'string'

export type SchemaField = {
  key: string
  label: string
  type: 'list' | 'number' | 'string'
  kind: FieldKind
  helper?: string
  /** Nombre singular de una fila de lista (para el botón de añadir). */
  item?: string
}

/** TypeSchema real de `race` (seed dnd5e-srd-5.1). */
export const RACE_SCHEMA: SchemaField[] = [
  {
    key: 'abilityBonuses',
    label: 'Bonificaciones de atributo',
    type: 'list',
    kind: 'abilityList',
    helper: 'Atributo y bonificación por fila.',
    item: 'bonificación',
  },
  { key: 'speed', label: 'Velocidad', type: 'number', kind: 'number', helper: 'En pies (ft).' },
  { key: 'size', label: 'Tamaño', type: 'string', kind: 'string' },
  {
    key: 'languages',
    label: 'Idiomas',
    type: 'list',
    kind: 'stringList',
    item: 'idioma',
  },
  { key: 'traits', label: 'Rasgos', type: 'list', kind: 'stringList', item: 'rasgo' },
]

export type InheritanceMode = 'inherit' | 'override' | 'extend'

export const MODE_LABELS: Record<InheritanceMode, string> = {
  inherit: 'Heredar',
  override: 'Sobrescribir',
  extend: 'Extender',
}

export type AbilityBonus = { ability: string; bonus: number }

/** Valores reales del padre Dwarf (seed) — para mostrar qué se hereda. */
export const PARENT_DWARF = {
  name: 'Dwarf',
  properties: {
    abilityBonuses: [{ ability: 'Constitution', bonus: 2 }] as AbilityBonus[],
    speed: 25,
    size: 'Medium',
    languages: ['Common', 'Dwarvish'],
    traits: [
      'Darkvision (60 ft)',
      'Dwarven Resilience (advantage on saves vs poison; resistance to poison damage)',
      'Dwarven Combat Training (battleaxe, handaxe, light hammer, warhammer)',
      'Tool Proficiency (smith’s, brewer’s or mason’s tools)',
      'Stonecunning (double proficiency on History checks related to stonework)',
    ],
  },
}

/** Item homebrew en edición (variantes `normal` y `error`). */
export const HOMEBREW = {
  key: 'race/dwarf/hill-dwarf-saldar',
  name: 'Hill Dwarf de Scaldar',
  summary:
    'Subraza homebrew de la campaña: guardia de la muralla de Scaldar, resistente a las heridas de asedio.',
  description:
    'Los hill dwarfs de Scaldar sirven en la muralla desde su juventud; su temple hace que resistan el impacto de los arietes.',
  tags: ['subraza', 'homebrew'],
  parentKey: 'race/dwarf',
  scope: 'campaign',
  status: 'Publicado',
  revision: 2,
  properties: {
    abilityBonuses: [{ ability: 'Wisdom', bonus: 1 }] as AbilityBonus[],
    speed: 25,
    size: 'Medium',
    languages: [] as string[],
    traits: [
      'Dwarven Toughness de Scaldar: tu PV máximo aumenta en 2 por nivel (homebrew de campaña).',
    ],
  },
  inheritance: {
    abilityBonuses: 'extend',
    traits: 'extend',
    languages: 'extend',
  } as Record<string, InheritanceMode>,
  revisions: [
    { number: 2, message: 'Ajuste del rasgo tras la sesión 3', date: '2026-11-09T18:30:00Z' },
    { number: 1, message: 'Alta de la subraza', date: '2026-11-05T19:00:00Z' },
  ],
}

/** Item oficial del SRD en solo lectura (variante `solo-lectura`). */
export const SRD_HILL_DWARF = {
  key: 'race/dwarf/hill-dwarf',
  name: 'Hill Dwarf',
  summary:
    'As a hill dwarf, you have keen senses, deep intuition, and remarkable resilience.',
  description: '',
  tags: [] as string[],
  parentKey: 'race/dwarf',
  scope: 'global',
  status: 'Publicado',
  revision: 1,
  properties: {
    abilityBonuses: [{ ability: 'Wisdom', bonus: 1 }] as AbilityBonus[],
    speed: 25,
    size: 'Medium',
    languages: [] as string[],
    traits: [
      'Dwarven Toughness (your hit point maximum increases by 1 every time you gain a level)',
    ],
  },
  inheritance: {
    abilityBonuses: 'extend',
    traits: 'extend',
    languages: 'extend',
  } as Record<string, InheritanceMode>,
  revisions: [
    { number: 1, message: 'Importación del paquete dnd5e-srd-5.1', date: '2026-11-10T09:00:00Z' },
  ],
}

/** Árbol de Razas del prototipo: seed + el homebrew en edición. */
export const TREE_HOMEBREW: ProtoItem = {
  key: HOMEBREW.key,
  name: HOMEBREW.name,
  summary: HOMEBREW.summary,
  props: [],
  parent: HOMEBREW.parentKey,
}

/** Forma de un item en el editor (espejo de las secciones de LIBRARY §3). */
export type EditorItem = {
  key: string
  name: string
  summary: string
  description: string
  tags: string[]
  parentKey: string | null
  scope: string
  status: string
  revision: number
  properties: {
    abilityBonuses: AbilityBonus[]
    speed?: number | string
    size?: string
    languages: string[]
    traits: string[]
  }
  inheritance: Record<string, InheritanceMode>
  revisions: { number: number; message: string; date: string }[]
}

/**
 * Variante «error»: sin claves `speed` ni `size` — exactamente lo que el
 * servidor rechaza con «Falta la propiedad obligatoria 'speed'/'size'»
 * (ContentSchema), mostrando además los modos en Sobrescribir (los campos
 * quedan vacíos y editables).
 */
export const HOMEBREW_ERROR: EditorItem = {
  ...HOMEBREW,
  properties: {
    abilityBonuses: [{ ability: 'Wisdom', bonus: 1 }],
    languages: [],
    traits: HOMEBREW.properties.traits,
  },
  inheritance: { ...HOMEBREW.inheritance, speed: 'override', size: 'override' },
}

/** Item vacío para la variante «nuevo» (scope personal: único disponible en la biblioteca global). */
export const EMPTY_NEW: EditorItem = {
  key: '',
  name: '',
  summary: '',
  description: '',
  tags: [],
  parentKey: null,
  scope: 'user',
  status: 'Borrador',
  revision: 0,
  properties: { abilityBonuses: [], speed: '', size: '', languages: [], traits: [] },
  inheritance: {},
  revisions: [],
}
