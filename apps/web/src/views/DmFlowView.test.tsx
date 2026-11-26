import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DmFlowView } from './DmFlowView'

afterEach(cleanup)
beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
})

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const proposals = [
  {
    id: 'p-1', name: 'Guardia de Scaldar', authorName: 'Mira Colmillo',
    workflowState: 'proposed', currentRevision: 1,
    changes: ['velocidad: 25 → 30', '+ rasgo «Veterano»'],
    updatedAt: new Date().toISOString(),
  },
  {
    id: 'p-2', name: 'Ritual de la corona', authorName: 'Kaelen Piedra Alta',
    workflowState: 'proposed', currentRevision: 2,
    changes: ['+ requisito: nivel 3'], updatedAt: new Date().toISOString(),
  },
]

const grants = [
  { id: 'g-1', itemId: 'p-1', subjectKind: 'role', subjectId: 'GM', permission: 'view' },
]

function stubFetch() {
  const calls: string[] = []
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    calls.push(`${method} ${url}`)
    if (url.includes('/proposals')) return jsonResponse(proposals)
    if (url.includes('/grants') && method === 'GET') return jsonResponse(grants)
    if (url.includes('/grants') && method === 'POST') {
      return jsonResponse({
        id: 'g-2', itemId: 'p-1', subjectKind: 'user', subjectId: 'Nuevo', permission: 'edit',
      }, 201)
    }
    if (url.includes('/grants') && method === 'DELETE') {
      return new Response(null, { status: 204 })
    }
    if (url.includes('/approve') || url.includes('/reject')) {
      return jsonResponse({ workflowState: 'published', currentRevision: 3 })
    }
    return jsonResponse({})
  }))
  return { calls }
}

describe('Flujo del DM real (PMB-4.8)', () => {
  it('lista propuestas, muestra el diff y aprueba con confirmación', async () => {
    const { calls } = stubFetch()
    render(<DmFlowView />)
    expect(await screen.findByText('Bandeja del DM · 2 pendiente(s)')).toBeTruthy()

    fireEvent.click(screen.getAllByRole('button', { name: 'Ver cambios' })[0])
    expect(screen.getByText('velocidad: 25 → 30')).toBeTruthy()

    fireEvent.click(screen.getAllByRole('button', { name: 'Aprobar y publicar' })[0])
    const dialog = screen.getByRole('dialog', { name: 'Aprobar y publicar' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Aprobar y publicar' }))
    expect(calls.some((call) => call.startsWith('POST ') && call.includes('/approve'))).toBe(true)
    await waitFor(() => expect(screen.getByRole('status').textContent)
      .toContain('revisión 3 publicada'))
  })

  it('rechaza una propuesta y avisa al autor', async () => {
    stubFetch()
    render(<DmFlowView />)
    await screen.findByText('Bandeja del DM · 2 pendiente(s)')
    fireEvent.click(screen.getAllByRole('button', { name: 'Rechazar' })[0])
    const dialog = screen.getByRole('dialog', { name: 'Rechazar' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Rechazar' }))
    await waitFor(() => expect(screen.getByRole('status').textContent)
      .toContain('devuelta a borrador'))
  })

  it('gestiona permisos de la propuesta seleccionada', async () => {
    const { calls } = stubFetch()
    render(<DmFlowView />)
    await screen.findByText('Bandeja del DM · 2 pendiente(s)')
    fireEvent.click(screen.getByRole('button', { name: 'Guardia de Scaldar' }))

    const panel = screen.getByRole('region', { name: 'Permisos explícitos' })
    expect(await within(panel).findByText('GM')).toBeTruthy()
    fireEvent.change(within(panel).getByRole('textbox', { name: 'Usuario o rol' }),
      { target: { value: 'Nuevo' } })
    fireEvent.click(within(panel).getByRole('button', { name: 'Añadir permiso' }))
    await waitFor(() => expect(calls.some((call) =>
      call.startsWith('POST ') && call.includes('/grants'))).toBe(true))

    fireEvent.click(within(panel).getByRole('button', { name: 'Quitar' }))
    expect(calls.some((call) => call.startsWith('DELETE ') && call.includes('/grants'))).toBe(true)
  })
})
