import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ReplaceAssetPrototype } from './ReplaceAssetPrototype'

afterEach(cleanup)

describe('Prototipo sustitución de recursos (PMB-4.10)', () => {
  it('filtra por paquete y licencia', () => {
    render(<ReplaceAssetPrototype />)
    expect(screen.getAllByRole('button', { name: /\.png/ })).toHaveLength(3)

    fireEvent.click(within(screen.getByRole('group', { name: 'Paquete' }))
      .getByRole('button', { name: 'dnd5e-srd-5.1' }))
    expect(screen.getAllByRole('button', { name: /\.png/ })).toHaveLength(2)

    fireEvent.click(within(screen.getByRole('group', { name: 'Licencia' }))
      .getByRole('button', { name: 'CC0' }))
    expect(screen.getByText('No hay recursos con estos filtros.')).toBeTruthy()
  })

  it('sustituye el archivo conservando imageId y referencias', () => {
    render(<ReplaceAssetPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Sustituir archivo' }))
    const dialog = screen.getByRole('dialog', { name: 'Sustituir archivo' })
    fireEvent.change(within(dialog).getByRole('textbox'), {
      target: { value: 'nuevo-enano.png' },
    })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Sustituir' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.getByRole('status').textContent).toContain('img-dwarf inmutable')
    expect(screen.getByRole('status').textContent).toContain('3 referencias intactas')
    expect(screen.getAllByText('nuevo-enano.png').length).toBeGreaterThan(0)
  })

  it('muestra estado vacío honesto', () => {
    render(<ReplaceAssetPrototype variant="vacio" />)
    expect(screen.getByText('No hay recursos con estos filtros.')).toBeTruthy()
  })
})
