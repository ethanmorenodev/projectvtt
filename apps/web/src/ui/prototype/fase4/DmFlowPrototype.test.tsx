import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { DmFlowPrototype } from './DmFlowPrototype'

afterEach(cleanup)

describe('Prototipo flujo del DM (PMB-4.8)', () => {
  it('muestra el diff y aprueba una propuesta como DM', () => {
    render(<DmFlowPrototype />)
    expect(screen.getByText('Bandeja del DM · 2 pendiente(s)')).toBeTruthy()

    fireEvent.click(screen.getAllByRole('button', { name: 'Ver cambios' })[0])
    expect(screen.getByText('velocidad: 25 → 30')).toBeTruthy()

    fireEvent.click(screen.getAllByRole('button', { name: 'Aprobar y publicar' })[0])
    expect(screen.getByText('Bandeja del DM · 1 pendiente(s)')).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('revisión 2 publicada')
  })

  it('el autor propone un borrador y el DM puede rechazarlo', () => {
    render(<DmFlowPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Vista autor' }))
    fireEvent.click(screen.getByRole('button', { name: 'Proponer' }))
    expect(screen.getByRole('status').textContent).toContain('enviada a revisión')

    fireEvent.click(screen.getByRole('button', { name: 'Vista DM' }))
    expect(screen.getByText('Bandeja del DM · 3 pendiente(s)')).toBeTruthy()
    fireEvent.click(screen.getAllByRole('button', { name: 'Rechazar' })[2])
    expect(screen.getByText('Bandeja del DM · 2 pendiente(s)')).toBeTruthy()
  })

  it('añade y quita permisos explícitos', () => {
    render(<DmFlowPrototype />)
    const panel = screen.getByRole('region', { name: 'Permisos del contenido' })
    fireEvent.change(within(panel).getByRole('textbox', { name: 'Destinatario' }),
      { target: { value: 'Nuevo Jugador' } })
    fireEvent.click(within(panel).getByRole('button', { name: 'Añadir permiso' }))
    expect(within(panel).getAllByText('Nuevo Jugador')).toHaveLength(1)

    fireEvent.click(within(panel).getAllByRole('button', { name: 'Quitar' })[0])
    expect(within(panel).queryByText('Kaelen Piedra Alta')).toBeNull()
  })

  it('muestra estado vacío honesto', () => {
    render(<DmFlowPrototype variant="vacio" />)
    expect(screen.getByText('No hay propuestas pendientes de revisión.')).toBeTruthy()
  })
})
