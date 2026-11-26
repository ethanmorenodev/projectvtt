import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

/**
 * PMB-2.9: el manejo de la sesión caducada es global — un 401 autenticado
 * avisa con toast y devuelve al login (patrón único de COMPONENT_RULES §9).
 */

afterEach(() => {
  cleanup()
  window.localStorage.clear()
  window.history.replaceState(null, '', '/')
})

beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      // El logout y el resto de llamadas responden ok; el 401 se dispara
      // manualmente desde el test vía el evento global.
      if (url.includes('/auth/me')) {
        return new Response(JSON.stringify({ id: 'u1', userName: 'ana' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        })
      }
      return new Response(JSON.stringify([]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
})

describe('Sesión caducada (PMB-2.9)', () => {
  it('el evento global avisa con toast y vuelve al login', async () => {
    render(<App />)
    await screen.findByText('Aún no hay campañas')

    // Lo que dispara el cliente API ante un 401 autenticado.
    window.dispatchEvent(new Event('pv:session-expired'))

    expect(await screen.findByText('Tu sesión ha caducado')).toBeTruthy()
    await waitFor(() => {
      expect(screen.getByText('Iniciar sesión')).toBeTruthy()
    })
    // La ruta queda en /login para re-entrar.
    expect(window.location.pathname).toBe('/login')
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar aviso' }))
  })
})
