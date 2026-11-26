/**
 * Tokens de diseño como constantes tipadas (PMB-1.1).
 * Espejo de `tokens.css` para el lado JS/TS (valores que React necesita
 * como números: tamaños de miniatura, alturas). La fuente de verdad visual
 * sigue siendo docs/visual/DESIGN_TOKENS.md.
 */

/** Papel de un recurso visual (§17.3 de la investigación de assets). */
export type AssetRole = 'icon' | 'thumbnail' | 'illustration'

export const tokens = {
  color: {
    surface0: '#16171a',
    surface1: '#1b1d21',
    surface2: '#212429',
    surface3: '#282c32',
    surface4: '#31363e',
    borderSubtle: '#33383f',
    borderStrong: '#454b55',
    textPrimary: '#edecf2',
    textSecondary: '#b9b5c4',
    textMuted: '#a39fad',
    accent: '#6259a3',
    accentHover: '#6d63ab',
    accentActive: '#564d8f',
    accentSoft: '#2a2740',
    accentOn: '#f4f1ff',
    danger: '#d27272',
    success: '#7fbf8e',
    warning: '#d6a75c',
    info: '#79a9d6',
  },
  space: { 1: 2, 2: 4, 3: 6, 4: 8, 5: 12, 6: 16, 7: 20, 8: 24, 9: 32, 10: 48 },
  radius: { xs: 4, sm: 6, md: 8, lg: 12, pill: 999 },
  control: { height: 28, heightComfortable: 32, rowHeight: 32, iconSize: 16 },
  thumb: { xs: 48, sm: 64, md: 128, lg: 256 },
  duration: { fast: 120, base: 160, slow: 240 },
} as const

/** Tamaños de miniatura por papel de recurso. */
export const thumbSizeForRole: Record<AssetRole, 64 | 128 | 256> = {
  icon: 64,
  thumbnail: 128,
  illustration: 256,
}
