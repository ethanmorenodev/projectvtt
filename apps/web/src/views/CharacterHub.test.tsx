import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ActorDto, ActorEffectDto, ActorItemDto } from '../api/types'
import { CharacterHub } from './CharacterHub'

/**
 * Character Hub (PMB-3.7): ventana sincronizada con la ficha clásica
 * (misma selección y datos), secciones con datos reales y lore «Solo DM»
 * que no se renderiza para jugadores.
 */

afterEach(cleanup)

const actor: ActorDto = {
  id: 'a1',
  campaignId: 'c1',
  name: 'Elenar',
  rulesetKey: 'dnd5e',
  attributes: {
    strength: 17,
    dexterity: 14,
    constitution: 16,
    intelligence: 10,
    wisdom: 12,
    charisma: 8,
  },
  resources: { hitPoints: { current: 9, max: 9 } },
  version: 1,
  createdByUserId: 'u1',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  kind: 'pc',
  portraitId: null,
  biography: 'Nació en Puerto de Scaldar.',
  personality: 'Desconfiado pero leal.',
  level: 1,
  systemData: null,
  build: null,
}

const items: ActorItemDto[] = [
  {
    id: 'i1',
    actorId: 'a1',
    name: 'Thieves’ tools',
    quantity: 1,
    version: 1,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    slotId: null,
    contentItemId: null,
    weight: 1,
    volume: null,
  },
]

const effects: ActorEffectDto[] = [
  { id: 'e1', actorId: 'a1', name: 'Bendecido', createdAt: new Date().toISOString() },
]

beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/usages') || url.includes('/content/items')) {
        return new Response(JSON.stringify([]), {
          status: 200, headers: { 'Content-Type': 'application/json' },
        })
      }
      if (url.includes('/history')) {
        return new Response(
          JSON.stringify([
            {
              id: 'h1',
              campaignId: 'c1',
              userId: 'u1',
              userName: 'ana',
              kind: 'actor.created',
              summary: "Ficha 'Elenar' creada",
              createdAt: new Date().toISOString(),
            },
          ]),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        )
      }
      return new Response(JSON.stringify({ title: 'No encontrado' }), {
        status: 404,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
})

function renderHub(overrides: Partial<Parameters<typeof CharacterHub>[0]> = {}) {
  const props: Parameters<typeof CharacterHub>[0] = {
    campaignId: 'c1',
    canManage: true,
    actors: [actor],
    selectedActorId: 'a1',
    items: [],
    effects: [],
    onSelectActor: () => undefined,
    ...overrides,
  }
  return render(<CharacterHub {...props} />)
}

describe('Character Hub (PMB-3.7)', () => {
  it('perfil muestra identidad, atributos con modificadores y estado', () => {
    renderHub()
    expect(screen.getByRole('heading', { name: 'Elenar' })).toBeTruthy()
    expect(screen.getByText(/Nivel 1/)).toBeTruthy()
    // Fue 17 y Con 16: ambos +3.
    expect(screen.getAllByText('+3').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('Puntos de golpe: 9/9')).toBeTruthy()
    expect(screen.getByText('Desconfiado pero leal.')).toBeTruthy()
  })

  it('equipo muestra inventario y efectos reales; relaciones carga desde la API', async () => {
    renderHub({ items, effects })
    fireEvent.click(screen.getByRole('button', { name: 'Equipo' }))
    expect(screen.getByText(/Thieves’ tools/)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Relaciones' }))
    expect(await screen.findByText(/Sin relaciones registradas/)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Perfil' }))
    expect(screen.getByText('Bendecido')).toBeTruthy()
  })

  it('relaciones muestra los vínculos del contenido usado por el personaje', async () => {
    const fetchMock = vi.mocked(globalThis.fetch)
    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/usages')) {
        return new Response(JSON.stringify([{ itemId: 'item-1' }]), {
          status: 200, headers: { 'Content-Type': 'application/json' },
        })
      }
      if (url.includes('/content/items')) {
        const body = url.includes('/item-1/relations')
          ? [{
            id: 'rel-1', fromItemId: 'item-1', toItemId: 'item-2',
            kind: 'requires', label: 'Requiere', metadata: null,
          }]
          : [
            { id: 'item-1', name: 'Dwarf' },
            { id: 'item-2', name: 'Battleaxe' },
          ]
        return new Response(JSON.stringify(body), {
          status: 200, headers: { 'Content-Type': 'application/json' },
        })
      }
      return new Response(JSON.stringify([]), {
        status: 200, headers: { 'Content-Type': 'application/json' },
      })
    })

    renderHub()
    fireEvent.click(screen.getByRole('button', { name: 'Relaciones' }))
    expect(await screen.findByText(/Battleaxe/)).toBeTruthy()
    expect(screen.getByText(/requiere/)).toBeTruthy()
  })

  it('el lore Solo DM solo se renderiza para el DM', async () => {
    renderHub({ canManage: true })
    fireEvent.click(screen.getByRole('button', { name: 'Historia' }))
    expect(screen.getByText('Solo DM')).toBeTruthy()
    expect(screen.getByText(/solo verán el DM y los propietarios/)).toBeTruthy()
    cleanup()

    renderHub({ canManage: false })
    fireEvent.click(screen.getByRole('button', { name: 'Historia' }))
    expect(screen.queryByText('Solo DM')).toBeNull()
  })

  it('el selector de personaje sincroniza la selección con la ficha', () => {
    const selected: string[] = []
    renderHub({
      actors: [actor, { ...actor, id: 'a2', name: 'Brom' }],
      onSelectActor: (id) => selected.push(id ?? ''),
    })
    fireEvent.change(screen.getByRole('combobox', { name: 'Personaje' }), {
      target: { value: 'a2' },
    })
    expect(selected).toEqual(['a2'])
  })
})
