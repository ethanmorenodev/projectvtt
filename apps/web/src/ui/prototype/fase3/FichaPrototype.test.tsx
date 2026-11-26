import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { FichaPrototype } from './FichaPrototype'

/**
 * Prototipo del rediseño de la Ficha clásica: misma función (crear,
 * editar atributos/PG, inventario, efectos, tiradas) con el lenguaje
 * visual del Hub. Modo lectura/edición y variantes de revisión.
 */

afterEach(cleanup)

describe('Prototipo Ficha clásica (barrido de paneles)', () => {
  it('recompone las secciones de la hoja y alterna edición', () => {
    render(<FichaPrototype />)
    expect(screen.getByRole('heading', { name: 'Elenar' })).toBeTruthy()
    expect(screen.getByText('Puntos de golpe: 9/9')).toBeTruthy()
    expect(screen.getByText('Herramientas de ladrón')).toBeTruthy()
    expect(screen.getByText('Ataque con daga')).toBeTruthy()

    // Editar muestra entradas para atributos y PG.
    fireEvent.click(screen.getByRole('button', { name: 'Editar ficha' }))
    expect(screen.getByRole('spinbutton', { name: 'Fue' })).toBeTruthy()
    expect(screen.getByLabelText('Puntos de vida actuales')).toBeTruthy()
    // El mismo botón pasa a "Guardar".
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeTruthy()
  })

  it('variante vacio: alta de personaje y estado honesto', () => {
    render(<FichaPrototype variant="vacio" />)
    expect(screen.getByPlaceholderText('Nombre')).toBeTruthy()
    expect(screen.getByText('Aún no hay actores en esta campaña.')).toBeTruthy()
  })

  it('variante extremo: nombres largos y lista densa sin romperse', () => {
    render(<FichaPrototype variant="extremo" />)
    expect(
      screen.getByRole('heading', { name: /Guardia de élite de la ciudadela/ }),
    ).toBeTruthy()
    // 9 ítems (3 grupos repetidos) con el nombre largo.
    expect(screen.getAllByText(/de la guardia nocturna del puerto/).length).toBe(9)
  })
})
