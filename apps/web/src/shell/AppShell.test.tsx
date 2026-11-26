import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AppShell } from './AppShell'

afterEach(cleanup)

function renderShell(onNavigate = vi.fn()) {
  const result = render(
    <AppShell
      active="inicio"
      breadcrumb="Inicio"
      userName="ana"
      onNavigate={onNavigate}
      onSignOut={() => undefined}
      palette={{ placeholder: 'Buscar', emptyText: 'Sin resultados', groups: [] }}
    >
      <main>Contenido</main>
    </AppShell>,
  )
  return { ...result, onNavigate }
}

describe('Shell — raíl acoplado', () => {
  it('muestra la navegación siempre visible, sin cápsula ni pista flotante', () => {
    renderShell()
    const nav = screen.getByRole('navigation', { name: 'Navegación principal' })
    expect(nav).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Inicio' }).getAttribute('aria-current')).toBe('page')
    expect(screen.queryByText(/Pasa por/)).toBeNull()
    expect(document.querySelector('.app-shell__edge')).toBeNull()
  })

  it('cambia de ruta al pulsar un destino del raíl', () => {
    const { onNavigate } = renderShell()
    screen.getByRole('button', { name: 'Biblioteca' }).click()
    expect(onNavigate).toHaveBeenCalledWith({ kind: 'biblioteca' })
  })
})
