import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { VisualAssetsPrototype } from './VisualAssetsPrototype'

afterEach(cleanup)

describe('Prototipo de mejora visual (iconografía)', () => {
  it('muestra la comparación antes/después y el catálogo con licencia', () => {
    render(<VisualAssetsPrototype />)
    expect(screen.getByRole('heading', { name: /Mejora visual/ })).toBeTruthy()
    expect(screen.getByRole('region', { name: /Comparación antes y después/ })).toBeTruthy()
    expect(screen.getByText(/game-icons.net \(CC BY 3\.0/)).toBeTruthy()
    expect(screen.getByRole('region', { name: /Catálogo de iconos/ })).toBeTruthy()
  })

  it('las tarjetas nuevas usan imágenes SVG y las antiguas iniciales', () => {
    const { container } = render(<VisualAssetsPrototype />)
    expect(container.querySelectorAll('.visualproto__thumb--new img').length).toBe(6)
    expect(container.querySelectorAll('.visualproto__thumb--old').length).toBe(6)
    expect(container.querySelectorAll('.visualproto__rail-item img').length).toBe(5)
  })
})
