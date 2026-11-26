import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { EspacioCampanaPrototype } from './EspacioCampanaPrototype'

afterEach(cleanup)

describe('Prototipo Espacio de campaña (PMB-2.6)', () => {
  it('muestra la subnavegación y el resumen', () => {
    render(<EspacioCampanaPrototype />)
    expect(screen.getByRole('heading', { name: 'Resumen' })).toBeTruthy()
    expect(screen.getByText('Personajes')).toBeTruthy()
    expect(screen.getByText('Participantes y permisos')).toBeTruthy()
    expect(screen.getByText('escenas')).toBeTruthy()
  })

  it('cambia de sección al pulsar la subnavegación', () => {
    render(<EspacioCampanaPrototype />)
    fireEvent.click(screen.getByText('Escenas y mapas'))
    expect(screen.getByRole('heading', { name: 'Escenas y mapas' })).toBeTruthy()
    expect(screen.getByText('Puerto de Scaldar')).toBeTruthy()
    fireEvent.click(screen.getByText('Personajes'))
    expect(screen.getByText('Elenar')).toBeTruthy()
  })

  it('las variantes vacía y extrema cubren estados y nombres largos', () => {
    const { unmount } = render(<EspacioCampanaPrototype variant="vacio" />)
    expect(screen.getByText('Campaña vacía')).toBeTruthy()
    unmount()

    render(<EspacioCampanaPrototype variant="extremo" />)
    fireEvent.click(screen.getByText('Escenas y mapas'))
    expect(
      screen.getByText('La batalla final en las murallas de la ciudadela de Scaldar y el asedio de la guardia nocturna'),
    ).toBeTruthy()
  })
})
