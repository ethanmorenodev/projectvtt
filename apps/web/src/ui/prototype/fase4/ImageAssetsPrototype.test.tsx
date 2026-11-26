import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { CreditsPrototype, ImageLibraryPrototype } from './ImageAssetsPrototype'

afterEach(cleanup)

describe('Prototipo biblioteca de imágenes (PMB-4.7)', () => {
  it('filtra por papel y licencia y muestra la procedencia en el inspector', () => {
    render(<ImageLibraryPrototype />)
    expect(screen.getByText('6 imagen(es)')).toBeTruthy()

    fireEvent.click(
      within(screen.getByRole('group', { name: 'Papel' }))
        .getByRole('button', { name: 'Miniatura' }))
    expect(screen.getByText('2 imagen(es)')).toBeTruthy()

    fireEvent.click(
      within(screen.getByRole('group', { name: 'Licencia' }))
        .getByRole('button', { name: 'CC0' }))
    expect(screen.getByText('0 imagen(es)')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    expect(screen.getByText('6 imagen(es)')).toBeTruthy()

    const inspector = screen.getByRole('complementary', { name: 'Detalle de imagen' })
    expect(within(inspector).getAllByText(/game-icons.net/).length).toBeGreaterThan(0)
    expect(within(inspector).getByText('CC BY 3.0')).toBeTruthy()
  })

  it('muestra la ilustración solo bajo petición y reutiliza por referencia', () => {
    render(<ImageLibraryPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Ampliar' }))
    const dialog = screen.getByRole('dialog', { name: 'Vista ampliada' })
    expect(within(dialog).getByText(/game-icons.net/)).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cerrar' }))
    expect(screen.queryByRole('dialog')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Usar en elemento' }))
    expect(screen.getByRole('status').textContent).toContain('referencia')
  })

  it('muestra un estado vacío honesto sin imágenes', () => {
    render(<ImageLibraryPrototype variant="vacio" />)
    expect(screen.getByText('No hay imágenes con estos filtros.')).toBeTruthy()
    expect(screen.getByText('0 imagen(es)')).toBeTruthy()
  })
})

describe('Prototipo créditos y licencias (PMB-4.7)', () => {
  it('lista paquetes e imágenes y exporta la nota de atribución', () => {
    render(<CreditsPrototype />)
    expect(screen.getAllByText(/CC BY 4.0/).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByRole('button', { name: 'Exportar nota de atribución' }))
    const note = screen.getByRole('textbox', {
      name: 'Nota de atribución',
    }) as HTMLTextAreaElement
    expect(note.value).toContain('Wizards of the Coast')
    expect(note.value).toContain('game-icons.net')
  })
})
