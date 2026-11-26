import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { InventoryPrototype } from './InventoryPrototype'

afterEach(cleanup)

describe('Prototipo inventario visual (PMB-6.3)', () => {
  it('muestra la silueta con huecos ocupados y la mochila', () => {
    render(<InventoryPrototype />)
    expect(screen.getByRole('button', { name: 'Arma principal: Longsword' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Botas: Boots of Elvenkind' })).toBeTruthy()
    expect(screen.getByText('Mochila · 3 objeto(s)')).toBeTruthy()
  })

  it('equipa un objeto de la mochila en un hueco libre', () => {
    render(<InventoryPrototype />)
    fireEvent.click(screen.getByRole('button', { name: /Potion of Healing/ }))
    fireEvent.change(screen.getByRole('combobox', { name: 'Hueco' }), {
      target: { value: 'helmet' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Equipar' }))

    expect(screen.getByRole('status').textContent).toContain('equipado en Casco')
    expect(screen.getByRole('button', { name: 'Casco: Potion of Healing' })).toBeTruthy()
    expect(screen.getByText('Mochila · 2 objeto(s)')).toBeTruthy()
  })

  it('no sobrescribe un hueco ocupado', () => {
    render(<InventoryPrototype />)
    fireEvent.click(screen.getByRole('button', { name: /Potion of Healing/ }))
    fireEvent.change(screen.getByRole('combobox', { name: 'Hueco' }), {
      target: { value: 'armor' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Equipar' }))
    expect(screen.getByRole('status').textContent).toContain('ya está ocupado')
    expect(screen.getByRole('button', { name: 'Armadura: Chain Mail' })).toBeTruthy()
  })

  it('desequipa un objeto y lo devuelve a la mochila', () => {
    render(<InventoryPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Arma principal: Longsword' }))
    fireEvent.click(screen.getByRole('button', { name: 'Desequipar' }))
    expect(screen.getByRole('status').textContent).toContain('devuelto a la mochila')
    expect(screen.getByText('Mochila · 4 objeto(s)')).toBeTruthy()
  })

  it('muestra estado vacío honesto', () => {
    render(<InventoryPrototype variant="vacio" />)
    expect(screen.getByText('La mochila está vacía.')).toBeTruthy()
    expect(within(screen.getByRole('region', { name: 'Silueta de equipamiento' }))
      .getAllByText(/Arma principal|Casco/).length).toBeGreaterThan(0)
    expect(screen.getByText('Selecciona un objeto.')).toBeTruthy()
  })
})
