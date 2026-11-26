/** Datos de MUESTRA del prototipo PMB-4.10 (sustitución sin romper referencias). */

export interface ProtoAsset {
  id: string
  fileName: string
  role: string
  packKey: string | null
  license: string
  sizeBytes: number
  references: string[]
  variants: string[]
}

export const SAMPLE_ASSETS: ProtoAsset[] = [
  {
    id: 'img-dwarf',
    fileName: 'enano.png',
    role: 'miniatura',
    packKey: 'dnd5e-srd-5.1',
    license: 'CC BY 3.0',
    sizeBytes: 184320,
    references: ['Dwarf', 'Hill Dwarf', 'Mountain Dwarf'],
    variants: ['thumb-sm', 'thumb-md', 'thumb-lg'],
  },
  {
    id: 'img-fighter',
    fileName: 'guerrero.png',
    role: 'miniatura',
    packKey: 'dnd5e-srd-5.1',
    license: 'CC BY 3.0',
    sizeBytes: 204800,
    references: ['Fighter', 'Champion'],
    variants: ['thumb-sm', 'thumb-md'],
  },
  {
    id: 'img-bg',
    fileName: 'fondo.png',
    role: 'ilustracion',
    packKey: null,
    license: 'CC0',
    sizeBytes: 512000,
    references: ['Escena Inicio'],
    variants: [],
  },
]
