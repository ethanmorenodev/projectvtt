import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ImageAssetDto } from '../api/types'
import { ImageLibraryView } from './ImageLibraryView'

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

const images: ImageAssetDto[] = [
  {
    id: 'img-1',
    fileName: 'enano.png',
    contentType: 'image/png',
    sizeBytes: 100,
    role: 'miniatura',
    albums: ['Personajes'],
    tags: ['srd'],
    license: 'CC BY 3.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/3.0/',
    author: 'game-icons.net',
    origin: 'Paquete de iconos',
    attribution: ['Icono de enano (CC BY 3.0)'],
    focal: null,
    packKey: null,
    referenceCount: 0,
    variants: [],
    createdAt: new Date().toISOString(),
  },
  {
    id: 'img-2',
    fileName: 'fondo.jpg',
    contentType: 'image/jpeg',
    sizeBytes: 200,
    role: 'ilustracion',
    albums: ['Escenarios'],
    tags: ['fondo'],
    license: 'CC0',
    licenseUrl: null,
    author: 'Proyecto VTT',
    origin: 'Recurso propio',
    attribution: ['Fondo CC0'],
    focal: null,
    packKey: null,
    referenceCount: 0,
    variants: [],
    createdAt: new Date().toISOString(),
  },
]

function stubFetch() {
  const calls: string[] = []
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    calls.push(`${method} ${url}`)
    if (url.includes('/file')) return new Response(new Blob(['img']), { status: 200 })
    if (url.includes('/images') && method === 'POST') {
      return jsonResponse(images[0], 201)
    }
    if (url.includes('/images') && method === 'DELETE') {
      return new Response(null, { status: 204 })
    }
    if (url.includes('/images?')) {
      const query = url.slice(url.indexOf('?'))
      const role = new URLSearchParams(query).get('role')
      return jsonResponse(role ? images.filter((image) => image.role === role) : images)
    }
    if (url.includes('/images')) return jsonResponse(images)
    return jsonResponse({}, 404)
  }))
  return { calls }
}

describe('Biblioteca de imágenes real (PMB-4.7)', () => {
  it('carga imágenes, filtra por papel y muestra procedencia/licencia', async () => {
    stubFetch()
    render(<ImageLibraryView />)
    expect(await screen.findByText('2 imagen(es)')).toBeTruthy()

    fireEvent.click(within(screen.getByRole('group', { name: 'Papel' }))
      .getByRole('button', { name: 'Miniatura' }))
    expect(await screen.findByText('1 imagen(es)')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /enano\.png/ }))
    const inspector = screen.getByRole('complementary', { name: 'Detalle de imagen' })
    expect(within(inspector).getAllByText(/game-icons.net/).length).toBeGreaterThan(0)
    expect(within(inspector).getByText('CC BY 3.0')).toBeTruthy()
  })

  it('abre el lightbox bajo petición y elimina con confirmación', async () => {
    const { calls } = stubFetch()
    render(<ImageLibraryView />)
    await screen.findByText('2 imagen(es)')
    fireEvent.click(screen.getByRole('button', { name: /enano\.png/ }))

    fireEvent.click(await screen.findByRole('button', { name: 'Ampliar' }))
    expect(screen.getByRole('dialog', { name: 'Vista ampliada' })).toBeTruthy()
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Vista ampliada' }))
      .getByRole('button', { name: 'Cerrar' }))

    fireEvent.click(screen.getByRole('button', { name: 'Eliminar imagen' }))
    const dialog = screen.getByRole('dialog', { name: 'Eliminar imagen' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Eliminar imagen' }))
    await waitFor(() => expect(calls.some((call) => call.startsWith('DELETE '))).toBe(true))
  })

  it('sustituye el archivo conservando el imageId', async () => {
    const { calls } = stubFetch()
    render(<ImageLibraryView />)
    await screen.findByText('2 imagen(es)')
    fireEvent.click(screen.getByRole('button', { name: /enano\.png/ }))
    fireEvent.click(await screen.findByRole('button', { name: 'Sustituir archivo' }))

    const dialog = screen.getByRole('dialog', { name: 'Sustituir «enano.png»' })
    const input = within(dialog).getByLabelText('Nuevo archivo') as HTMLInputElement
    Object.defineProperty(input, 'files', {
      value: [new File(['img'], 'enano-v2.png', { type: 'image/png' })],
    })
    fireEvent.change(input)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Sustituir archivo' }))

    await waitFor(() => expect(calls.some((call) =>
      call.startsWith('POST ') && call.includes('/replace'))).toBe(true))
    expect(screen.getByRole('status').textContent).toContain('inmutable')
  })
})
