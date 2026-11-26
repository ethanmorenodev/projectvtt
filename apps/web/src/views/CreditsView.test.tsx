import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CreditsView } from './CreditsView'

afterEach(cleanup)
beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
})

function jsonResponse(data: unknown): Response {
  return new Response(JSON.stringify(data), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('Créditos y licencias reales (PMB-4.7)', () => {
  it('lista paquetes e imágenes y exporta la nota de atribución', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/credits/export')) {
        return new Response('Wizards of the Coast — CC BY 4.0\nIcono de enano — CC BY 3.0', {
          status: 200,
          headers: { 'Content-Type': 'text/plain' },
        })
      }
      if (url.includes('/content/packs')) {
        return jsonResponse([{
          id: 'p-1',
          key: 'dnd5e-srd-5.1',
          name: 'D&D 5e SRD 5.1',
          version: '5.1',
          system: 'dnd5e',
          sourceUrl: null,
          license: 'CC BY 4.0',
          licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
          attribution: ['Wizards of the Coast, SRD 5.1 (2016), CC BY 4.0.'],
          importedAt: new Date().toISOString(),
        }])
      }
      if (url.includes('/images')) {
        return jsonResponse([{
          id: 'img-1',
          fileName: 'enano.png',
          contentType: 'image/png',
          sizeBytes: 100,
          role: 'miniatura',
          albums: ['Personajes'],
          tags: ['srd'],
          license: 'CC BY 3.0',
          licenseUrl: null,
          author: 'game-icons.net',
          origin: 'Paquete de iconos',
          attribution: ['Icono de enano (CC BY 3.0)'],
          focal: null,
          packKey: null,
          referenceCount: 0,
          variants: [],
          createdAt: new Date().toISOString(),
        }])
      }
      return jsonResponse({})
    }))

    render(<CreditsView />)
    expect(await screen.findByText('D&D 5e SRD 5.1')).toBeTruthy()
    expect(await screen.findByText('enano.png')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Exportar nota de atribución' }))
    const note = await screen.findByRole('textbox', {
      name: 'Nota de atribución',
    }) as HTMLTextAreaElement
    expect(note.value).toContain('Wizards of the Coast')
    expect(note.value).toContain('CC BY 3.0')
  })
})
