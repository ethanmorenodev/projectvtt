import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { SceneAnnotationsState } from '../api/types'
import { setAnnotations } from '../api/annotations'
import { MapToolsView } from './MapToolsView'

vi.mock('../api/annotations', () => ({
  fetchAnnotations: vi.fn(async (sceneId: string) => ({ sceneId, items: [], version: 0 })),
  setAnnotations: vi.fn(async (sceneId: string) => ({ sceneId, items: [], version: 1 })),
}))

const annotations: SceneAnnotationsState = {
  sceneId: 'scene-1',
  version: 2,
  items: [
    {
      id: 'a1', sceneId: 'scene-1', authorUserId: 'gm', kind: 'draw',
      points: [{ x: 1, y: 1 }, { x: 2, y: 2 }], text: null,
      version: 1, createdAt: '2026-11-25T00:00:00Z', updatedAt: '2026-11-25T00:00:00Z',
    },
    {
      id: 'a2', sceneId: 'scene-1', authorUserId: 'gm', kind: 'note',
      points: [{ x: 3, y: 3 }], text: 'Punto de interés',
      version: 2, createdAt: '2026-11-25T00:00:00Z', updatedAt: '2026-11-25T00:00:00Z',
    },
  ],
}

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  vi.unstubAllGlobals()
})

beforeEach(() => {
  vi.stubGlobal('crypto', { randomUUID: () => 'cmd-1' })
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    x: 0, y: 0, left: 0, top: 0, right: 400, bottom: 240,
    width: 400, height: 240, toJSON: () => ({}),
  } as DOMRect)
})

describe('Anotaciones compartidas en herramientas de mapa (ADR-043)', () => {
  it('dibuja trazos y notas recibidos del servidor', () => {
    render(<MapToolsView annotations={annotations} canWrite />)

    expect(document.querySelectorAll('.maptools__annotation--draw').length).toBe(1)
    expect(document.querySelector('.maptools__annotation--draw')?.getAttribute('points'))
      .toBe('1,1 2,2')
    expect(screen.getByText('Punto de interés')).toBeTruthy()
  })

  it('oculta compartir y limpiar cuando no se puede escribir', () => {
    render(<MapToolsView annotations={annotations} canWrite={false} />)

    expect(screen.queryByRole('button', { name: 'Compartir dibujo' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Compartir nota' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Limpiar anotaciones' })).toBeNull()
  })

  it('envía comandos con la versión actual y un commandId nuevo', async () => {
    render(<MapToolsView annotations={annotations} canWrite />)

    fireEvent.click(screen.getByRole('button', { name: 'Dibujar' }))
    const canvas = document.querySelector('.maptools__canvas') as HTMLElement
    fireEvent.click(canvas, { clientX: 20, clientY: 20 })
    fireEvent.click(canvas, { clientX: 120, clientY: 20 })
    fireEvent.click(screen.getByRole('button', { name: 'Compartir dibujo' }))

    await waitFor(() => expect(setAnnotations).toHaveBeenCalledTimes(1))
    expect(setAnnotations).toHaveBeenCalledWith('scene-1', {
      commandId: 'cmd-1',
      expectedVersion: 2,
      kind: 'draw',
      points: [{ x: 0.5, y: 0.5 }, { x: 3.5, y: 0.5 }],
      text: null,
      clear: false,
    })

    fireEvent.click(screen.getByRole('button', { name: 'Limpiar anotaciones' }))
    await waitFor(() => expect(setAnnotations).toHaveBeenCalledTimes(2))
    expect(setAnnotations).toHaveBeenLastCalledWith('scene-1', {
      commandId: 'cmd-1',
      expectedVersion: 2,
      kind: 'draw',
      clear: true,
    })
  })
})
