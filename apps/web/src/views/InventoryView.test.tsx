import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { navigate } from '../routing/router'
import type { ActorItemDto, EquipmentSlotDto } from '../api/types'
import { InventoryView } from './InventoryView'

afterEach(cleanup)
beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
  navigate('/inventario/actor-1')
})

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const slots: EquipmentSlotDto[] = [
  { id: 'main-hand', label: 'Arma principal', icon: null, accepts: ['item'], optional: true, multiple: false },
  { id: 'armor', label: 'Armadura', icon: null, accepts: ['item'], optional: true, multiple: false },
]

function item(overrides: Partial<ActorItemDto> & { id: string }): ActorItemDto {
  return {
    actorId: 'actor-1',
    name: 'Objeto',
    quantity: 1,
    version: 0,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    slotId: null,
    contentItemId: null,
    weight: 1,
    volume: null,
    ...overrides,
  }
}

const items: ActorItemDto[] = [
  item({ id: 'it-sword', name: 'Longsword', slotId: 'main-hand', weight: 3 }),
  item({ id: 'it-potion', name: 'Potion', weight: 1 }),
]

function stubFetch() {
  const calls: string[] = []
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    calls.push(`${method} ${url}`)
    if (url.includes('/equipment-slots')) return jsonResponse(slots)
    if (url.includes('/items') && method === 'POST') {
      return jsonResponse({ actorId: 'actor-1', items: items.map((entry) =>
        entry.id === 'it-potion' ? { ...entry, slotId: 'armor' } : entry) })
    }
    if (url.includes('/items') && method === 'DELETE') {
      return jsonResponse({ actorId: 'actor-1', items: items.map((entry) =>
        entry.id === 'it-sword' ? { ...entry, slotId: null } : entry) })
    }
    if (url.includes('/items')) return jsonResponse({ actorId: 'actor-1', items })
    return jsonResponse({
      id: 'actor-1', campaignId: 'c1', name: 'Elenar', rulesetKey: 'dnd5e',
      attributes: {}, resources: {}, version: 0, createdByUserId: 'u1',
      createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
      kind: 'pc', portraitId: null, biography: null, personality: null,
      level: 1, systemData: null, build: null,
    })
  }))
  return { calls }
}

describe('Inventario visual real (PMB-6.4)', () => {
  it('muestra huecos del sistema, mochila y detalle del objeto', async () => {
    stubFetch()
    render(<InventoryView />)
    expect(await screen.findByRole('button', { name: 'Arma principal: Longsword' })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Potion/ })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /Potion/ }))
    expect(screen.getByRole('heading', { name: 'Potion' })).toBeTruthy()
  })

  it('equipa y desequipa a través de la API', async () => {
    const { calls } = stubFetch()
    render(<InventoryView />)
    await screen.findByRole('button', { name: /Potion/ })
    fireEvent.click(screen.getByRole('button', { name: /Potion/ }))
    fireEvent.change(screen.getByRole('combobox', { name: 'Hueco' }), {
      target: { value: 'armor' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Equipar' }))
    await waitFor(() => expect(calls.some((call) =>
      call.startsWith('POST ') && call.includes('/equip'))).toBe(true))
    expect(screen.getByRole('status').textContent).toContain('equipado en Armadura')

    fireEvent.click(screen.getByRole('button', { name: 'Arma principal: Longsword' }))
    fireEvent.click(screen.getByRole('button', { name: 'Desequipar' }))
    await waitFor(() => expect(calls.some((call) =>
      call.startsWith('DELETE ') && call.includes('/equip'))).toBe(true))
  })
})
