import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ContentItemDto, ContentTypeDto } from '../api/types'
import { EditorView } from './EditorView'

/**
 * Editor universal real sobre la API (PMB-4.4): PATCH por diff (no pisa
 * datos ocultos), errores de campo del servidor con foco, solo lectura para
 * alcance global, creación con ámbito personal, publicación con
 * confirmación y panel de revisiones.
 */

afterEach(cleanup)
beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
})

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const now = new Date().toISOString()

const raceType: ContentTypeDto = {
  id: 't-race',
  system: 'dnd5e',
  key: 'race',
  displayName: 'Race',
  schema: {
    fields: [
      { key: 'abilityBonuses', type: 'list', required: true },
      { key: 'speed', type: 'number', required: true },
      { key: 'size', type: 'string', required: true },
      { key: 'languages', type: 'list', required: true },
      { key: 'traits', type: 'list', required: true },
    ],
  },
  createdAt: now,
}

const types: ContentTypeDto[] = [
  raceType,
  { id: 't-class', system: 'dnd5e', key: 'class', displayName: 'Class', schema: null, createdAt: now },
]

const dwarf: ContentItemDto = {
  id: 'i-dwarf',
  system: 'dnd5e',
  typeKey: 'race',
  typeId: 't-race',
  name: 'Dwarf',
  summary: 'Bold and hardy.',
  description: null,
  parentId: null,
  tags: ['srd'],
  imageId: null,
  focal: null,
  properties: {
    abilityBonuses: [{ ability: 'Constitution', bonus: 2 }],
    speed: 25,
    size: 'Medium',
    languages: ['Common', 'Dwarvish'],
    traits: ['Darkvision', 'Dwarven Resilience'],
  },
  inheritance: {},
  scope: 'global',
  campaignId: null,
  workflowState: 'published',
  currentRevision: 1,
  createdByUserId: null,
  createdAt: now,
  updatedAt: now,
  packKey: 'dnd5e-srd-5.1',
  packItemKey: 'race/dwarf',
  isPrivate: false,
  privateFields: [],
  usageCount: 0,
}

const hill: ContentItemDto = {
  id: 'i-hill',
  system: 'dnd5e',
  typeKey: 'race',
  typeId: 't-race',
  name: 'Hill Dwarf de Scaldar',
  summary: 'Homebrew de campaña.',
  description: null,
  parentId: 'i-dwarf',
  tags: ['homebrew'],
  imageId: null,
  focal: null,
  properties: {
    abilityBonuses: [{ ability: 'Constitution', bonus: 1 }],
    speed: 25,
    size: 'Medium',
    languages: ['Common'],
    traits: ['Resistencia enana'],
  },
  // speed con modo propio: renderiza control editable (no heredado).
  inheritance: { speed: 'override' },
  scope: 'campaign',
  campaignId: 'c1',
  workflowState: 'draft',
  currentRevision: 0,
  createdByUserId: 'u1',
  createdAt: now,
  updatedAt: now,
  packKey: null,
  packItemKey: null,
  isPrivate: false,
  privateFields: [],
  usageCount: 0,
}

const items = [dwarf, hill]

const revisions = [
  {
    id: 'r1',
    number: 1,
    payload: null,
    message: 'Importación SRD',
    createdByUserId: 'u1',
    createdAt: now,
  },
]

type Call = { url: string; method: string; body: Record<string, unknown> | undefined }

/** Stub de fetch con registro de llamadas; `patchError` simula el 400 del servidor. */
function stubFetch(options: { patchError?: boolean } = {}): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      const body = init?.body ? (JSON.parse(String(init.body)) as Record<string, unknown>) : undefined
      calls.push({ url, method, body })

      if (method === 'PATCH') {
        if (options.patchError) {
          return jsonResponse(
            {
              title: 'Solicitud incorrecta.',
              detail: "Falta la propiedad obligatoria 'speed'.",
              extensions: { field: 'properties.speed' },
            },
            400,
          )
        }
        return jsonResponse({ ...hill, ...(body ?? {}) })
      }
      if (method === 'POST' && url.includes('/publish')) {
        return jsonResponse({ ...hill, workflowState: 'published', currentRevision: 1 })
      }
      if (method === 'POST' && url.endsWith('/content/items')) {
        return jsonResponse({ ...hill, id: 'i-nuevo', ...(body ?? {}) })
      }
      if (url.includes('/revisions')) {
        return jsonResponse(revisions)
      }
      if (url.includes('/content/types')) {
        return jsonResponse(types)
      }
      if (url.includes('/content/items')) {
        return jsonResponse(items)
      }
      return jsonResponse({ title: 'No encontrado' }, 404)
    }),
  )
  return calls
}

function renderEditor(
  target: { kind: 'edit'; itemId: string } | { kind: 'create'; typeId: string },
  onBack = vi.fn(),
) {
  render(
    <EditorView types={types} items={items} packs={[]} target={target} onBack={onBack} />,
  )
  return onBack
}

describe('Editor universal real (PMB-4.4)', () => {
  it('envía en PATCH solo los campos cambiados', async () => {
    const calls = stubFetch()
    renderEditor({ kind: 'edit', itemId: 'i-hill' })

    const name = (await screen.findByLabelText('Nombre')) as HTMLInputElement
    expect(name.value).toBe('Hill Dwarf de Scaldar')
    expect(name.disabled).toBe(false)

    fireEvent.change(name, { target: { value: 'Guardia de Scaldar' } })
    // Aviso en cabecera y en el pie del formulario.
    expect(screen.getAllByText('Cambios sin guardar').length).toBeGreaterThan(0)

    fireEvent.click(screen.getByRole('button', { name: 'Guardar borrador' }))
    await waitFor(() => expect(calls.some((call) => call.method === 'PATCH')).toBe(true))

    const patch = calls.find((call) => call.method === 'PATCH')!
    expect(patch.url).toContain('/api/v1/content/items/i-hill')
    // Diff exacto: ni properties/inheritance (sin tocar) ni claves ocultas.
    expect(patch.body).toEqual({ name: 'Guardia de Scaldar' })
  })

  it('refleja el error de campo del servidor, marca el control y lo enfoca', async () => {
    stubFetch({ patchError: true })
    renderEditor({ kind: 'edit', itemId: 'i-hill' })

    fireEvent.click(await screen.findByRole('button', { name: 'Guardar borrador' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain("Falta la propiedad obligatoria 'speed'.")

    const speed = document.querySelector('[data-editor-field="speed"]') as HTMLInputElement
    expect(speed).toBeTruthy()
    expect(speed.getAttribute('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(speed)
    // El mensaje también queda bajo el campo afectado (id del campo).
    expect(document.querySelector('#speed-error')?.textContent).toBe(
      "Falta la propiedad obligatoria 'speed'.",
    )
  })

  it('alcance global: solo lectura con el servidor como autoridad', async () => {
    const onBack = renderEditor({ kind: 'edit', itemId: 'i-dwarf' })

    const name = (await screen.findByLabelText('Nombre')) as HTMLInputElement
    expect(name.disabled).toBe(true)
    expect(screen.getByText('Contenido global importado — solo lectura.')).toBeTruthy()
    expect(screen.getByText('Solo lectura')).toBeTruthy()

    // Sin escritura: ni pie de formulario ni publicación propia.
    expect(screen.queryByRole('button', { name: 'Guardar borrador' })).toBeNull()
    expect((screen.getByRole('button', { name: 'Publicar' }) as HTMLButtonElement).disabled).toBe(
      true,
    )

    // La vuelta al explorador funciona igual.
    fireEvent.click(screen.getByRole('button', { name: '← Volver a la biblioteca' }))
    expect(onBack).toHaveBeenCalledTimes(1)
  })

  it('creación: POST a /content/items con ámbito personal', async () => {
    const calls = stubFetch()
    renderEditor({ kind: 'create', typeId: 't-race' })

    const name = (await screen.findByLabelText('Nombre')) as HTMLInputElement
    fireEvent.change(name, { target: { value: 'Guardia veterana' } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear borrador' }))
    await waitFor(() =>
      expect(calls.some((call) => call.method === 'POST' && call.url.endsWith('/content/items'))).toBe(
        true,
      ),
    )

    const post = calls.find(
      (call) => call.method === 'POST' && call.url.endsWith('/content/items'),
    )!
    expect(post.body).toMatchObject({
      typeId: 't-race',
      name: 'Guardia veterana',
      // La biblioteca global solo crea contenido personal (PMB-4.4).
      scope: 'user',
    })
    // Tras crear, el item queda seleccionado en el editor.
    expect(await screen.findByRole('heading', { name: 'Guardia veterana' })).toBeTruthy()
  })

  it('publicar pide confirmación y crea una revisión', async () => {
    const calls = stubFetch()
    renderEditor({ kind: 'edit', itemId: 'i-hill' })

    fireEvent.click(await screen.findByRole('button', { name: 'Publicar' }))

    const dialog = await screen.findByRole('dialog', { name: 'Publicar una revisión' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publicar' }))

    await waitFor(() =>
      expect(calls.some((call) => call.method === 'POST' && call.url.includes('/publish'))).toBe(
        true,
      ),
    )
    const publish = calls.find(
      (call) => call.method === 'POST' && call.url.includes('/publish'),
    )!
    expect(publish.url).toContain('/api/v1/content/items/i-hill/publish')
  })

  it('lista las revisiones publicadas en el panel de estado', async () => {
    stubFetch()
    renderEditor({ kind: 'edit', itemId: 'i-hill' })

    expect(await screen.findByText('Revisión 1 · Importación SRD')).toBeTruthy()
    expect(screen.getByRole('complementary', { name: 'Estado y versionado' })).toBeTruthy()
    // Homebrew sin paquete: procedencia honesta.
    expect(screen.getByText('Contenido creado en ProjectVTT (sin paquete de origen).')).toBeTruthy()
  })
})
