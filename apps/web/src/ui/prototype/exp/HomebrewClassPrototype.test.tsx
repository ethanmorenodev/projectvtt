import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { HomebrewClassPrototype } from './HomebrewClassPrototype'

afterEach(cleanup)

describe('Prototipo EXP-3 — creador de clases homebrew', () => {
  it('permite empezar desde cero o duplicar una clase, sin JSON', () => {
    render(<HomebrewClassPrototype />)
    expect(screen.getByRole('button', { name: /Ninguna — desde cero/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Guerrero/ })).toBeTruthy()
    expect(screen.queryByText(/json/i)).toBeNull()
  })

  it('distingue efectos automáticos de resolución manual', () => {
    render(<HomebrewClassPrototype />)
    fireEvent.click(screen.getByRole('button', { name: /Habilidades/ }))
    expect(screen.getAllByText('Automático').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Resolución manual').length).toBeGreaterThan(0)
  })

  it('guarda borrador y publica con ámbitos elegidos', () => {
    render(<HomebrewClassPrototype />)
    fireEvent.click(screen.getByRole('button', { name: 'Guardar borrador' }))
    expect(screen.getByText('Borrador guardado ✓')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /Revisar y publicar/ }))
    expect(screen.getByText(/no modifica a los personajes que ya usan la clase/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Publicar clase' }))
    expect(screen.getByText('Clase publicada ✓')).toBeTruthy()
  })
})
