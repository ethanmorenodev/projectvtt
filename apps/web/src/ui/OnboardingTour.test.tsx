import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { OnboardingTour, TOUR_STORAGE_KEY } from './OnboardingTour'

/** Mock simple de localStorage: suficiente para comprobar la bandera. */
function stubLocalStorage() {
  const data = new Map<string, string>()
  const store = {
    getItem: vi.fn((key: string) => data.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => {
      data.set(key, value)
    }),
    removeItem: vi.fn((key: string) => {
      data.delete(key)
    }),
    clear: vi.fn(() => data.clear()),
    key: vi.fn((index: number) => [...data.keys()][index] ?? null),
    get length() {
      return data.size
    },
  }
  vi.stubGlobal('localStorage', store)
  return { store, data }
}

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('Recorrido guiado de primer ingreso', () => {
  it('muestra el primer paso en un diálogo accesible', () => {
    stubLocalStorage()
    render(<OnboardingTour />)

    const dialog = screen.getByRole('dialog', { name: 'Recorrido guiado' })
    expect(dialog.getAttribute('aria-modal')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Tus campañas' })).toBeTruthy()
    expect(screen.getByText(/Aquí se listan tus campañas/)).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Anterior' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('avanza por los cinco pasos y ofrece Empezar al final', () => {
    stubLocalStorage()
    render(<OnboardingTour />)

    const titles = [
      'Tus campañas',
      'Dentro de la campaña',
      'La mesa de juego',
      'Tu biblioteca',
      'Tus personajes',
    ]
    for (let index = 1; index < titles.length; index += 1) {
      fireEvent.click(screen.getByRole('button', { name: 'Siguiente' }))
      expect(screen.getByRole('heading', { name: titles[index] })).toBeTruthy()
    }
    expect(screen.getByText('5 / 5')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Empezar' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Siguiente' })).toBeNull()
  })

  it('Omitir marca la bandera y cierra el recorrido', () => {
    const { store } = stubLocalStorage()
    render(<OnboardingTour />)

    fireEvent.click(screen.getByRole('button', { name: 'Omitir' }))
    expect(store.setItem).toHaveBeenCalledWith(TOUR_STORAGE_KEY, '1')
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('no se muestra si la bandera ya está puesta', () => {
    const { data } = stubLocalStorage()
    data.set(TOUR_STORAGE_KEY, '1')
    render(<OnboardingTour />)

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('no rompe la app si localStorage no está disponible', () => {
    vi.stubGlobal('localStorage', {
      getItem: () => {
        throw new Error('almacenamiento bloqueado')
      },
      setItem: () => {
        throw new Error('almacenamiento bloqueado')
      },
    })
    render(<OnboardingTour />)

    expect(screen.getByRole('dialog')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Omitir' }))
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})
