import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LoreView } from './LoreView'

afterEach(cleanup)
beforeEach(() => window.localStorage.setItem('projectvtt.token', 'token-de-prueba'))

function jsonResponse(data: unknown): Response {
  return new Response(JSON.stringify(data), {
    status: 200, headers: { 'Content-Type': 'application/json' },
  })
}

const graph = {
  nodes: [
    { id: 'n1', name: 'La Corona Rota', typeKey: 'faction', summary: 'Facción real', isPrivate: false },
    { id: 'n2', name: 'Scaldar', typeKey: 'location', summary: 'Ciudad', isPrivate: false },
    { id: 'n3', name: 'Sociedad Secreta', typeKey: 'faction', summary: null, isPrivate: true },
  ],
  edges: [
    { id: 'e1', fromItemId: 'n1', kind: 'locatedIn', toItemId: 'n2', label: 'sede' },
  ],
}

const events = [
  { id: 'ev1', name: 'Primer hecho', summary: null, happenedAt: '1492-01-01', order: 1 },
]

function stubFetch() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    if (url.includes('/lore/graph')) return jsonResponse(graph)
    if (url.includes('/lore/timeline')) return jsonResponse(events)
    return jsonResponse({})
  }))
}

describe('Lore del mundo (PMB-7.3)', () => {
  it('muestra nodos visibles, secretos del DM y cronología', async () => {
    stubFetch()
    render(<LoreView />)
    expect(await screen.findByText('La Corona Rota')).toBeTruthy()
    expect(screen.getByText('Sociedad Secreta')).toBeTruthy()
    expect(screen.getAllByText('DM').length).toBeGreaterThan(0)
    expect(screen.getByText('Primer hecho')).toBeTruthy()
    expect(screen.getByText('3 elemento(s)')).toBeTruthy()
    expect(screen.getByText('1 evento(s)')).toBeTruthy()
  })

  it('muestra las relaciones del nodo seleccionado', async () => {
    stubFetch()
    render(<LoreView />)
    fireEvent.click(await screen.findByRole('button', { name: /La Corona Rota/ }))
    expect(screen.getByText('locatedIn')).toBeTruthy()
    expect(screen.getAllByText(/Scaldar/).length).toBeGreaterThan(0)
  })
})
