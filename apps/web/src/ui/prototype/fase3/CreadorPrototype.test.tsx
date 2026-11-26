import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { CreadorPrototype } from './CreadorPrototype'

/**
 * Prototipo del creador de personajes (PMB-3.4): mapa de pasos aprobado,
 * efectos visibles por elección, atributos manual + compra de puntos y
 * variantes de revisión (vacio/extremo).
 */

afterEach(cleanup)

describe('Prototipo Creador de personajes (PMB-3.4)', () => {
  it('muestra el mapa de pasos y permite navegar con efectos visibles', () => {
    render(<CreadorPrototype />)
    expect(screen.getByText('Paso 1 de 10')).toBeTruthy()
    expect(screen.getByRole('button', { name: /Raza y variante/ })).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /Raza y variante/ }))
    expect(screen.getByText('Dwarf')).toBeTruthy()
    expect(screen.getAllByText(/Con \+2/).length).toBeGreaterThan(0)

    // Elegir el elfo cambia los efectos (y sus variantes).
    fireEvent.click(screen.getByRole('button', { name: /Elf/ }))
    expect(screen.getAllByText(/Dex \+2/).length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: 'Wood Elf' })).toBeTruthy()
  })

  it('compra de puntos: presupuesto, costes y modificadores', () => {
    render(<CreadorPrototype />)
    fireEvent.click(screen.getByRole('button', { name: /Atributos/ }))
    expect(screen.getByText(/Presupuesto/)).toBeTruthy()
    expect(screen.getAllByText('0 pts').length).toBe(6)

    fireEvent.click(screen.getByRole('button', { name: 'Sumar Strength' }))
    fireEvent.click(screen.getByRole('button', { name: 'Sumar Strength' }))
    // 8→10: el coste del valor actual (10) es 2 pts; el resto sigue a 0.
    expect(screen.getByText('2 pts')).toBeTruthy()

    // Cambiar a manual muestra entradas numéricas.
    fireEvent.click(screen.getByRole('button', { name: 'Manual' }))
    expect(screen.getAllByRole('spinbutton').length).toBe(6)
  })

  it('variantes vacio y extremo para revisión', () => {
    const { unmount } = render(<CreadorPrototype variant="vacio" />)
    fireEvent.click(screen.getByRole('button', { name: /Raza y variante/ }))
    expect(screen.getByText('Elige una raza para ver sus efectos.')).toBeTruthy()
    unmount()

    render(<CreadorPrototype variant="extremo" />)
    fireEvent.click(screen.getByRole('button', { name: /Revisión/ }))
    expect(
      screen.getByText(/Guardia de élite de la ciudadela de Scaldar/),
    ).toBeTruthy()
  })
})
