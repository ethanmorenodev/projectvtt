import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { MesaPrototype } from './MesaPrototype'

afterEach(() => {
  cleanup()
  window.localStorage.clear()
})

describe('Prototipo Mesa de juego (PMB-2.8)', () => {
  it('muestra las cuatro zonas de la distribución', () => {
    render(<MesaPrototype />)
    expect(screen.getByText('Puerto de Scaldar')).toBeTruthy()
    expect(screen.getByText('Inspector')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Añadir token' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Chat' })).toBeTruthy()
  })

  it('el inspector muestra la selección al pulsar un token', () => {
    render(<MesaPrototype />)
    // «Elenar» aparece en su token y en el inspector (selección inicial).
    expect(screen.getAllByText('Elenar').length).toBeGreaterThan(0)
    fireEvent.click(screen.getByRole('button', { name: 'Token Guardia del puerto' }))
    expect(screen.getAllByText('Guardia del puerto').length).toBeGreaterThan(0)
    expect(screen.getByText(/movido por ana/)).toBeTruthy()
  })

  it('las variantes vacía y extrema cubren estados y nombres largos', () => {
    const { unmount } = render(<MesaPrototype variant="vacio" />)
    expect(screen.getByText('Escena sin tokens')).toBeTruthy()
    expect(screen.getByText('Nada seleccionado')).toBeTruthy()
    unmount()

    render(<MesaPrototype variant="extremo" />)
    expect(
      screen.getAllByRole('button', { name: 'Token Guardia de élite de la ciudadela de Scaldar con armadura ceremonial' }).length,
    ).toBeGreaterThan(0)
    expect(screen.getAllByRole('button', { name: /^Token / }).length).toBeGreaterThan(10)
  })
})
