/** Datos de MUESTRA del prototipo de inventario visual (PMB-6.3). */

export type Rarity = 'común' | 'poco común' | 'raro'

export interface ProtoEquipment {
  id: string
  name: string
  slotId: string | null
  glyph: string
  weight: number
  value: number
  rarity: Rarity
  effects: string[]
  container?: string
}

export const SLOT_LABELS: Record<string, string> = {
  'main-hand': 'Arma principal',
  'off-hand': 'Secundaria',
  armor: 'Armadura',
  helmet: 'Casco',
  gloves: 'Guantes',
  boots: 'Botas',
  'accessory-1': 'Accesorio 1',
  'accessory-2': 'Accesorio 2',
}

/** Orden de disposición alrededor de la silueta. */
export const SLOT_ORDER = [
  'main-hand', 'off-hand', 'helmet', 'armor', 'gloves', 'boots',
  'accessory-1', 'accessory-2',
]

export const SAMPLE_EQUIPMENT: ProtoEquipment[] = [
  {
    id: 'eq-longsword', name: 'Longsword', slotId: 'main-hand', glyph: '⚔️',
    weight: 3, value: 15, rarity: 'común',
    effects: ['Daño 1d8 cortante', 'Versátil 1d10 a dos manos'],
  },
  {
    id: 'eq-shield', name: 'Escudo', slotId: 'off-hand', glyph: '🛡️',
    weight: 6, value: 10, rarity: 'común',
    effects: ['+2 CA'],
  },
  {
    id: 'eq-chain-mail', name: 'Chain Mail', slotId: 'armor', glyph: '🧥',
    weight: 55, value: 75, rarity: 'común',
    effects: ['CA 16', 'Desventaja en Sigilo'],
  },
  {
    id: 'eq-backpack', name: 'Backpack', slotId: null, glyph: '🎒',
    weight: 5, value: 2, rarity: 'común',
    effects: ['Contenedor: 30 lb / 1 pie cúbico'],
    container: '30 lb',
  },
  {
    id: 'eq-potion', name: 'Potion of Healing', slotId: null, glyph: '🧪',
    weight: 1, value: 50, rarity: 'poco común',
    effects: ['Recupera 2d4+2 PG'],
  },
  {
    id: 'eq-rope', name: 'Hempen Rope (50 feet)', slotId: null, glyph: '🪢',
    weight: 10, value: 1, rarity: 'común',
    effects: [],
  },
  {
    id: 'eq-boots', name: 'Boots of Elvenkind', slotId: 'boots', glyph: '👢',
    weight: 1, value: 500, rarity: 'raro',
    effects: ['Ventaja en Sigilo'],
  },
]
