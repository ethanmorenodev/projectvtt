import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MapToolsView } from './MapToolsView'

afterEach(cleanup)
beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    x: 0, y: 0, left: 0, top: 0, right: 400, bottom: 240,
    width: 400, height: 240, toJSON: () => ({}),
  } as DOMRect)
})

describe('Herramientas de mapa en la mesa (PMB-7.5)', () => {
  it('muestra herramientas, capas y medición a cero', () => {
    render(<MapToolsView />)
    expect(screen.getByRole('button', { name: 'Medir' }).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Capas' })).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('0 cuadrados')
  })

  it('mide entre dos clics y permite ocultar una capa', () => {
    render(<MapToolsView />)
    const canvas = document.querySelector('.maptools__canvas') as HTMLElement
    fireEvent.click(canvas, { clientX: 20, clientY: 20 })
    fireEvent.click(canvas, { clientX: 120, clientY: 20 })
    expect(screen.getByRole('status').textContent).not.toContain('0 cuadrados')

    fireEvent.click(screen.getByRole('button', { name: /Ocultar Niebla/ }))
    expect(screen.getByRole('button', { name: /Mostrar Niebla/ })).toBeTruthy()
  })
})
