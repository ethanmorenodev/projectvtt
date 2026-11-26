import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import type { ComponentProps } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { CampaignDetail, ContentItemDto } from '../api/types'
import { ToastProvider } from '../ui'
import { CampaignSpaceView } from './CampaignSpaceView'

/**
 * PMB-2.7: el Espacio de campaña integra el prototipo aprobado con datos
 * reales (escenas, personajes, participantes) y reserva las secciones sin
 * respaldo en el sistema (sin contenido provisional).
 */

afterEach(cleanup)

const campaign: CampaignDetail = {
  id: 'c1',
  name: 'La caída de Scaldar',
  role: 'Owner',
  createdAt: new Date().toISOString(),
  archivedAt: null,
  memberCount: 2,
  description: 'Terror gótico los viernes.',
  hasCover: false,
}

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function contentItem(overrides: Partial<ContentItemDto> & {
  id: string
  name: string
  typeKey: string
  scope: string
}): ContentItemDto {
  const now = new Date().toISOString()
  return {
    system: 'dnd5e',
    typeId: 't1',
    summary: null,
    description: null,
    parentId: null,
    tags: [],
    imageId: null,
    focal: null,
    properties: {},
    inheritance: {},
    campaignId: null,
    workflowState: 'published',
    currentRevision: 1,
    createdByUserId: null,
    createdAt: now,
    updatedAt: now,
    packKey: null,
    packItemKey: null,
    isPrivate: false,
    privateFields: [],
    usageCount: 0,
    ...overrides,
  }
}

function Space(props: ComponentProps<typeof CampaignSpaceView>) {
  return (
    <ToastProvider>
      <CampaignSpaceView {...props} />
    </ToastProvider>
  )
}

function renderSpace(current = campaign) {
  return render(
    <Space
      campaign={current}
      onOpenLore={() => undefined}
      onOpenScene={() => undefined}
      onCreateCharacter={() => undefined}
      onOpenDraft={() => undefined}
      onChanged={() => undefined}
    />,
  )
}

beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/campaigns/c1/scenes')) {
        return jsonResponse([
          { id: 's1', campaignId: 'c1', name: 'Puerto de Scaldar', gridCellSize: 32, gridShape: 'square', createdAt: new Date().toISOString(), tokenCount: 1 },
        ])
      }
      if (url.includes('/api/v1/scenes/')) {
        return jsonResponse({ id: 's1', campaignId: 'c1', name: 'Puerto de Scaldar', gridCellSize: 32, gridShape: 'square', createdAt: new Date().toISOString() })
      }
      if (url.includes('/actors')) {
        return jsonResponse([
          { id: 'a1', campaignId: 'c1', name: 'Elenar', rulesetKey: 'dnd5e', attributes: {}, resources: {}, version: 1, createdByUserId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() },
        ])
      }
      if (url.includes('/members')) {
        return jsonResponse([
          { userId: 'u1', userName: 'ana', role: 'Owner', joinedAt: new Date().toISOString() },
        ])
      }
      if (url.includes('/activity')) {
        return jsonResponse([])
      }
      if (url.includes('/api/v1/content/items')) {
        return jsonResponse([])
      }
      return jsonResponse({ title: 'No encontrado' }, 404)
    }),
  )
})

describe('Espacio de campaña (PMB-2.7)', () => {
  it('muestra el resumen con datos reales y cambia de sección', async () => {
    render(
      <Space
        campaign={campaign}
        onOpenLore={() => undefined}
        onOpenScene={() => undefined}
        onCreateCharacter={() => undefined}
        onOpenDraft={() => undefined}
        onChanged={() => undefined}
      />,
    )
    // Resumen por defecto con conteos reales.
    await screen.findByText('1 escena')
    expect(screen.getByText('1 personaje')).toBeTruthy()
    expect(screen.getByText('2 jugadores')).toBeTruthy()
    // La campaña es el heading del subnavegador (deep-link de App).
    expect(screen.getByRole('heading', { name: 'La caída de Scaldar' })).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Participantes y permisos' }))
    await screen.findByText('ana')
    expect(screen.getByText('Propietario')).toBeTruthy()
  })

  it('busca personajes por nombre y tipo (EXP-30)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('/actors')) {
          return jsonResponse([
            { id: 'a1', campaignId: 'c1', name: 'Elenar', kind: 'pc', rulesetKey: 'dnd5e', attributes: {}, resources: {}, version: 1, createdByUserId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() },
            { id: 'a2', campaignId: 'c1', name: 'Grom', kind: 'npc', rulesetKey: 'dnd5e', attributes: {}, resources: {}, version: 1, createdByUserId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() },
          ])
        }
        if (url.includes('/members')) return jsonResponse([])
        if (url.includes('/activity')) return jsonResponse([])
        return jsonResponse([])
      }),
    )
    render(
      <Space
        campaign={campaign}
        onOpenLore={() => undefined}
        onOpenScene={() => undefined}
        onCreateCharacter={() => undefined}
        onOpenDraft={() => undefined}
        onChanged={() => undefined}
      />,
    )
    fireEvent.click(await screen.findByRole('button', { name: 'Personajes' }))
    const search = await screen.findByLabelText('Buscar personajes por nombre o tipo')
    fireEvent.change(search, { target: { value: 'npc' } })
    await screen.findByText('Grom')
    expect(screen.queryByText('Elenar')).toBeNull()
    fireEvent.change(search, { target: { value: 'zzz' } })
    await screen.findByText('Sin personajes para «zzz»')
  })

  it('muestra el catálogo vacío en Criaturas en lugar de «próximamente» (EXP-34)', async () => {
    renderSpace()
    fireEvent.click(await screen.findByRole('button', { name: 'Criaturas' }))
    expect(await screen.findByText('Sin criaturas disponibles')).toBeTruthy()
    expect(screen.getByText(
      'Las criaturas de la biblioteca aparecerán aquí cuando estén disponibles.',
    )).toBeTruthy()
    expect(screen.queryByText('Criaturas: próximamente')).toBeNull()
  })

  it('mantiene facciones y localizaciones como «próximamente»', async () => {
    renderSpace()
    fireEvent.click(await screen.findByRole('button', { name: 'Facciones' }))
    expect(await screen.findByText('Facciones: próximamente')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Localizaciones' }))
    expect(await screen.findByText('Localizaciones: próximamente')).toBeTruthy()
  })

  it('filtra los catálogos reales y reutiliza el inspector (EXP-34)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('/actors')) {
          return jsonResponse([
            { id: 'a1', campaignId: 'c1', name: 'Elenar', kind: 'pc', rulesetKey: 'dnd5e', attributes: {}, resources: {}, version: 1, createdByUserId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() },
            { id: 'a2', campaignId: 'c1', name: 'Grom', kind: 'npc', rulesetKey: 'dnd5e', attributes: {}, resources: {}, version: 1, createdByUserId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() },
          ])
        }
        if (url.includes('/api/v1/content/items')) {
          return jsonResponse([
            contentItem({ id: 'i1', name: 'Dragón rojo', typeKey: 'creature', scope: 'global', summary: 'Bestia imponente' }),
            contentItem({ id: 'i2', name: 'Carta de Scaldar', typeKey: 'item', scope: 'campaign', campaignId: 'c1' }),
            contentItem({ id: 'i3', name: 'Regla casera', typeKey: 'ability', scope: 'user' }),
            contentItem({ id: 'i4', name: 'Foráneo', typeKey: 'item', scope: 'campaign', campaignId: 'c2' }),
          ])
        }
        return jsonResponse([])
      }),
    )
    renderSpace()
    const mainArea = within(document.querySelector('.space__main') as HTMLElement)

    fireEvent.click(await screen.findByRole('button', { name: 'NPC' }))
    expect(await mainArea.findByText('Grom')).toBeTruthy()
    expect(mainArea.queryByText('Elenar')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Criaturas' }))
    expect(await mainArea.findByText('Dragón rojo')).toBeTruthy()
    expect(mainArea.queryByText('Carta de Scaldar')).toBeNull()
    fireEvent.click(mainArea.getByRole('button', { name: /Dragón rojo/ }))
    expect(await screen.findByRole('heading', { name: 'Dragón rojo' })).toBeTruthy()
    const inspector = within(document.querySelector('.space__inspector') as HTMLElement)
    expect(inspector.getByText('Bestia imponente')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Biblioteca de campaña' }))
    expect(await mainArea.findByText('Carta de Scaldar')).toBeTruthy()
    expect(mainArea.queryByText('Foráneo')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Contenido homebrew' }))
    expect(await mainArea.findByText('Regla casera')).toBeTruthy()
    expect(mainArea.queryByText('Carta de Scaldar')).toBeNull()
  })

  it('las secciones de lore ya implementadas enlazan con la vista real', async () => {
    const opened: string[] = []
    render(
      <Space
        campaign={campaign}
        onOpenLore={() => opened.push('lore')}
        onOpenScene={() => undefined}
        onCreateCharacter={() => undefined}
        onOpenDraft={() => undefined}
        onChanged={() => undefined}
      />,
    )
    fireEvent.click(await screen.findByRole('button', { name: 'Historia y lore' }))
    expect(screen.getByText(/Relaciones, cronología y secretos del DM/)).toBeTruthy()
    expect(screen.queryByText(/llegan en la Fase 7/)).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Abrir lore del mundo' }))
    expect(opened).toEqual(['lore'])
  })

  it('la mesa lista las escenas reales para entrar', async () => {
    const opened: string[] = []
    render(
      <Space
        campaign={campaign}
        onOpenLore={() => undefined}
        onOpenScene={(scene) => opened.push(scene.id)}
        onCreateCharacter={() => undefined}
        onOpenDraft={() => undefined}
        onChanged={() => undefined}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Mesa de juego' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Entrar en Puerto de Scaldar' }))
    await waitFor(() => expect(opened).toContain('s1'))
  })

  it('muestra «Eliminar escena» solo para quien puede gestionar', async () => {
    renderSpace()
    fireEvent.click(await screen.findByRole('button', { name: 'Mesa de juego' }))
    expect(await screen.findByRole('button', { name: 'Eliminar escena' })).toBeTruthy()
    cleanup()

    renderSpace({ ...campaign, role: 'Player' })
    fireEvent.click(await screen.findByRole('button', { name: 'Mesa de juego' }))
    await screen.findByRole('button', { name: 'Entrar en Puerto de Scaldar' })
    expect(screen.queryByRole('button', { name: 'Eliminar escena' })).toBeNull()
  })

  it('confirma la eliminación de la escena con DELETE y recarga la lista', async () => {
    const baseFetch = vi.mocked(fetch)
    let deleted = false
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'DELETE' && url === '/api/v1/scenes/s1') {
        deleted = true
        return jsonResponse({ deleted: true })
      }
      if (url.includes('/campaigns/c1/scenes')) {
        return jsonResponse(deleted ? [] : [
          { id: 's1', campaignId: 'c1', name: 'Puerto de Scaldar', gridCellSize: 32, gridShape: 'square', createdAt: new Date().toISOString(), tokenCount: 1 },
        ])
      }
      return baseFetch(input, init)
    }))
    renderSpace()
    fireEvent.click(await screen.findByRole('button', { name: 'Mesa de juego' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Eliminar escena' }))

    const dialog = screen.getByRole('dialog', { name: '¿Eliminar la escena?' })
    expect(within(dialog).getByText(/Puerto de Scaldar/)).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Eliminar escena' }))

    await waitFor(() => expect(deleted).toBe(true))
    expect(await screen.findByText('Escena eliminada.')).toBeTruthy()
    await waitFor(() => expect(screen.queryByText('Puerto de Scaldar')).toBeNull())
  })
})
