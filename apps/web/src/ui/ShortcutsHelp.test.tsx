import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ShortcutsHelp } from './ShortcutsHelp'

afterEach(cleanup)

describe('Ayuda de atajos de teclado', () => {
  it('muestra los atajos de la mesa solo cuando se indica', () => {
    const { rerender } = render(<ShortcutsHelp open onClose={vi.fn()} />)
    expect(screen.getByRole('heading', { name: 'General' })).toBeTruthy()
    expect(screen.queryByText('Mesa de juego')).toBeNull()

    rerender(<ShortcutsHelp open onClose={vi.fn()} showMesa />)
    expect(screen.getByText('Mesa de juego')).toBeTruthy()
    expect(screen.getByText('T')).toBeTruthy()
    expect(screen.getByText('Alternar niebla')).toBeTruthy()
  })

  it('cierra con Escape', () => {
    const onClose = vi.fn()
    render(<ShortcutsHelp open onClose={onClose} />)
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).toHaveBeenCalled()
  })
})
