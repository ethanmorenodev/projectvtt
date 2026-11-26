import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup } from '@testing-library/react'
import { CommandPalette } from './CommandPalette'
import type { PaletteGroup } from './CommandPalette'

afterEach(cleanup)

function groups(onRun: (id: string) => void): PaletteGroup[] {
  return [
    {
      id: 'acciones',
      title: 'Acciones',
      items: [
        { id: 'inicio', label: 'Ir a Inicio', onRun: () => onRun('inicio') },
        { id: 'biblioteca', label: 'Ir a Biblioteca', hint: 'Fase 4', onRun: () => onRun('biblioteca') },
      ],
    },
    {
      id: 'campanas',
      title: 'Campañas',
      items: [{ id: 'camp-1', label: 'La caída de Scaldar', onRun: () => onRun('camp-1') }],
    },
  ]
}

function renderPalette(onRun: (id: string) => void, onClose = vi.fn()) {
  render(
    <CommandPalette
      open
      placeholder="Buscar…"
      emptyText="Sin resultados"
      groups={groups(onRun)}
      onOpen={vi.fn()}
      onClose={onClose}
    />,
  )
  return onClose
}

describe('CommandPalette', () => {
  it('filtra resultados con la consulta', () => {
    renderPalette(vi.fn())
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'biblio' } })
    expect(screen.getAllByRole('option')).toHaveLength(1)
    expect(screen.getByRole('option', { name: /Biblioteca/ })).toBeTruthy()
  })

  it('Enter ejecuta el elemento activo y cierra', () => {
    const onRun = vi.fn()
    const onClose = renderPalette(onRun)
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'caída' } })
    fireEvent.keyDown(screen.getByRole('combobox'), { key: 'Enter' })
    expect(onRun).toHaveBeenCalledWith('camp-1')
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('las flechas cambian el elemento activo', () => {
    renderPalette(vi.fn())
    const input = screen.getByRole('combobox')
    fireEvent.keyDown(input, { key: 'ArrowDown' })
    expect(screen.getByRole('option', { name: /Biblioteca/ }).getAttribute('aria-selected')).toBe('true')
    fireEvent.keyDown(input, { key: 'ArrowUp' })
    expect(screen.getByRole('option', { name: /Ir a Inicio/ }).getAttribute('aria-selected')).toBe('true')
  })

  it('un clic en un resultado lo ejecuta y muestra el estado vacío', () => {
    const onRun = vi.fn()
    renderPalette(onRun)
    fireEvent.click(screen.getByRole('option', { name: /La caída/ }))
    expect(onRun).toHaveBeenCalledWith('camp-1')
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'zzz' } })
    expect(screen.getByText('Sin resultados')).toBeTruthy()
  })
})
