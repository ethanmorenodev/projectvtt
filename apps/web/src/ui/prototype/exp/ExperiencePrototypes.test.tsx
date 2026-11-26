import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { CreatorVisualPrototype } from './CreatorVisualPrototype'
import { InventoryVisualPrototype } from './InventoryVisualPrototype'

afterEach(cleanup)

describe('Prototipo EXP-1 — creación visual', () => {
  it('muestra elecciones en tarjetas y la imagen del personaje siempre visible', () => {
    render(<CreatorVisualPrototype />)
    expect(screen.getByRole('heading', { name: /¿De dónde viene tu personaje/ })).toBeTruthy()
    expect(screen.getByRole('complementary', { name: 'Imagen del personaje' })).toBeTruthy()
    expect(screen.getByText('Elfo')).toBeTruthy()
    expect(screen.getByText('Recomendado')).toBeTruthy()
  })

  it('seleccionar una raza actualiza el resumen y desplegar explica qué aporta', () => {
    render(<CreatorVisualPrototype />)
    fireEvent.click(screen.getByText('Enano'))
    expect(screen.getByText(/Enano · Resistente y tenaz/)).toBeTruthy()
    fireEvent.click(screen.getAllByText('¿Qué aporta?')[1])
    expect(screen.getByText(/Bonificación a Constitución/)).toBeTruthy()
    expect(screen.getByText(/Velocidad 30 ft/)).toBeTruthy()
  })

  it('el encuadre de la imagen es reversible y avisa sin perder trabajo', () => {
    render(<CreatorVisualPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Ajustar encuadre' }))
    expect(document.querySelector('.exp__crop-grid')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Listo' }))
    expect(document.querySelector('.exp__crop-grid')).toBeNull()
  })
})

describe('Prototipo EXP-2 — equipo visual', () => {
  it('muestra inventario, imagen central y huecos de equipo', () => {
    render(<InventoryVisualPrototype />)
    expect(screen.getByRole('region', { name: 'Objetos del inventario' })).toBeTruthy()
    expect(screen.getByRole('region', { name: 'Personaje' })).toBeTruthy()
    expect(screen.getByRole('complementary', { name: 'Objetos equipados' })).toBeTruthy()
  })

  it('equipar por botón actualiza los efectos y el hueco', () => {
    render(<InventoryVisualPrototype />)
    fireEvent.click(screen.getByText('Cota de malla'))
    fireEvent.click(screen.getByRole('button', { name: 'Equipar' }))
    expect(screen.getByText('Cota de malla equipado en Armadura.')).toBeTruthy()
    expect(screen.getByText('16')).toBeTruthy()
  })

  it('comparar muestra el objeto actual y el seleccionado', () => {
    render(<InventoryVisualPrototype />)
    fireEvent.click(screen.getByText('Espada larga'))
    fireEvent.click(screen.getByRole('button', { name: 'Comparar' }))
    expect(screen.getByText('Equipado ahora')).toBeTruthy()
    expect(screen.getByText('Este objeto')).toBeTruthy()
  })
})
