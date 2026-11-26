/**
 * Datos de MUESTRA del prototipo de biblioteca de imágenes (PMB-4.7).
 * No se descarga contenido externo: dos miniaturas usan recursos locales de
 * `apps/web/public` y el resto son marcadores de posición. Las licencias y
 * atribuciones son ejemplos honestos de lo que debe registrar la app real.
 */

export type ImageRole = 'icono' | 'miniatura' | 'ilustracion'

export interface ProtoImage {
  id: string
  name: string
  role: ImageRole
  albums: string[]
  tags: string[]
  license: string
  licenseUrl: string | null
  author: string
  origin: string
  attribution: string
  /** Recurso local disponible; si falta se dibuja un marcador con `glyph`. */
  src?: string
  glyph?: string
}

export const ROLE_LABELS: Record<ImageRole, string> = {
  icono: 'Icono',
  miniatura: 'Miniatura',
  ilustracion: 'Ilustración',
}

export const SAMPLE_IMAGES: ProtoImage[] = [
  {
    id: 'img-logo',
    name: 'Logotipo ProjectVTT',
    role: 'icono',
    albums: ['Interfaz'],
    tags: ['ui', 'marca'],
    license: 'Propietario',
    licenseUrl: null,
    author: 'ProjectVTT',
    origin: 'Subido por el usuario',
    attribution: 'Logotipo del proyecto. Uso interno.',
    src: '/logo-projectvtt.png',
  },
  {
    id: 'img-login',
    name: 'Fondo de inicio de sesión',
    role: 'ilustracion',
    albums: ['Escenarios'],
    tags: ['fondo', 'ui'],
    license: 'CC0',
    licenseUrl: 'https://creativecommons.org/publicdomain/zero/1.0/',
    author: 'Proyecto VTT',
    origin: 'Recurso propio',
    attribution: 'Fondo neutro de inicio de sesión, liberado como CC0.',
    src: '/login-bg-neutral.jpg',
  },
  {
    id: 'img-dwarf',
    name: 'Miniatura Enano',
    role: 'miniatura',
    albums: ['Personajes'],
    tags: ['srd', 'dnd5e'],
    license: 'CC BY 3.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/3.0/',
    author: 'game-icons.net',
    origin: 'Paquete de iconos',
    attribution: 'Icono de enano de game-icons.net, licenciado bajo CC BY 3.0.',
    glyph: 'D',
  },
  {
    id: 'img-dragon',
    name: 'Miniatura Dragón',
    role: 'miniatura',
    albums: ['Personajes'],
    tags: ['srd', 'dnd5e'],
    license: 'CC BY 3.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/3.0/',
    author: 'game-icons.net',
    origin: 'Paquete de iconos',
    attribution: 'Icono de dragón de game-icons.net, licenciado bajo CC BY 3.0.',
    glyph: '🐉',
  },
  {
    id: 'img-scroll',
    name: 'Icono pergamino',
    role: 'icono',
    albums: ['Interfaz'],
    tags: ['ui'],
    license: 'CC BY 3.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/3.0/',
    author: 'game-icons.net',
    origin: 'Paquete de iconos',
    attribution: 'Icono de pergamino de game-icons.net, licenciado bajo CC BY 3.0.',
    glyph: '📜',
  },
  {
    id: 'img-map',
    name: 'Mapa de campaña',
    role: 'ilustracion',
    albums: ['Escenarios'],
    tags: ['mapa', 'cc0'],
    license: 'CC0',
    licenseUrl: 'https://creativecommons.org/publicdomain/zero/1.0/',
    author: 'Proyecto VTT',
    origin: 'Recurso propio',
    attribution: 'Mapa de ejemplo generado para el prototipo, CC0.',
    glyph: '🗺️',
  },
]

export interface ProtoPack {
  key: string
  name: string
  license: string
  licenseUrl: string
  attribution: string
}

export const SAMPLE_PACKS: ProtoPack[] = [
  {
    key: 'dnd5e-srd-5.1',
    name: 'D&D 5e — System Reference Document 5.1',
    license: 'CC BY 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
    attribution:
      'Wizards of the Coast, System Reference Document 5.1 (2016), licensed under Creative Commons Attribution 4.0 International (CC BY 4.0).',
  },
]

/** Texto exportable con la nota de atribución (ADR-036 §5). */
export function attributionNote(): string {
  const packs = SAMPLE_PACKS.map(
    (pack) => `${pack.name} — ${pack.license} (${pack.licenseUrl})\n${pack.attribution}`,
  )
  const images = SAMPLE_IMAGES.map(
    (image) => `${image.name} — ${image.license}${image.licenseUrl ? ` (${image.licenseUrl})` : ''}\n${image.attribution}`,
  )
  return ['Atribuciones de ProjectVTT', '', ...packs, '', ...images].join('\n')
}
