import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ModernLayoutPrototype } from './ModernLayoutPrototype'

afterEach(cleanup)

describe('Prototipo de rediseño moderno', () => {
  it('muestra navegación de escenas, rail compacto, panel con pestañas y dock', () => {
    render(<ModernLayoutPrototype />)
    expect(screen.getByRole('navigation', { name: 'Escenas' })).toBeTruthy()
    expect(screen.getByRole('main', { name: 'Tablero' })).toBeTruthy()
    expect(screen.getByRole('complementary', { name: 'Inspector' })).toBeTruthy()
    expect(screen.getByRole('contentinfo', { name: 'Acciones rápidas' })).toBeTruthy()
    expect(screen.getAllByRole('tab').length).toBe(3)
  })

  it('usa datos de muestra: tokens y regla de medición', () => {
    const { container } = render(<ModernLayoutPrototype />)
    expect(container.querySelectorAll('.modern__token').length).toBe(3)
    expect(screen.getByText(/35 ft · 7 cuadrados/)).toBeTruthy()
  })
})
