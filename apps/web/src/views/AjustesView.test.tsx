import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

const audio = vi.hoisted(() => ({
  setAudioSettings: vi.fn(),
  play: vi.fn(),
}))

vi.mock('../audio/sound', () => ({
  getAudioSettings: () => ({ enabled: false, volume: 50 }),
  setAudioSettings: audio.setAudioSettings,
  play: audio.play,
}))

import { AjustesView } from './AjustesView'

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe('Pantalla de ajustes de sonido', () => {
  it('muestra los controles accesibles y el aviso de que la información también es visual', () => {
    render(<AjustesView />)

    expect(screen.getByRole('heading', { name: 'Ajustes de sonido' })).toBeTruthy()
    expect(screen.getByRole('switch', { name: 'Activar sonido' })).toBeTruthy()
    expect(screen.getByRole('slider', { name: 'Volumen' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Probar sonido' })).toBeTruthy()
    expect(screen.getByText(/La información también es visual/)).toBeTruthy()
  })

  it('actualiza el gestor al activar el sonido', () => {
    render(<AjustesView />)

    fireEvent.click(screen.getByRole('switch', { name: 'Activar sonido' }))

    expect(audio.setAudioSettings).toHaveBeenCalledWith({ enabled: true, volume: 50 })
    expect(screen.getByRole('switch', { name: 'Activar sonido' }).getAttribute('aria-checked'))
      .toBe('true')
  })

  it('actualiza el gestor al cambiar el volumen', () => {
    render(<AjustesView />)

    fireEvent.change(screen.getByRole('slider', { name: 'Volumen' }), { target: { value: '80' } })

    expect(audio.setAudioSettings).toHaveBeenCalledWith({ enabled: false, volume: 80 })
    expect(screen.getByText('80%')).toBeTruthy()
  })

  it('invoca el gestor al pulsar «Probar sonido»', () => {
    render(<AjustesView />)

    fireEvent.click(screen.getByRole('button', { name: 'Probar sonido' }))

    expect(audio.play).toHaveBeenCalledWith('confirm')
  })
})
