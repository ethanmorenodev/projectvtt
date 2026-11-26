import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MapToolsPrototype } from './MapToolsPrototype'

afterEach(cleanup)
beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    x: 0, y: 0, left: 0, top: 0, right: 400, bottom: 240,
    width: 400, height: 240, toJSON: () => ({}),
  } as DOMRect)
})

describe('Prototipo herramientas de mapa (PMB-7.5)', () => {
  it('muestra herramientas, capas y anotaciones de muestra', () => {
    render(<MapToolsPrototype />)
    expect(screen.getByRole('button', { name: 'Medir' }).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Capas' })).toBeTruthy()
    expect(screen.getAllByTitle(/Entrada norte|Trampa/).length).toBe(2)
    expect(screen.getByText(/Niebla activa/)).toBeTruthy()
  })

  it('mide distancia entre dos puntos del mapa', () => {
    render(<MapToolsPrototype />)
    const canvas = screen.getByRole('region', { name: 'Mapa' })
      .querySelector('.maptools__canvas') as HTMLElement
    fireEvent.click(canvas, { clientX: 10, clientY: 10 })
    fireEvent.click(canvas, { clientX: 110, clientY: 10 })
    expect(screen.getByRole('status').textContent).toContain('Distancia:')
    expect(screen.getByText(/cuadrados · .* ft/)).toBeTruthy()
  })

  it('oculta una capa y permite dibujar', () => {
    render(<MapToolsPrototype />)
    fireEvent.click(screen.getByRole('button', { name: /Ocultar Niebla de guerra/ }))
    expect(screen.queryByText(/Niebla activa/)).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Dibujar' }))
    expect(screen.getByRole('button', { name: 'Dibujar' }).getAttribute('aria-pressed')).toBe('true')
    const canvas = screen.getByRole('region', { name: 'Mapa' })
      .querySelector('.maptools__canvas') as HTMLElement
    fireEvent.click(canvas, { clientX: 40, clientY: 40 })
    expect(screen.getByRole('status').textContent).toContain('Trazo:')
  })

  it('muestra estado vacío honesto', () => {
    render(<MapToolsPrototype variant="vacio" />)
    expect(screen.queryByTitle('Entrada norte')).toBeNull()
    expect(screen.getByText(/0 anotación\(es\)/)).toBeTruthy()
  })
})
