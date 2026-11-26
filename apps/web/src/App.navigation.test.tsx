import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

/**
 * Criterio de PMB-1.4: la navegación por áreas usa URLs reales y el
 * botón "atrás" del navegador la restaura; un deep-link hidrata sus datos.
 */

afterEach(() => {
  cleanup()
  window.localStorage.clear()
  window.history.replaceState(null, '', '/')
})

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

beforeEach(() => {
  // La sesión se restaura desde localStorage (AuthContext): sin token no
  // se llama a /auth/me y la app muestra el login.
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/auth/me')) {
        return jsonResponse({ id: 'u1', userName: 'ana' })
      }
      if (url.includes('/api/v1/campaigns/abc-1')) {
        return jsonResponse({
          id: 'abc-1',
          name: 'La caída de Scaldar',
          role: 'Owner',
          createdAt: new Date().toISOString(),
          archivedAt: null,
          memberCount: 2,
        })
      }
      if (url.includes('/api/v1/campaigns')) {
        return jsonResponse([])
      }
      return jsonResponse({ title: 'No encontrado', detail: 'no existe' }, 404)
    }),
  )
})

describe('App — navegación con URLs reales', () => {
  it('el raíl navega por URL y el botón atrás restaura el área', async () => {
    render(<App />)
    await screen.findByText('Aún no hay campañas')

    fireEvent.click(screen.getByRole('button', { name: /Inicio/ }))
    await screen.findByText('Continuar jugando')
    expect(window.location.pathname).toBe('/inicio')

    window.history.back()
    await waitFor(() => expect(window.location.pathname).not.toBe('/inicio'))
    await screen.findByText('Aún no hay campañas')
    expect(window.location.pathname).toBe('/')
  })

  it('el raíl agrupado abre Herramientas y muestra las migas de pan', async () => {
    render(<App />)
    await screen.findByText('Aún no hay campañas')

    fireEvent.click(screen.getByRole('button', { name: 'Herramientas' }))
    expect(await screen.findByRole('heading', { name: 'Herramientas' })).toBeTruthy()
    expect(window.location.pathname).toBe('/herramientas')
    expect(screen.getByText('Herramientas', { selector: '.app-shell__crumb span' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Herramientas' }).getAttribute('aria-current')).toBe('page')
  })

  it('un deep-link de campaña hidrata sus datos', async () => {
    window.history.replaceState(null, '', '/campaigns/abc-1')
    render(<App />)
    // El nombre va partido con el badge de rol: se busca por rol/regex.
    await screen.findByRole('heading', { name: /La caída de Scaldar/ })
    expect(window.location.pathname).toBe('/campaigns/abc-1')
    // El espacio de campaña ya no está en el raíl global: permanece activo
    // «Campañas» y el espacio se alcanza desde la propia campaña.
    expect(screen.getByRole('button', { name: 'Campañas' }).getAttribute('aria-current')).toBe('page')
  })
})
