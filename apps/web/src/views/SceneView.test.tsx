import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CampaignRealtime } from '../realtime/hub'
import type {
  ActorDto,
  CampaignDetail,
  CampaignMember,
  ChatMessageDto,
  CombatStateDto,
  FogStateDto,
  RollDto,
  SceneDetail,
} from '../api/types'
import { SceneView } from './SceneView'

/** Carga parcial de la mesa (bug de partida): un 500 en una de las seis
 *  peticiones no debe dejar el chat ni las tiradas vacíos. */

const NOW = '2026-11-26T10:00:00Z'

const campaign: CampaignDetail = {
  id: 'c1',
  name: 'La caída de Scaldar',
  role: 'Owner',
  createdAt: NOW,
  archivedAt: null,
  memberCount: 1,
  description: null,
  hasCover: false,
}

const scene: SceneDetail = {
  id: 's1',
  campaignId: 'c1',
  name: 'Puerto de Scaldar',
  gridCellSize: 32,
  gridShape: 'square',
  createdAt: NOW,
  hasCover: false,
}

const actor: ActorDto = {
  id: 'a1', campaignId: 'c1', name: 'Elenar', rulesetKey: 'dnd5e',
  attributes: {}, resources: {}, version: 1, createdByUserId: 'u1',
  createdAt: NOW, updatedAt: NOW, kind: 'pc', portraitId: null,
  biography: null, personality: null, level: 1, systemData: null, build: null,
}

const roll: RollDto = {
  rollId: 'r1', campaignId: 'c1', actorId: 'a1', attribute: 'strength',
  breakdown: {
    rulesetKey: 'dnd5e', kind: 'check', attribute: 'strength', attributeValue: 14,
    die: 20, modifier: 2, extra: 0, effectModifier: 0, dice: [12],
    rollMode: 'normal', effects: [], total: 14, dc: null, success: null,
    formula: '1d20+2',
  },
  rolledByUserId: 'u1', occurredAt: NOW,
}

const message: ChatMessageDto = {
  id: 'm1', campaignId: 'c1', userId: 'u1', userName: 'ana',
  body: 'Hola mesa', createdAt: NOW,
}

const member: CampaignMember = {
  userId: 'u1', userName: 'ana', role: 'Owner', joinedAt: NOW,
}

const combat: CombatStateDto = {
  sceneId: 's1', active: false, round: 0, currentCombatantId: null, combatants: [],
}

const fog: FogStateDto = { sceneId: 's1', gridCellSize: 32, revealed: [] }

type HubHandlers = Record<string, (event: unknown) => void>

/** Los handlers se leen de la propia llamada al mock (sin canales globales). */
function hubHandler(name: string): ((event: unknown) => void) | undefined {
  const handlers = vi.mocked(CampaignRealtime.create).mock.calls[0]?.[0] as
    | HubHandlers
    | undefined
  return handlers?.[name]
}

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'u1', userName: 'ana' },
    loading: false,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  }),
}))

vi.mock('../pixi/SceneStage', () => ({
  SceneStage: {
    create: vi.fn(async () => ({
      destroy: vi.fn(),
      setGrid: vi.fn(),
      syncTokens: vi.fn(),
      setFog: vi.fn(),
      setFogEditing: vi.fn(),
      resizeToContainer: vi.fn(),
      centerWorld: () => ({ x: 0, y: 0 }),
      setMap: vi.fn(async () => true),
    })),
  },
}))

vi.mock('../realtime/hub', () => ({
  CampaignRealtime: {
    create: vi.fn(() => {
      return {
        start: vi.fn(async () => undefined),
        joinCampaign: vi.fn(async () => undefined),
        syncScene: vi.fn(async () => ({
          sceneId: 's1', campaignId: 'c1', sceneName: scene.name,
          gridCellSize: 32, gridShape: 'square', version: 1, tokens: [],
        })),
        stop: vi.fn(async () => undefined),
      }
    }),
  },
}))

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function problemResponse(detail: string): Response {
  return jsonResponse({ title: 'Error', detail }, 500)
}

let failPaths = new Set<string>()
let gatedPaths = new Map<string, Promise<Response>>()

beforeEach(() => {
  failPaths = new Set<string>()
  gatedPaths = new Map<string, Promise<Response>>()
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    if (url.includes('/campaigns/c1/actors')) return jsonResponse([actor])
    if (url.includes('/campaigns/c1/rolls')) return jsonResponse([roll])
    if (url.includes('/campaigns/c1/messages')) {
      const gate = gatedPaths.get('messages')
      if (gate) return gate
      return failPaths.has('messages')
        ? problemResponse('Fallo de mensajes')
        : jsonResponse([message])
    }
    if (url.includes('/campaigns/c1/members')) return jsonResponse([member])
    if (url.includes('/scenes/s1/combat')) {
      const gate = gatedPaths.get('combat')
      if (gate) return gate
      return failPaths.has('combat')
        ? problemResponse('Fallo de combate')
        : jsonResponse(combat)
    }
    if (url.includes('/scenes/s1/fog')) return jsonResponse(fog)
    if (url.includes('/annotations')) {
      return jsonResponse({ sceneId: 's1', version: 1, items: [] })
    }
    if (url.includes('/scenes/s1/map')) return jsonResponse(null, 404)
    if (url.includes('/campaigns/c1/scenes')) return jsonResponse([])
    if (url.includes('/content/items')) return jsonResponse([])
    if (url.includes('/history')) return jsonResponse([])
    return jsonResponse([])
  }))
})

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  vi.unstubAllGlobals()
})

function renderTable() {
  return render(<SceneView campaign={campaign} scene={scene} onBack={() => undefined} />)
}

describe('Carga parcial de la mesa', () => {
  it('carga mensajes y tiradas aunque falle /combat', async () => {
    failPaths.add('combat')

    renderTable()

    await waitFor(() => expect(screen.getAllByTestId('chat-message').length).toBe(1))
    expect(screen.getByText('Hola mesa')).toBeTruthy()
    fireEvent.click(screen.getByRole('tab', { name: 'Ficha' }))
    await waitFor(() => expect(screen.getAllByTestId('roll-entry').length).toBe(1))
    expect(screen.getByTestId('load-error').textContent).toContain('combate')
    expect(screen.getByTestId('retry-load')).toBeTruthy()
    expect(screen.queryByTestId('table-loading')).toBeNull()
  })

  it('muestra el reintento si falla /messages y carga los mensajes al reintentar', async () => {
    failPaths.add('messages')

    renderTable()

    await waitFor(() => expect(screen.getByTestId('load-error')).toBeTruthy())
    expect(screen.getByTestId('load-error').textContent).toContain('mensajes')
    expect(screen.queryByTestId('chat-message')).toBeNull()

    failPaths.delete('messages')
    fireEvent.click(screen.getByTestId('retry-load'))

    await waitFor(() => expect(screen.getAllByTestId('chat-message').length).toBe(1))
    await waitFor(() => expect(screen.queryByTestId('load-error')).toBeNull())
  })

  it('conserva un mensaje en vivo llegado antes de la respuesta HTTP', async () => {
    let releaseMessages: (response: Response) => void = () => undefined
    gatedPaths.set('messages', new Promise<Response>((resolve) => {
      releaseMessages = resolve
    }))

    renderTable()

    await waitFor(() => expect(typeof hubHandler('onMessagePosted')).toBe('function'))
    const liveMessage: ChatMessageDto = {
      id: 'm-live', campaignId: 'c1', userId: 'u2', userName: 'bruno',
      body: 'Mensaje en vivo', createdAt: NOW,
    }
    act(() => {
      hubHandler('onMessagePosted')?.({
        eventId: 'e1', campaignId: 'c1', actorUserId: 'u2', occurredAt: NOW,
        schemaVersion: 1, message: liveMessage,
      })
    })
    await waitFor(() => expect(screen.getAllByTestId('chat-message').length).toBe(1))

    releaseMessages(jsonResponse([message]))
    await waitFor(() => expect(screen.getAllByTestId('chat-message').length).toBe(2))
    expect(screen.getByText('Mensaje en vivo')).toBeTruthy()
    expect(screen.getByText('Hola mesa')).toBeTruthy()
  })

  it('conserva un combate en vivo recibido antes del reintento', async () => {
    failPaths.add('combat')

    renderTable()

    await waitFor(() => expect(screen.getByTestId('retry-load')).toBeTruthy())
    await waitFor(() => expect(typeof hubHandler('onCombatUpdated')).toBe('function'))
    act(() => {
      hubHandler('onCombatUpdated')?.({
        eventId: 'e3', campaignId: 'c1', actorUserId: 'u1', occurredAt: NOW,
        schemaVersion: 1, combat: { ...combat, active: true, round: 7 },
      })
    })

    failPaths.delete('combat')
    fireEvent.click(screen.getByTestId('retry-load'))
    fireEvent.click(screen.getByRole('tab', { name: 'Combate' }))

    // La respuesta del reintento trae la ronda 1 (más antigua): no debe ganar.
    await waitFor(() => expect(screen.getByText('Ronda 7')).toBeTruthy())
    expect(screen.queryByText('Ronda 1')).toBeNull()
  })

  it('no sobrescribe un combate en vivo con la respuesta HTTP posterior', async () => {
    let releaseCombat: (response: Response) => void = () => undefined
    gatedPaths.set('combat', new Promise<Response>((resolve) => {
      releaseCombat = resolve
    }))

    renderTable()

    await waitFor(() => expect(typeof hubHandler('onCombatUpdated')).toBe('function'))
    act(() => {
      hubHandler('onCombatUpdated')?.({
        eventId: 'e2', campaignId: 'c1', actorUserId: 'u1', occurredAt: NOW,
        schemaVersion: 1, combat: { ...combat, active: true, round: 7 },
      })
    })
    releaseCombat(jsonResponse({ ...combat, round: 1 }))

    fireEvent.click(screen.getByRole('tab', { name: 'Combate' }))
    await waitFor(() => expect(screen.getByText('Ronda 7')).toBeTruthy())
  })
})
