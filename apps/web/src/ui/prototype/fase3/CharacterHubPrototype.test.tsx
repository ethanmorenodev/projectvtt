import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { CharacterHubPrototype } from './CharacterHubPrototype'

/**
 * Prototipo del Character Hub (PMB-3.6): ventana tipo menú de RPG con
 * Perfil/Equipo/Relaciones/Historia, privacidad del DM visible en
 * revisión y variantes vacio/extremo.
 */

afterEach(cleanup)

describe('Prototipo Character Hub (PMB-3.6)', () => {
  it('cambia entre las cuatro secciones con su contenido', () => {
    render(<CharacterHubPrototype />)
    expect(screen.getByText('Elenar')).toBeTruthy()
    expect(screen.getByText('Puntos de golpe: 9/9')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Equipo' }))
    expect(screen.getByText('Arma principal')).toBeTruthy()
    expect(screen.getByText('Rapier')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Relaciones' }))
    expect(screen.getByText(/Gremio de ladrones/)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Historia' }))
    expect(screen.getByText(/sello del puerto/)).toBeTruthy()
  })

  it('el lore del DM se marca Solo DM y su previsualización es reversible', () => {
    render(<CharacterHubPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Historia' }))
    expect(screen.getByText('Solo DM')).toBeTruthy()

    const toggle = screen.getByRole('checkbox')
    fireEvent.click(toggle)
    expect(screen.getByText(/Oculta: el jugador no la vería/)).toBeTruthy()
  })

  it('variantes vacio y extremo para revisión', () => {
    const { unmount } = render(<CharacterHubPrototype variant="vacio" />)
    expect(screen.getByText('Sin recursos registrados.')).toBeTruthy()
    unmount()

    render(<CharacterHubPrototype variant="extremo" />)
    fireEvent.click(screen.getByRole('button', { name: 'Relaciones' }))
    expect(
      screen.getAllByText(/conexión larga para probar desbordes/).length,
    ).toBe(4)
  })
})
