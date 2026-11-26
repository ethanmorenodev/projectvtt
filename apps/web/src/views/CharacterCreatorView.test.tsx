import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { CampaignDetail } from '../api/types'
import { ToastProvider } from '../ui'
import { CharacterCreatorView } from './CharacterCreatorView'

/**
 * Creador de personajes real (PMB-3.5): pasos desde el blueprint (datos),
 * elecciones sobre contenido real, errores del servidor por paso, borrador
 * persistente y contenido fijado con usos al terminar.
 */

afterEach(cleanup)

const campaign: CampaignDetail = {
  id: 'c1',
  name: 'La caída de Scaldar',
  role: 'Owner',
  createdAt: new Date().toISOString(),
  archivedAt: null,
  memberCount: 2,
  description: null,
  hasCover: false,
}

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const blueprint = {
  id: 'bp1',
  system: 'dnd5e',
  key: 'standard',
  name: 'D&D 5e (SRD 5.1)',
  steps: [
    { key: 'identity', title: 'Identidad', kind: 'identity' },
    { key: 'race', title: 'Raza y variante', kind: 'choice', contentType: 'race', validation: { exactlyOne: true } },
    { key: 'class', title: 'Clase', kind: 'choice', contentType: 'class', validation: { exactlyOne: true } },
    { key: 'background', title: 'Trasfondo', kind: 'choice', contentType: 'background', validation: { exactlyOne: true } },
    { key: 'attributes', title: 'Atributos', kind: 'attributes' },
    { key: 'skills', title: 'Habilidades', kind: 'choice', options: ['Stealth', 'Perception'], validation: { countFromClass: true } },
    { key: 'review', title: 'Revisión', kind: 'review', optional: true },
  ],
  derivedStats: ['level', 'abilities'],
  startingEquipmentRules: null,
}

const races = [
  {
    id: 'race-dwarf', system: 'dnd5e', typeId: 't1', typeKey: 'race',
    name: 'Dwarf', summary: null, description: null, parentId: null,
    tags: [], imageId: null, focal: null,
    properties: { abilityBonuses: [{ ability: 'Constitution', bonus: 2 }], speed: 25, size: 'Medium', languages: ['Common', 'Dwarvish'], traits: ['Darkvision (60 ft)'] },
    inheritance: {}, scope: 'global', campaignId: null, workflowState: 'published',
    currentRevision: 1, createdByUserId: null,
    createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
  },
  {
    id: 'race-dwarf-mountain', system: 'dnd5e', typeId: 't1', typeKey: 'race',
    name: 'Mountain Dwarf', summary: null, description: null, parentId: 'race-dwarf',
    tags: [], imageId: null, focal: null,
    properties: { abilityBonuses: [{ ability: 'Strength', bonus: 2 }], speed: 25, size: 'Medium', languages: [], traits: ['Dwarven Armor Training'] },
    inheritance: { abilityBonuses: 'extend' }, scope: 'global', campaignId: null, workflowState: 'published',
    currentRevision: 1, createdByUserId: null,
    createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
  },
]

const previewOk = {
  valid: true,
  stepResults: blueprint.steps.map((step) => ({ step: step.key, ok: true, errors: [] })),
  derivedStats: {
    level: 1,
    abilities: Object.fromEntries(
      ['strength', 'dexterity', 'constitution', 'intelligence', 'wisdom', 'charisma']
        .map((ability) => [ability, { score: 12, modifier: 1 }]),
    ),
    speed: 25,
    size: 'Medium',
    languages: ['Common', 'Dwarvish'],
    pendingBonuses: [],
  },
}

function stubFetch(options: { preview?: unknown } = {}) {
  const calls: Array<{ url: string; method: string; body: unknown }> = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      const body = init?.body ? JSON.parse(String(init.body)) : null
      calls.push({ url, method, body })
      if (url.includes('/characters/blueprints')) {
        return jsonResponse([blueprint])
      }
      if (url.includes('typeKey=race')) {
        return jsonResponse(races)
      }
      if (url.includes('typeKey=class')) {
        return jsonResponse([])
      }
      if (url.includes('typeKey=background')) {
        return jsonResponse([])
      }
      if (url.includes('/characters/build/preview')) {
        return jsonResponse(options.preview ?? previewOk)
      }
      if (url.includes('/actors/') && url.includes('/usages')) {
        return jsonResponse({ id: 'usage-1' }, 201)
      }
      if (method === 'POST' && url.includes('/actors')) {
        return jsonResponse(
          {
            id: 'actor-1', campaignId: 'c1', name: body?.name ?? 'Elenar',
            rulesetKey: 'dnd5e', attributes: body?.attributes ?? {}, resources: {},
            version: 0, createdByUserId: 'u1',
            createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
            kind: 'pc', portraitId: null, biography: null, personality: null,
            level: 1, systemData: body?.systemData ?? null, build: body?.build ?? null,
          },
          201,
        )
      }
      return jsonResponse({ title: 'No encontrado' }, 404)
    }),
  )
  return calls
}

beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
})

describe('Creador de personajes real (PMB-3.5)', () => {
  function renderCreator(onDone: () => void = () => undefined) {
    return render(
      <ToastProvider>
        <CharacterCreatorView campaign={campaign} onDone={onDone} onExit={() => undefined} />
      </ToastProvider>,
    )
  }

  it('renderiza los pasos del blueprint y elige contenido con variantes', async () => {
    stubFetch()
    renderCreator()
    // Los pasos vienen del blueprint (datos), no del código.
    await screen.findByRole('button', { name: /Raza y variante/ })

    fireEvent.click(screen.getByRole('button', { name: /Raza y variante/ }))
    await screen.findByRole('button', { name: /Dwarf/ })
    fireEvent.click(screen.getByRole('button', { name: /Dwarf/ }))

    // Variante hija (subraza) y efectos visibles de la elección.
    expect(await screen.findByRole('button', { name: 'Mountain Dwarf' })).toBeTruthy()
    expect(screen.getAllByText(/Constitution \+2/).length).toBeGreaterThan(0)
    expect(screen.getAllByText('Darkvision (60 ft)').length).toBeGreaterThan(0)
  })

  it('muestra los errores del servidor en el paso correspondiente', async () => {
    stubFetch({
      preview: {
        ...previewOk,
        valid: false,
        stepResults: blueprint.steps.map((step) =>
          step.key === 'identity'
            ? { step: 'identity', ok: false, errors: ['El personaje necesita un nombre.'] }
            : { step: step.key, ok: true, errors: [] },
        ),
      },
    })
    renderCreator()
    fireEvent.click(await screen.findByRole('button', { name: /Identidad/ }))
    expect(await screen.findByText('El personaje necesita un nombre.')).toBeTruthy()
  })

  it('guardar y salir persiste el borrador con el paso actual', async () => {
    const calls = stubFetch()
    renderCreator()
    fireEvent.click(await screen.findByRole('button', { name: /Identidad/ }))
    fireEvent.change(screen.getByPlaceholderText('Nombre'), {
      target: { value: 'Elenar' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar y salir' }))

    await waitFor(() => {
      const saved = calls.find((call) => call.method === 'POST' && call.url.includes('/actors'))
      expect(saved).toBeTruthy()
      const build = saved!.body as { build?: { step?: number } }
      expect(build.build?.step).toBe(0)
    })
  })

  it('crear personaje crea el actor y fija el contenido con usos', async () => {
    const calls = stubFetch()
    let done = false
    renderCreator(() => {
      done = true
    })
    // Identidad con nombre y elecciones para un build completo.
    fireEvent.click(await screen.findByRole('button', { name: /Identidad/ }))
    fireEvent.change(screen.getByPlaceholderText('Nombre'), {
      target: { value: 'Elenar' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Raza y variante/ }))
    fireEvent.click((await screen.findAllByRole('button', { name: /Dwarf/ }))[0])

    fireEvent.click(screen.getByRole('button', { name: /Revisión/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Crear personaje' }))

    await waitFor(() => expect(done).toBeTruthy())
    const created = calls.find(
      (call) => call.method === 'POST' && call.url.includes('/campaigns/c1/actors'),
    )
    expect(created).toBeTruthy()
    const usageCalls = calls.filter((call) => call.url.includes('/usages'))
    // Raza (sin subraza elegida en este flujo): al menos 1 uso fijado.
    expect(usageCalls.length).toBeGreaterThanOrEqual(1)
  })
})
