import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { HerramientasView } from './HerramientasView'

const navigate = vi.fn()
vi.mock('../routing/router', () => ({
  useNavigate: () => navigate,
}))

afterEach(() => {
  cleanup()
  navigate.mockClear()
})

describe('Índice de herramientas (Fase A del rediseño)', () => {
  it('muestra los cuatro grupos y abre las rutas existentes', () => {
    render(<HerramientasView />)

    expect(screen.getByRole('heading', { name: 'Dirección de juego' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Recursos' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Personajes' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Configuración' })).toBeTruthy()

    const dm = screen.getByText('Flujo del DM').closest('article')!
    fireEvent.click(within(dm).getByRole('button', { name: /Abrir/ }))
    expect(navigate).toHaveBeenCalledWith({ kind: 'dm' })

    const imagenes = screen.getByText('Biblioteca de imágenes').closest('article')!
    fireEvent.click(within(imagenes).getByRole('button', { name: /Abrir/ }))
    expect(navigate).toHaveBeenCalledWith({ kind: 'imagenes' })
  })

  it('usa el contexto de campaña para el creador y la gestión', () => {
    render(<HerramientasView campaignId="c-1" />)

    const creador = screen.getByText('Creador de personajes').closest('article')!
    fireEvent.click(within(creador).getByRole('button', { name: /Abrir/ }))
    expect(navigate).toHaveBeenCalledWith({ kind: 'character-create', campaignId: 'c-1' })

    const gestion = screen.getByText('Gestión de campaña').closest('article')!
    fireEvent.click(within(gestion).getByRole('button', { name: /Abrir/ }))
    expect(navigate).toHaveBeenCalledWith({ kind: 'campaign', campaignId: 'c-1' })
  })
})
