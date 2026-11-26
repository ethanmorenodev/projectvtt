import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * Auditoría de accesibilidad (PMB-8.4) sobre la fuente única de verdad del
 * sistema visual (`tokens.css`) y las hojas de estilo de la app. Comprueba
 * los criterios medibles sin navegador: contraste WCAG de los pares de
 * tokens usados, foco visible, movimiento reducido y tamaño mínimo de
 * controles.
 *
 * Es un archivo .js a propósito: Vitest sustituye los imports de CSS por
 * cadenas vacías, así que se leen los archivos con `node:fs` en runtime y
 * `tsc --noEmit` no necesita tipos de Node (no hay dependencias nuevas).
 *
 * Tras la auditoría inicial (2026-11-17) se oscurecieron `accent` y sus
 * estados hover/active y se aclaró `text-muted` para cumplir AA en todas
 * las superficies (decisión del propietario). La prueba congela esos
 * contrastes como línea base: fallará si cualquier token se degrada.
 */
const read = (path) => readFileSync(path, 'utf8')

const tokens = read('src/theme/tokens.css')

const color = (name) => {
  const match = new RegExp(`--pv-${name}:\\s*(#[0-9a-fA-F]{6})`).exec(tokens)
  if (!match) throw new Error(`Token --pv-${name} no encontrado en tokens.css`)
  return match[1]
}

const luminance = (hex) => {
  const channels = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
  const [r, g, b] = channels.map((c) =>
    c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4)
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

const contrast = (fg, bg) => {
  const a = luminance(fg)
  const b = luminance(bg)
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}

/** Pares que superan AA para texto normal (≥4,5:1). */
const AA_PAIRS = [
  ['text-primary', 'surface-0', 'Título sobre fondo base'],
  ['text-primary', 'surface-1', 'Texto sobre paneles'],
  ['text-primary', 'surface-2', 'Texto sobre superficies elevadas'],
  ['text-secondary', 'surface-0', 'Texto secundario'],
  ['text-secondary', 'surface-1', 'Texto secundario en paneles'],
  ['text-secondary', 'surface-2', 'Texto secundario en superficies elevadas'],
  ['text-muted', 'surface-0', 'Texto atenuado sobre fondo base'],
  ['text-muted', 'surface-1', 'Texto atenuado sobre paneles'],
  ['text-muted', 'surface-2', 'Texto atenuado en superficie elevada'],
  ['text-muted', 'surface-3', 'Texto atenuado en superficie muy elevada'],
  ['text-muted', 'surface-4', 'Texto atenuado en superficie máxima'],
  ['accent-on', 'accent', 'Texto del botón primario'],
  ['accent-on', 'accent-hover', 'Texto del botón en hover'],
  ['accent-on', 'accent-active', 'Texto del botón pulsado'],
  ['success', 'success-bg', 'Estado positivo'],
  ['warning', 'warning-bg', 'Estado de aviso'],
  ['danger', 'danger-bg', 'Estado destructivo'],
  ['info', 'info-bg', 'Estado informativo'],
]

describe('Accesibilidad — contraste de tokens (PMB-8.4)', () => {
  it('todos los pares de texto superan AA (≥4,5:1)', () => {
    for (const [fg, bg, label] of AA_PAIRS) {
      const ratio = contrast(color(fg), color(bg))
      expect(ratio, `${label}: ${fg} sobre ${bg}`).toBeGreaterThanOrEqual(4.5)
    }
  })
})

describe('Accesibilidad — foco, movimiento y controles (PMB-8.4)', () => {
  it('define anillo de foco visible y tamaño mínimo de control de 28 px', () => {
    expect(tokens).toMatch(/--pv-focus-ring:\s*2px solid/)
    expect(tokens).toMatch(/--pv-control-height:\s*28px/)
  })

  it('respeta prefers-reduced-motion anulando las duraciones', () => {
    expect(tokens).toMatch(/@media \(prefers-reduced-motion: reduce\)/)
  })

  it('el tema legacy (styles.css) no diverge de los tokens de acento y texto', () => {
    const legacy = read('src/styles.css')
    expect(legacy).toMatch(new RegExp(`--accent:\\s*${color('accent')}`))
    expect(legacy).toMatch(new RegExp(`--muted:\\s*${color('text-muted')}`))
  })

  it('aplica foco visible en los controles interactivos de la app real', () => {
    const css = [
      'ui/ui.css', 'shell/shell.css', 'windows/windows.css',
      'views/mesa.css', 'views/library.css', 'views/campaign-space.css',
    ].map((path) => read(`src/${path}`)).join('\n')
    expect(css).toMatch(/:focus-visible/)
    expect(css).not.toMatch(/outline:\s*none\s*;/)
  })
})
