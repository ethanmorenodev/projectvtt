import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  ContentItemDto,
  ContentPackDto,
  ContentRelationDto,
  ContentTypeDto,
} from '../api/types'
import { LibraryView } from './LibraryView'

/**
 * Explorador de Biblioteca real (PMB-4.2): árbol de tipos, rejilla con
 * filtros, inspector con propiedades/herencia/procedencia reales y
 * revisiones bajo demanda. Lectura sobre la API de contenido.
 * PMB-4.5: filtros de estado/etiqueta, orden por «Uso», ver usos y duplicar.
 */

afterEach(cleanup)
beforeEach(() => {
  window.localStorage.setItem('projectvtt.token', 'token-de-prueba')
  Object.defineProperty(URL, 'createObjectURL', {
    configurable: true,
    value: vi.fn(() => 'blob:img-de-prueba'),
  })
  Object.defineProperty(URL, 'revokeObjectURL', {
    configurable: true,
    value: vi.fn(),
  })
})

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const now = new Date().toISOString()

const types: ContentTypeDto[] = [
  { id: 't-race', system: 'dnd5e', key: 'race', displayName: 'Race', schema: null, createdAt: now },
  { id: 't-class', system: 'dnd5e', key: 'class', displayName: 'Class', schema: null, createdAt: now },
  { id: 't-bg', system: 'dnd5e', key: 'background', displayName: 'Background', schema: null, createdAt: now },
]

function item(overrides: Partial<ContentItemDto> & { id: string; typeId: string }): ContentItemDto {
  return {
    system: 'dnd5e',
    typeKey: 'race',
    name: 'Elemento',
    summary: null,
    description: null,
    parentId: null,
    tags: [],
    imageId: null,
    focal: null,
    properties: {},
    inheritance: {},
    scope: 'global',
    campaignId: null,
    workflowState: 'published',
    currentRevision: 1,
    createdByUserId: 'u1',
    createdAt: now,
    updatedAt: now,
    packKey: 'dnd5e-srd-5.1',
    packItemKey: null,
    isPrivate: false,
    privateFields: [],
    usageCount: 0,
    ...overrides,
  }
}

const items: ContentItemDto[] = [
  item({
    id: 'i-dwarf',
    typeId: 't-race',
    name: 'Dwarf',
    summary: 'Bold and hardy.',
    packItemKey: 'race/dwarf',
    tags: ['srd'],
    usageCount: 2,
    properties: { speed: 25, abilityBonuses: [{ ability: 'Constitution', bonus: 2 }] },
  }),
  item({
    id: 'i-hill',
    typeId: 't-race',
    name: 'Hill Dwarf',
    parentId: 'i-dwarf',
    packItemKey: 'race/dwarf/hill-dwarf',
    tags: ['srd'],
    inheritance: { traits: 'extend' },
  }),
  item({
    id: 'i-home',
    typeId: 't-race',
    name: 'Guardia de élite de la ciudadela de Scaldar',
    scope: 'campaign',
    campaignId: 'c1',
    workflowState: 'draft',
    currentRevision: 0,
    packKey: null,
    packItemKey: null,
    usageCount: 5,
  }),
  item({
    id: 'i-wizard',
    typeId: 't-class',
    typeKey: 'class',
    name: 'Wizard',
    packItemKey: 'class/wizard',
  }),
]

const packs: ContentPackDto[] = [
  {
    id: 'p1',
    key: 'dnd5e-srd-5.1',
    name: 'D&D 5e — System Reference Document 5.1',
    version: '5.1',
    system: 'dnd5e',
    sourceUrl: 'https://www.dndbeyond.com/sources/dnd/srd-5-1',
    license: 'CC BY 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
    attribution: ['Wizards of the Coast, System Reference Document 5.1 (CC BY 4.0).'],
    importedAt: now,
  },
]

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

/** Resumen de usos (contrato PMB-4.5): sin snapshots. */
const usages = [
  {
    actorName: 'Kaelen Piedra Alta',
    campaignName: 'La Corona Rota',
    revisionNumber: 3,
    createdAt: now,
  },
  {
    actorName: 'Thalor Viento Norte',
    campaignName: 'La Corona Rota',
    revisionNumber: 1,
    createdAt: now,
  },
]

/** Informe del servidor que se devuelve al importar (ADR-036). */
const importReport = {
  packKey: 'mi-paquete',
  strategy: 'Keep',
  types: 2,
  created: 3,
  updated: 0,
  skipped: 1,
  conflicts: ['race/dwarf'],
}

function stubFetch(
  importStatus = 200,
  importBody: unknown = importReport,
  options: { catalog?: ContentItemDto[]; imageStatus?: number } = {},
) {
  // Copia: publicar muta el estado devuelto sin contaminar otros tests.
  const catalog = (options.catalog ?? items).map((entry) => ({ ...entry }))
  const calls: string[] = []
  const requests: { method: string; url: string; body: unknown }[] = []
  const relations: ContentRelationDto[] = [
    {
      id: 'r-1',
      fromItemId: 'i-dwarf',
      kind: 'grants',
      toItemId: 'i-hill',
      label: 'Herencia enana',
      metadata: null,
    },
    {
      id: 'r-2',
      fromItemId: 'i-home',
      kind: 'leadsTo',
      toItemId: 'i-dwarf',
      label: null,
      metadata: null,
    },
  ]
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      calls.push(url)
      if (typeof init?.body === 'string') {
        requests.push({ method, url, body: JSON.parse(init.body) })
      }
      if (init?.body instanceof FormData) {
        requests.push({ method, url, body: init.body })
      }
      // /import antes que /packs (la URL del primero contiene la del segundo).
      if (url.includes('/content/packs/import')) {
        return jsonResponse(importBody, importStatus)
      }
      if (url.includes('/images/') && url.endsWith('/file')) {
        return options.imageStatus
          ? new Response(null, { status: options.imageStatus })
          : new Response(new Blob(['imagen'], { type: 'image/png' }))
      }
      // /usages y /relations antes que /content/items (la URL contiene ambos).
      if (url.includes('/usages')) {
        return jsonResponse(url.includes('i-home') ? [] : usages)
      }
      if (url.includes('/relations')) {
        if (method === 'POST') {
          const body = JSON.parse(String(init?.body)) as Record<string, unknown>
          const fromItemId =
            url.match(/\/content\/items\/([^/]+)\/relations/)?.[1] ?? 'i-dwarf'
          const created: ContentRelationDto = {
            id: `r-${relations.length + 1}`,
            fromItemId,
            kind: String(body.kind),
            toItemId: String(body.toItemId),
            label: (body.label as string | null) ?? null,
            metadata: null,
          }
          relations.push(created)
          return jsonResponse(created, 201)
        }
        if (method === 'DELETE') {
          const id = url.split('/').pop()
          const index = relations.findIndex((relation) => relation.id === id)
          if (index >= 0) relations.splice(index, 1)
          return new Response(null, { status: 204 })
        }
        return jsonResponse(relations)
      }
      if (url.includes('/content/types')) return jsonResponse(types)
      if (url.includes('/content/packs') && url.endsWith('/export')) {
        return new Response(new Blob(['zip'], { type: 'application/zip' }))
      }
      if (url.includes('/content/packs')) return jsonResponse(packs)
      if (url.includes('/revisions')) return jsonResponse(revisions)
      if (url.includes('/publish') && method === 'POST') {
        const id = url.match(/\/content\/items\/([^/]+)\/publish/)?.[1]
        const published = catalog.find((entry) => entry.id === id)
        if (published) published.workflowState = 'published'
        return jsonResponse({ ...(published ?? catalog[0]), workflowState: 'published' })
      }
      if (url.includes('/content/items') && method === 'POST') {
        const body = JSON.parse(String(init?.body)) as Record<string, unknown>
        return jsonResponse(
          item({ id: 'i-copy', typeId: String(body.typeId), name: String(body.name) }),
          201,
        )
      }
      if (url.includes('/content/items')) return jsonResponse(catalog)
      return jsonResponse({ title: 'No encontrado' }, 404)
    }),
  )
  return { calls, requests }
}

const grid = () => screen.getByRole('region', { name: 'Rejilla de contenido' })
const inspector = () => screen.getByRole('complementary', { name: 'Inspector' })

describe('Explorador de Biblioteca real (PMB-4.2)', () => {
  it('carga tipos, items y paquetes: árbol, rejilla e inspector reales', async () => {
    stubFetch()
    render(<LibraryView />)

    // Árbol con etiquetas en español y conteos reales (3 razas).
    expect(await screen.findByRole('button', { name: /Razas/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Clases/ })).toBeTruthy()
    expect(screen.getByText('3 elementos')).toBeTruthy()

    // Rejilla: miniaturas con estado y ámbito.
    expect(within(grid()).getByRole('button', { name: /^Dwarf/ })).toBeTruthy()
    expect(within(grid()).getByRole('button', { name: /Guardia de élite/ })).toBeTruthy()

    // Seleccionar Dwarf: propiedades formateadas + procedencia del SRD.
    fireEvent.click(screen.getByRole('button', { name: 'Dwarf' }))
    expect(within(inspector()).getByRole('heading', { name: 'Dwarf' })).toBeTruthy()
    expect(within(inspector()).getByText('race/dwarf')).toBeTruthy()
    // La propiedad se parte entre etiquetas: se compara por contenido.
    expect(
      within(inspector()).getByText(
        (_content, element) =>
          element?.tagName === 'LI' && element.textContent === 'speed: 25',
      ),
    ).toBeTruthy()
    expect(within(inspector()).getByText('Constitution +2')).toBeTruthy()
    expect(
      within(inspector()).getByText('D&D 5e — System Reference Document 5.1'),
    ).toBeTruthy()
    expect(within(inspector()).getByRole('link', { name: 'CC BY 4.0' })).toBeTruthy()
    // Editar sigue bloqueado para el SRD global (el servidor lo decide);
    // Duplicar y Ver usos ya están vivos (PMB-4.5).
    const edit = within(inspector()).getByRole('button', {
      name: 'Editar',
    }) as HTMLButtonElement
    expect(edit.disabled).toBe(true)
    expect(
      (within(inspector()).getByRole('button', { name: 'Duplicar' }) as HTMLButtonElement).disabled,
    ).toBe(false)
    expect(
      (within(inspector()).getByRole('button', { name: 'Ver usos' }) as HTMLButtonElement).disabled,
    ).toBe(false)
  })

  it('filtra por búsqueda y ámbito con estado vacío recuperable', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    // Búsqueda sin resultados → estado honesto + limpiar.
    fireEvent.change(screen.getByLabelText('Buscar por nombre, etiqueta o tipo…'), {
      target: { value: 'zzz' },
    })
    expect(screen.getByText('0 elementos')).toBeTruthy()
    expect(screen.getByText('No hay elementos con estos filtros.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    expect(screen.getByText('3 elementos')).toBeTruthy()

    // Ámbito «DM»: el SRD es global y el homebrew es de campaña → 0.
    fireEvent.change(screen.getByRole('combobox', { name: 'Ámbito:' }), {
      target: { value: 'dm' },
    })
    expect(screen.getByText('0 elementos')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    expect(screen.getByText('3 elementos')).toBeTruthy()
  })

  it('carga las revisiones bajo demanda en el inspector', async () => {
    const { calls } = stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    fireEvent.click(screen.getByRole('button', { name: 'Dwarf' }))

    fireEvent.click(within(inspector()).getByRole('button', { name: 'Ver revisiones' }))
    expect(await within(inspector()).findByText('Revisión 1 · Importación SRD')).toBeTruthy()
    expect(
      calls.some((url) => url.includes('/content/items/i-dwarf/revisions')),
    ).toBe(true)

    // Al colapsar se oculta; al reabrir no vuelve a pedir.
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Ocultar revisiones' }))
    expect(screen.queryByText('Revisión 1 · Importación SRD')).toBeNull()
  })

  it('item de campaña sin paquete: estado borrador y procedencia honesta', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.click(within(grid()).getByRole('button', { name: /Guardia de élite/ }))
    expect(
      within(inspector()).getByRole('heading', { name: /Guardia de élite/ }),
    ).toBeTruthy()
    expect(within(inspector()).getByText('Campaña')).toBeTruthy()
    expect(within(inspector()).getByText('Borrador')).toBeTruthy()
    expect(
      within(inspector()).getByText('Contenido creado en ProjectVTT (sin paquete de origen).'),
    ).toBeTruthy()
    // Sin publicar: 0 revisiones.
    expect(screen.queryByText(/rev\. 1/)).toBeNull()
  })

  it('«Editar» abre el editor real y «Volver» regresa al explorador (PMB-4.4)', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    // El SRD (global) sigue bloqueado; el de campaña sí se puede editar.
    fireEvent.click(within(grid()).getByRole('button', { name: /Guardia de élite/ }))
    const edit = within(inspector()).getByRole('button', {
      name: 'Editar',
    }) as HTMLButtonElement
    expect(edit.disabled).toBe(false)
    fireEvent.click(edit)

    // El editor sustituye al explorador: ruta, formulario y panel de estado.
    expect(await screen.findByRole('region', { name: 'Formulario de contenido' })).toBeTruthy()
    expect(screen.getByRole('complementary', { name: 'Estado y versionado' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: /Guardia de élite/ })).toBeTruthy()

    // Vuelta: se recarga la lista (los guardados del editor se reflejan).
    fireEvent.click(screen.getByRole('button', { name: '← Volver a la biblioteca' }))
    expect(await screen.findByRole('region', { name: 'Rejilla de contenido' })).toBeTruthy()
  })

  it('«Nuevo contenido» abre la creación con el tipo activo (PMB-4.4)', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.click(screen.getByRole('button', { name: 'Nuevo contenido' }))
    expect(await screen.findByRole('heading', { name: 'Nuevo contenido' })).toBeTruthy()
    expect(screen.getByRole('region', { name: 'Formulario de contenido' })).toBeTruthy()
    // Ámbito personal: el único posible desde la biblioteca global.
    const scope = screen.getByLabelText('Ámbito') as HTMLSelectElement
    expect(scope.value).toBe('user')
    expect(screen.getByRole('button', { name: 'Crear borrador' })).toBeTruthy()
  })
})

describe('Explorador de Biblioteca — relaciones tipadas (PMB-4.6)', () => {
  it('lista relaciones del servidor y navega al elemento relacionado', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    fireEvent.click(within(grid()).getByRole('button', { name: /^Dwarf/ }))

    const link = await within(inspector()).findByRole('button', { name: 'Hill Dwarf' })
    fireEvent.click(link)
    expect(within(inspector()).getByRole('heading', { name: 'Hill Dwarf' })).toBeTruthy()
  })

  it('añade y quita relaciones con el servidor como autoridad', async () => {
    const { calls, requests } = stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    fireEvent.click(within(grid()).getByRole('button', { name: /^Dwarf/ }))
    await within(inspector()).findByRole('button', { name: 'Editar relaciones' })

    fireEvent.click(within(inspector()).getByRole('button', { name: 'Editar relaciones' }))
    const dialog = screen.getByRole('dialog', { name: 'Editar relaciones de «Dwarf»' })
    const selects = within(dialog).getAllByRole('combobox')
    fireEvent.change(selects[0], { target: { value: 'leadsTo' } })
    fireEvent.change(selects[1], { target: { value: 'i-home' } })
    fireEvent.change(
      within(dialog).getByRole('textbox', { name: 'Etiqueta (opcional)' }),
      { target: { value: 'Prueba' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Añadir relación' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    await waitFor(() => expect(screen.getByRole('status').textContent)
      .toContain('añadida a «Guardia de élite'))
    const post = requests.find(
      (request) => request.method === 'POST' && request.url.includes('/relations'))
    expect(post?.body).toMatchObject({
      kind: 'leadsTo', toItemId: 'i-home', label: 'Prueba',
    })

    // Tras recargar aparece como saliente; la entrante previa ya mostraba
    // el mismo nombre, así que hay dos enlaces con ese nombre.
    await waitFor(() => expect(
      within(inspector()).getAllByRole('button', { name: 'Guardia de élite de la ciudadela de Scaldar' }),
    ).toHaveLength(2))

    fireEvent.click(within(inspector()).getByRole('button', { name: 'Editar relaciones' }))
    const removes = within(screen.getByRole('dialog', { name: 'Editar relaciones de «Dwarf»' }))
      .getAllByRole('button', { name: 'Quitar' })
    fireEvent.click(removes[removes.length - 1])
    // DELETE no lleva body: se comprueba por URL (el GET no termina en id).
    expect(calls.some((url) => url.includes('/relations/'))).toBe(true)
  })
})

describe('Explorador de Biblioteca — PMB-4.5 (filtros, usos, duplicar)', () => {
  it('filtra por estado y etiqueta en la rejilla', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    expect(screen.getByText('3 elementos')).toBeTruthy()

    // Los filtros secundarios viven en el panel plegable (Fase C).
    fireEvent.click(screen.getByRole('button', { name: 'Mostrar filtros' }))

    // Estado: solo borradores → el homebrew de campaña (1 de 3).
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Estado:' })).getByRole('button', {
        name: 'Borrador',
      }),
    )
    expect(screen.getByText('1 elemento')).toBeTruthy()
    expect(within(grid()).getByRole('button', { name: /Guardia de élite/ })).toBeTruthy()

    // Etiqueta «srd» sobre borradores → ninguno coincide: vacío recuperable.
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Etiqueta:' })).getByRole('button', {
        name: 'srd',
      }),
    )
    expect(screen.getByText('0 elementos')).toBeTruthy()
    expect(screen.getByText('No hay elementos con estos filtros.')).toBeTruthy()
    fireEvent.click(screen.getAllByRole('button', { name: 'Limpiar filtros' })[0])
    expect(screen.getByText('3 elementos')).toBeTruthy()

    // Etiqueta: «srd» deja las 2 razas importadas; «Todas» las restaura.
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Etiqueta:' })).getByRole('button', {
        name: 'srd',
      }),
    )
    expect(screen.getByText('2 elementos')).toBeTruthy()
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Etiqueta:' })).getByRole('button', {
        name: 'Todas',
      }),
    )
    expect(screen.getByText('3 elementos')).toBeTruthy()
  })

  it('ordena por «Uso» con los usageCount del servidor', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.change(screen.getByRole('combobox', { name: 'Orden' }), {
      target: { value: 'uso' },
    })
    const names = [...document.querySelectorAll('.biblio-tile__name')].map(
      (node) => node.textContent ?? '',
    )
    // Guardia (5) → Dwarf (2) → Hill (0), distinto del orden alfabético.
    expect(names).toEqual([
      'Guardia de élite de la ciudadela de Scaldar',
      'Dwarf',
      'Hill Dwarf',
    ])
  })

  it('«Ver usos» lista el resumen sin snapshots y ofrece estado vacío', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    // Dwarf: 2 usos de muestra (sin snapshots, con campaña y revisión).
    fireEvent.click(within(grid()).getByRole('button', { name: /^Dwarf/ }))
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Ver usos' }))
    const panel = await screen.findByRole('region', { name: 'Usos del elemento' })
    expect(within(panel).getAllByRole('listitem')).toHaveLength(2)
    expect(within(panel).getByText('Kaelen Piedra Alta')).toBeTruthy()
    expect(within(panel).getByText(/La Corona Rota · rev\. 3/)).toBeTruthy()
    expect(within(panel).getByText(/los snapshots no se muestran/)).toBeTruthy()

    // Guardia sin usos → estado vacío honesto.
    fireEvent.click(within(grid()).getByRole('button', { name: /Guardia de élite/ }))
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Ver usos' }))
    expect(await screen.findByText(/Sin usos registrados/)).toBeTruthy()
  })

  it('«Duplicar» confirma y crea una copia personal vía POST', async () => {
    const { requests } = stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.click(within(grid()).getByRole('button', { name: /Guardia de élite/ }))
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Duplicar' }))

    const dialog = screen.getByRole('dialog', {
      name: 'Duplicar «Guardia de élite de la ciudadela de Scaldar»',
    })
    expect(within(dialog).getByText(/biblioteca personal/)).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Duplicar' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect((await screen.findByRole('status')).textContent).toContain(
      'Copia creada en tu biblioteca personal: «Guardia de élite de la ciudadela de Scaldar (copia)».',
    )

    // La petición copia solo lo visible con ámbito personal: sin padre
    // (la jerarquía la decide el editor) y sin campaña ni campos privados.
    const post = requests.find(
      (request) => request.method === 'POST' && request.url.includes('/content/items'),
    )
    expect(post).toBeTruthy()
    const body = post!.body as Record<string, unknown>
    expect(body.typeId).toBe('t-race')
    expect(body.name).toBe('Guardia de élite de la ciudadela de Scaldar (copia)')
    expect(body.scope).toBe('user')
    expect(body.properties).toEqual({})
    expect(body.parentId).toBeUndefined()
    expect(body.campaignId).toBeUndefined()
    expect(body.privateFields).toBeUndefined()
  })
})

describe('Explorador de Biblioteca — importación de paquetes (EXP-43)', () => {
  const openImportDialog = async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    fireEvent.click(screen.getByRole('button', { name: 'Importar paquete' }))
    return screen.getByRole('dialog', { name: 'Importar paquete .pvttpkg' })
  }

  it('abre el diálogo con selector de archivo y las tres estrategias', async () => {
    const dialog = await openImportDialog()
    expect(within(dialog).getByLabelText('Archivo del paquete (.pvttpkg)')).toBeTruthy()
    const select = within(dialog).getByRole('combobox', {
      name: 'Estrategia ante conflictos',
    }) as HTMLSelectElement
    expect([...select.options].map((option) => option.value)).toEqual([
      'Keep', 'Update', 'Copy',
    ])
    expect(within(dialog).getByText(/El servidor detecta los conflictos/)).toBeTruthy()
  })

  it('envía el archivo y la estrategia elegida y muestra el informe del servidor', async () => {
    const { requests, calls } = stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    fireEvent.click(screen.getByRole('button', { name: 'Importar paquete' }))
    const dialog = screen.getByRole('dialog', { name: 'Importar paquete .pvttpkg' })

    const file = new File(['paquete'], 'mi-paquete.pvttpkg', { type: 'application/zip' })
    fireEvent.change(within(dialog).getByLabelText('Archivo del paquete (.pvttpkg)'), {
      target: { files: [file] },
    })
    fireEvent.change(within(dialog).getByRole('combobox', {
      name: 'Estrategia ante conflictos',
    }), { target: { value: 'Copy' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Importar' }))

    const report = await screen.findByRole('status', { name: 'Informe de importación' })
    expect(within(report).getByText('Paquete «mi-paquete» importado.')).toBeTruthy()
    expect(within(report).getByText('race/dwarf')).toBeTruthy()

    const post = requests.find(
      (request) => request.method === 'POST' && request.url.includes('/content/packs/import'),
    )
    expect(post).toBeTruthy()
    const form = post!.body as FormData
    expect(form.get('strategy')).toBe('Copy')
    expect((form.get('file') as File).name).toBe('mi-paquete.pvttpkg')

    // La biblioteca se recarga: ítems y paquetes se piden de nuevo.
    await waitFor(() => expect(
      calls.filter((url) => url.endsWith('/content/items')),
    ).toHaveLength(2))
    expect(calls.filter((url) => url.endsWith('/content/packs'))).toHaveLength(2)
  })

  it('muestra tal cual el 403 del servidor sin simular permisos', async () => {
    stubFetch(403, {
      title: 'Acceso denegado',
      detail: 'No tienes permiso de operador de contenido.',
    })
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    fireEvent.click(screen.getByRole('button', { name: 'Importar paquete' }))
    const dialog = screen.getByRole('dialog', { name: 'Importar paquete .pvttpkg' })
    const file = new File(['paquete'], 'mi-paquete.pvttpkg', { type: 'application/zip' })
    fireEvent.change(within(dialog).getByLabelText('Archivo del paquete (.pvttpkg)'), {
      target: { files: [file] },
    })
    fireEvent.change(within(dialog).getByRole('combobox', {
      name: 'Estrategia ante conflictos',
    }), { target: { value: 'Update' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Importar' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('No tienes permiso de operador de contenido.')
    expect(screen.getByRole('dialog', { name: 'Importar paquete .pvttpkg' })).toBeTruthy()
    expect(screen.queryByRole('status', { name: 'Informe de importación' })).toBeNull()
  })
})

describe('Explorador de Biblioteca — portadas, búsqueda y escala', () => {
  const searchInput = () => screen.getByLabelText('Buscar por nombre, etiqueta o tipo…')
  const thumbSrc = (tile: HTMLElement) => tile.querySelector('img')?.getAttribute('src')

  it('muestra la imagen asignada en la miniatura (data-testid library-tile)', async () => {
    const withImage = items.map((entry, index) =>
      index === 0 ? { ...entry, imageId: 'img-dwarf' } : entry)
    stubFetch(200, importReport, { catalog: withImage })
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    const tile = await waitFor(() => {
      const found = screen.getAllByTestId('library-tile')[0]
      expect(thumbSrc(found)).toBe('blob:img-de-prueba')
      return found
    })
    expect(tile.querySelector('.biblio-tile__thumb--cover')).toBeTruthy()
    expect(
      within(tile).getByRole('button', { name: /^Dwarf/ }),
    ).toBeTruthy()
  })

  it('cae a la portada derivada si la imagen no hay o falla la carga', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    expect(thumbSrc(screen.getAllByTestId('library-tile')[0]))
      .toBe('/icons/dwarf-face.svg')

    cleanup()
    const withImage = items.map((entry, index) =>
      index === 0 ? { ...entry, imageId: 'img-roto' } : entry)
    stubFetch(200, importReport, { catalog: withImage, imageStatus: 404 })
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })
    await waitFor(() => expect(thumbSrc(screen.getAllByTestId('library-tile')[0]))
      .toBe('/icons/dwarf-face.svg'))
  })

  it('busca por typeKey y por el nombre legible del tipo', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.change(searchInput(), { target: { value: 'race' } })
    expect(screen.getByTestId('library-count').textContent).toBe('3 elementos')
    expect(screen.getAllByTestId('library-tile')).toHaveLength(3)

    fireEvent.change(searchInput(), { target: { value: 'razas' } })
    expect(screen.getByTestId('library-count').textContent).toBe('3 elementos')
    expect(screen.getAllByTestId('library-tile')).toHaveLength(3)
  })

  it('reinicia búsqueda, etiqueta, estado y ámbito al cambiar de tipo', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.change(searchInput(), { target: { value: 'Dwarf' } })
    fireEvent.change(screen.getByRole('combobox', { name: 'Ámbito:' }), {
      target: { value: 'dm' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Mostrar filtros' }))
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Estado:' })).getByRole('button', {
        name: 'Borrador',
      }),
    )
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Etiqueta:' })).getByRole('button', {
        name: 'srd',
      }),
    )
    expect(screen.getByTestId('library-count').textContent).toBe('0 elementos')

    // «Clases» tiene 1 elemento: categoría vacía deshabilitada no, cambia el tipo.
    fireEvent.click(screen.getByRole('button', { name: /Clases/ }))
    expect((searchInput() as HTMLInputElement).value).toBe('')
    expect((screen.getByRole('combobox', { name: 'Ámbito:' }) as HTMLSelectElement).value)
      .toBe('todos')
    expect(screen.getByTestId('library-count').textContent).toBe('1 elemento')
    expect(within(grid()).getByRole('button', { name: /Wizard/ })).toBeTruthy()
  })

  it('deshabilita las categorías sin elementos con título explicativo', async () => {
    stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    const empty = screen.getByRole('button', { name: /Trasfondos/ }) as HTMLButtonElement
    expect(empty.disabled).toBe(true)
    expect(empty.title).toBe('Esta categoría no tiene elementos.')
    const race = screen.getByRole('button', { name: /Razas/ }) as HTMLButtonElement
    expect(race.disabled).toBe(false)
  })

  it('pagina en bloques de 60 y reinicia la página al filtrar', async () => {
    const extra = Array.from({ length: 65 }, (_, index) => item({
      id: `i-extra-${index}`,
      typeId: 't-race',
      name: `Extra ${String(index).padStart(3, '0')}`,
    }))
    stubFetch(200, importReport, { catalog: [...items, ...extra] })
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    expect(screen.getAllByTestId('library-tile')).toHaveLength(60)
    expect(screen.getByTestId('library-count').textContent).toBe('68 elementos')
    fireEvent.click(screen.getByTestId('library-show-more'))
    expect(screen.getAllByTestId('library-tile')).toHaveLength(68)
    expect(screen.queryByTestId('library-show-more')).toBeNull()

    fireEvent.change(searchInput(), { target: { value: 'Extra 001' } })
    expect(screen.getByTestId('library-count').textContent).toBe('1 elemento')
    expect(screen.getAllByTestId('library-tile')).toHaveLength(1)
  })

  it('publica con el endpoint real y exporta los paquetes listados', async () => {
    const { calls, requests } = stubFetch()
    render(<LibraryView />)
    await screen.findByRole('button', { name: /Razas/ })

    fireEvent.click(within(grid()).getByRole('button', { name: /Guardia de élite/ }))
    const publish = screen.getByTestId('library-publish') as HTMLButtonElement
    expect(publish.disabled).toBe(false)
    fireEvent.click(publish)
    expect(await screen.findByText(/Revisión publicada de/)).toBeTruthy()
    expect(requests.find((request) => request.url.includes('/publish'))?.body)
      .toEqual({ message: null })
    const published = screen.getByTestId('library-publish') as HTMLButtonElement
    expect(published.disabled).toBe(true)
    expect(published.title).toBe('Este contenido ya está publicado.')

    fireEvent.click(screen.getByTestId('library-export-pack'))
    await waitFor(() => expect(calls.some((url) => url.includes(
      '/content/packs/dnd5e-srd-5.1/export',
    ))).toBe(true))
  })
})
