import { useEffect, useMemo, useState } from 'react'
import { apiFetch } from '../api/client'
import { exportPack, importPack, listPacks } from '../api/packs'
import type { ImportStrategy, PackageImportReport } from '../api/packs'
import { useImageUrl } from '../api/portraits'
import { t } from '../i18n'
import type {
  ContentItemDto,
  ContentPackDto,
  ContentRelationDto,
  ContentRevisionDto,
  ContentTypeDto,
  ItemUsageSummaryDto,
} from '../api/types'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Dialog } from '../ui/Dialog'
import { Button } from '../ui/Button'
import { contentIcon, coverWithImage } from '../ui/contentIcons'
import { Icon } from '../ui/Icon'
import { useNavigate } from '../routing/router'
import { EditorView } from './EditorView'
import type { EditorTarget } from './EditorView'
import './library.css'

/**
 * Explorador de Biblioteca (PMB-4.2, LIBRARY.md §2): árbol de tipos y
 * categorías, rejilla de miniaturas cuadradas e inspector con propiedades,
 * herencia, procedencia (paquete + licencia) y revisiones. Lectura sobre la
 * API de contenido; la edición abre el editor universal (PMB-4.4).
 *
 * PMB-4.5 (prototipo aprobado): filtros de estado y etiqueta en la rejilla,
 * orden por «Uso» (`usageCount`) y acciones vivas en el inspector —
 * Duplicar (confirmación → POST /content/items con ámbito personal) y
 * Ver usos (panel inline con `GET /content/items/{id}/usages`, resumen sin
 * snapshots) y Publicar (mismo endpoint del editor). Rejilla paginada y
 * portadas con imagen asignada.
 */

type ScopeFilter = 'todos' | 'global' | 'campaign' | 'dm' | 'user'
type Sort = 'nombre' | 'recientes' | 'uso'
/** Filtro de estado (PMB-4.5): valores de `workflowState` + «todos».
 * «draft» = aún no publicado (incluye propuestas), como en el prototipo. */
type EstadoFilter = 'todos' | 'published' | 'draft'

const TYPE_ORDER = ['race', 'class', 'background']

/** Paginación client-side: bloques de 60 sobre el conjunto filtrado. */
const PAGE_SIZE = 60

/** Kinds del modelo (CONTENT_MODEL §8); el servidor valida los permitidos. */
const RELATION_KINDS = [
  'grants', 'requires', 'excludes', 'memberOf', 'locatedIn', 'leadsTo', 'owns',
] as const

/** EXP-43: valores que acepta el endpoint de importación del backend. */
const IMPORT_STRATEGIES: readonly ImportStrategy[] = ['Keep', 'Update', 'Copy']

/** Valor de propiedad legible (arrays, bonificaciones, primitivas). */
function formatPropertyValue(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return '—'
  }
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value)
  }
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return '—'
    }
    return value
      .map((entry) => {
        if (entry && typeof entry === 'object') {
          const record = entry as Record<string, unknown>
          if ('ability' in record && 'bonus' in record) {
            return `${record.ability} +${record.bonus}`
          }
          return JSON.stringify(entry)
        }
        return String(entry)
      })
      .join(' · ')
  }
  if (typeof value === 'object') {
    return JSON.stringify(value)
  }
  return String(value)
}

function scopeLabel(scope: string): string {
  const scopes = t.biblioteca.scopes as Record<string, string>
  return scopes[scope] ?? scope
}

function typeLabel(type: ContentTypeDto): string {
  const labels = t.biblioteca.typeLabels as Record<string, string>
  return labels[type.key] ?? type.displayName
}

function statusLabel(state: string): string {
  const status = t.biblioteca.status as Record<string, string>
  return status[state] ?? state
}

function relationKindLabel(kind: string): string {
  const labels = t.biblioteca.relationKinds as Record<string, string>
  return labels[kind] ?? kind
}

/** Miniatura de la rejilla: usa la imagen real del contenido cuando cargó
 *  (incluido el fallback si falla) y, si no, la portada derivada del tipo. */
function LibraryTile({
  item,
  selected,
  onSelect,
}: {
  item: ContentItemDto
  selected: boolean
  onSelect: (id: string) => void
}) {
  const imageUrl = useImageUrl(item.imageId)
  const cover = coverWithImage(imageUrl, item.typeKey, item.name, item.tags)
  return (
    <li data-testid="library-tile">
      <button
        type="button"
        className={`biblio-tile ${selected ? 'is-selected' : ''}`}
        onClick={() => onSelect(item.id)}
      >
        <span className={`biblio-tile__thumb biblio-tile__thumb--${cover.kind}`}
          aria-hidden="true">
          <img src={cover.src} alt="" />
        </span>
        <span className="biblio-tile__name">{item.name}</span>
        <span className="biblio-tile__meta">
          <span className={`biblio-tag ${item.scope !== 'global' ? 'is-warn' : ''}`}>
            {scopeLabel(item.scope)}
          </span>
          {item.workflowState !== 'published' && (
            <span className="biblio-tag is-warn">
              {statusLabel(item.workflowState)}
            </span>
          )}
        </span>
      </button>
    </li>
  )
}

export function LibraryView() {
  const go = useNavigate()
  const [types, setTypes] = useState<ContentTypeDto[] | null>(null)
  const [items, setItems] = useState<ContentItemDto[] | null>(null)
  const [packs, setPacks] = useState<ContentPackDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [typeId, setTypeId] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  const [scope, setScope] = useState<ScopeFilter>('todos')
  const [sort, setSort] = useState<Sort>('nombre')
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE)
  const [expandedTypes, setExpandedTypes] = useState<Set<string>>(new Set())
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set())

  const [revisions, setRevisions] = useState<ContentRevisionDto[] | null>(null)
  const [revisionsOpen, setRevisionsOpen] = useState(false)
  const [revisionsError, setRevisionsError] = useState<string | null>(null)
  const [revisionsBusy, setRevisionsBusy] = useState(false)

  // PMB-4.5: filtros de estado/etiqueta y flujos del inspector.
  const [estado, setEstado] = useState<EstadoFilter>('todos')
  const [tag, setTag] = useState<string | null>(null)
  // Fase C del rediseño: los filtros secundarios se agrupan en un panel
  // desplegable para que la barra principal quede legible de un vistazo.
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [showUsages, setShowUsages] = useState(false)
  const [usages, setUsages] = useState<ItemUsageSummaryDto[] | null>(null)
  const [usagesError, setUsagesError] = useState<string | null>(null)
  const [usagesBusy, setUsagesBusy] = useState(false)
  const [duplicateOpen, setDuplicateOpen] = useState(false)
  const [duplicateError, setDuplicateError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [publishBusy, setPublishBusy] = useState(false)
  const [publishError, setPublishError] = useState<string | null>(null)
  const [exportKey, setExportKey] = useState<string | null>(null)
  const [exportError, setExportError] = useState<string | null>(null)

  // EXP-43: importación de paquetes. El informe y el error son del servidor;
  // la interfaz no decide conflictos ni simula permisos de operador.
  const [importOpen, setImportOpen] = useState(false)
  const [importFile, setImportFile] = useState<File | null>(null)
  const [importStrategy, setImportStrategy] = useState<ImportStrategy>('Keep')
  const [importBusy, setImportBusy] = useState(false)
  const [importError, setImportError] = useState<string | null>(null)
  const [importReport, setImportReport] = useState<PackageImportReport | null>(null)

  // Relaciones tipadas (PMB-4.6): se leen de la API y se editan con el
  // servidor como autoridad (kind permitido y permisos validados allí).
  const [relations, setRelations] = useState<ContentRelationDto[] | null>(null)
  const [relationEditorOpen, setRelationEditorOpen] = useState(false)
  const [relKind, setRelKind] = useState<string>('grants')
  const [relTarget, setRelTarget] = useState<string>('')
  const [relLabel, setRelLabel] = useState('')
  const [relationError, setRelationError] = useState<string | null>(null)

  // Editor universal (PMB-4.4): null = explorador; con valor, la vista
  // entera pasa al editor del item o a la creación con el tipo activo.
  const [editing, setEditing] = useState<EditorTarget | null>(null)

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const [typeList, itemList, packList] = await Promise.all([
          apiFetch<ContentTypeDto[]>('/api/v1/content/types'),
          apiFetch<ContentItemDto[]>('/api/v1/content/items'),
          apiFetch<ContentPackDto[]>('/api/v1/content/packs'),
        ])
        if (cancelled) {
          return
        }
        const ordered = [...typeList].sort((a, b) => {
          const ai = TYPE_ORDER.indexOf(a.key)
          const bi = TYPE_ORDER.indexOf(b.key)
          return (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi) || a.displayName.localeCompare(b.displayName)
        })
        setTypes(ordered)
        setItems(itemList)
        setPacks(packList)
        if (ordered.length > 0) {
          setTypeId(ordered[0].id)
          setExpandedTypes(new Set([ordered[0].id]))
        }
      } catch (cause) {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : t.biblioteca.error)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [reloadKey])

  // Vuelta del editor: se recargan los items (guardados/creados allí) sin
  // repetir la carga completa de tipos y packs.
  const refetchItems = async () => {
    try {
      const itemList = await apiFetch<ContentItemDto[]>('/api/v1/content/items')
      setItems(itemList)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.biblioteca.error)
    }
  }

  // Tras importar se recargan ítems y paquetes sin perder filtros ni selección.
  const reloadCatalog = async () => {
    try {
      const [itemList, packList] = await Promise.all([
        apiFetch<ContentItemDto[]>('/api/v1/content/items'),
        listPacks(),
      ])
      setItems(itemList)
      setPacks(packList)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.biblioteca.error)
    }
  }

  const submitImport = async () => {
    if (!importFile) {
      return
    }
    setImportBusy(true)
    setImportError(null)
    try {
      const report = await importPack(importFile, importStrategy)
      setImportReport(report)
      setImportOpen(false)
      setImportFile(null)
      await reloadCatalog()
    } catch (cause) {
      // 403 (no operador) y cualquier otro fallo se muestran tal cual.
      setImportError(cause instanceof Error ? cause.message : t.biblioteca.importError)
    } finally {
      setImportBusy(false)
    }
  }

  // Cualquier cambio de conjunto (tipo o filtros) vuelve a la primera página.
  useEffect(() => {
    setVisibleCount(PAGE_SIZE)
  }, [typeId, scope, estado, tag, query])

  // Las revisiones y los usos son de la selección actual: se limpian al
  // cambiar (igual que el aviso de duplicar y el diálogo).
  useEffect(() => {
    setRevisions(null)
    setRevisionsOpen(false)
    setRevisionsError(null)
    setUsages(null)
    setUsagesError(null)
    setShowUsages(false)
    setDuplicateOpen(false)
    setDuplicateError(null)
    setPublishError(null)
    setNotice(null)
  }, [selectedId])

  // Relaciones de la selección: se recargan al cambiar de elemento.
  useEffect(() => {
    setRelations(null)
    setRelationEditorOpen(false)
    setRelationError(null)
    if (!selectedId) {
      return
    }
    let cancelled = false
    void apiFetch<ContentRelationDto[]>(`/api/v1/content/items/${selectedId}/relations`)
      .then((rows) => {
        if (!cancelled) {
          setRelations(rows)
        }
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setRelations([])
          setRelationError(cause instanceof Error ? cause.message : t.biblioteca.relationError)
        }
      })
    return () => {
      cancelled = true
    }
  }, [selectedId])

  const orderedTypes = types ?? []
  const allItems = items ?? []
  const activeType = orderedTypes.find((type) => type.id === typeId) ?? null
  const typeItems = useMemo(
    () => (activeType ? allItems.filter((item) => item.typeId === activeType.id) : []),
    [allItems, activeType],
  )

  // Etiquetas disponibles en la rejilla activa (derivadas de los items).
  const availableTags = useMemo(() => {
    const set = new Set<string>()
    for (const item of typeItems) {
      for (const value of item.tags) set.add(value)
    }
    return [...set].sort((a, b) => a.localeCompare(b, 'es'))
  }, [typeItems])

  const visible = useMemo(() => {
    let list = typeItems
    if (scope !== 'todos') {
      list = list.filter((item) => item.scope === scope)
    }
    // PMB-4.5: «draft» = aún no publicado (incluye propuestas), como en el
    // prototipo aprobado.
    if (estado === 'published') {
      list = list.filter((item) => item.workflowState === 'published')
    }
    if (estado === 'draft') {
      list = list.filter((item) => item.workflowState !== 'published')
    }
    if (tag) {
      list = list.filter((item) => item.tags.includes(tag))
    }
    const q = query.trim().toLowerCase()
    if (q) {
      list = list.filter((item) => {
        const type = orderedTypes.find((entry) => entry.id === item.typeId)
        const typeName = type ? typeLabel(type) : ''
        return (
          item.name.toLowerCase().includes(q) ||
          (item.summary ?? '').toLowerCase().includes(q) ||
          item.tags.some((value) => value.toLowerCase().includes(q)) ||
          item.typeKey.toLowerCase().includes(q) ||
          typeName.toLowerCase().includes(q)
        )
      })
    }
    if (sort === 'nombre') {
      list = [...list].sort((a, b) => a.name.localeCompare(b.name, 'en'))
    } else if (sort === 'uso') {
      list = [...list].sort(
        (a, b) => b.usageCount - a.usageCount || a.name.localeCompare(b.name, 'en'),
      )
    } else {
      list = [...list].sort(
        (a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt),
      )
    }
    return list
  }, [typeItems, scope, estado, tag, query, sort, orderedTypes])

  const selected = allItems.find((item) => item.id === selectedId) ?? null
  const selectedType = selected
    ? orderedTypes.find((type) => type.id === selected.typeId) ?? null
    : null
  const selectedPack = selected?.packKey
    ? packs?.find((pack) => pack.key === selected.packKey) ?? null
    : null
  const parent = selected?.parentId
    ? allItems.find((item) => item.id === selected.parentId) ?? null
    : null
  const system = orderedTypes[0]?.system ?? 'dnd5e'

  const countOf = (type: ContentTypeDto) =>
    allItems.filter((item) => item.typeId === type.id).length

  const childrenOf = (id: string) => allItems.filter((item) => item.parentId === id)
  const rootsOf = (type: ContentTypeDto) =>
    allItems.filter((item) => item.typeId === type.id && item.parentId === null)

  const toggleSet = (
    current: Set<string>,
    key: string,
    setter: (next: Set<string>) => void,
  ) => {
    const next = new Set(current)
    if (next.has(key)) next.delete(key)
    else next.add(key)
    setter(next)
  }

  const clearFilters = () => {
    setScope('todos')
    setQuery('')
    setEstado('todos')
    setTag(null)
    setVisibleCount(PAGE_SIZE)
  }

  /** Cambiar de tipo deja la rejilla limpia: sin filtros heredados ni
   *  páginas acumuladas que dejen el conjunto a cero sin explicación. */
  const selectType = (id: string) => {
    setTypeId(id)
    setSelectedId(null)
    setScope('todos')
    setQuery('')
    setEstado('todos')
    setTag(null)
    setVisibleCount(PAGE_SIZE)
  }

  const activeFilterCount =
    (scope !== 'todos' ? 1 : 0) + (estado !== 'todos' ? 1 : 0) + (tag ? 1 : 0)
  const hasSearchOrFilters = activeFilterCount > 0 || query.trim().length > 0
  // Con filtros el conteo es del conjunto filtrado; sin ellos, del servidor.
  const displayedCount = hasSearchOrFilters ? visible.length : typeItems.length

  const loadRevisions = async () => {
    if (revisionsOpen) {
      setRevisionsOpen(false)
      return
    }
    setRevisionsOpen(true)
    if (revisions || !selected) {
      return
    }
    setRevisionsBusy(true)
    setRevisionsError(null)
    try {
      const loaded = await apiFetch<ContentRevisionDto[]>(
        `/api/v1/content/items/${selected.id}/revisions`,
      )
      setRevisions(loaded)
    } catch (cause) {
      setRevisionsError(cause instanceof Error ? cause.message : t.biblioteca.revisionsError)
    } finally {
      setRevisionsBusy(false)
    }
  }

  // PMB-4.5 «Ver usos»: resumen sin snapshots; la visibilidad por membresía
  // la aplica el servidor. Se reabre sin recargar si ya se pidió.
  const loadUsages = async () => {
    if (showUsages) {
      setShowUsages(false)
      return
    }
    setShowUsages(true)
    if (usages !== null || !selected) {
      return
    }
    setUsagesBusy(true)
    setUsagesError(null)
    try {
      const loaded = await apiFetch<ItemUsageSummaryDto[]>(
        `/api/v1/content/items/${selected.id}/usages`,
      )
      setUsages(loaded)
    } catch (cause) {
      setUsagesError(cause instanceof Error ? cause.message : t.biblioteca.usagesError)
    } finally {
      setUsagesBusy(false)
    }
  }

  // PMB-4.5 «Duplicar»: sin endpoint propio — se copia lo visible con
  // POST /content/items en ámbito personal. Sin padre (la jerarquía se
  // decide en el editor) y sin campos privados ni historial.
  const confirmDuplicate = async () => {
    if (!selected) {
      return
    }
    setDuplicateOpen(false)
    setDuplicateError(null)
    try {
      await apiFetch<ContentItemDto>('/api/v1/content/items', {
        method: 'POST',
        body: {
          typeId: selected.typeId,
          name: `${selected.name} (copia)`,
          summary: selected.summary,
          description: selected.description,
          tags: selected.tags,
          imageId: selected.imageId,
          focal: selected.focal,
          properties: selected.properties,
          scope: 'user',
        },
      })
      setNotice(t.biblioteca.duplicateDone(`${selected.name} (copia)`))
      await refetchItems()
    } catch (cause) {
      setDuplicateError(cause instanceof Error ? cause.message : t.biblioteca.duplicateError)
    }
  }

  // Publicar: el mismo endpoint del editor; el servidor valida permisos y
  // estado. La UI solo deshabilita lo ya publicado y muestra su respuesta.
  const publishSelected = async () => {
    if (!selected || selected.workflowState === 'published') {
      return
    }
    setPublishBusy(true)
    setPublishError(null)
    try {
      await apiFetch<ContentItemDto>(`/api/v1/content/items/${selected.id}/publish`, {
        method: 'POST',
        body: { message: null },
      })
      setNotice(t.biblioteca.publishDone(selected.name))
      await refetchItems()
    } catch (cause) {
      setPublishError(cause instanceof Error ? cause.message : t.biblioteca.publishError)
    } finally {
      setPublishBusy(false)
    }
  }

  // EXP-43: `exportPack` descarga el zip del servidor vía objectURL.
  const downloadPack = async (key: string) => {
    setExportKey(key)
    setExportError(null)
    try {
      await exportPack(key)
    } catch (cause) {
      setExportError(cause instanceof Error ? cause.message : t.biblioteca.exportError)
    } finally {
      setExportKey(null)
    }
  }

  // --- Relaciones tipadas (PMB-4.6) -------------------------------------
  const nameOf = (id: string) => allItems.find((item) => item.id === id)?.name ?? id
  const relationKinds: readonly string[] = selectedType?.schema?.allowedRelations?.length
    ? selectedType.schema.allowedRelations
    : RELATION_KINDS
  const outgoing = relations?.filter((relation) => relation.fromItemId === selectedId) ?? []
  const incoming = relations?.filter((relation) => relation.toItemId === selectedId) ?? []

  const reloadRelations = async () => {
    if (!selected) {
      return
    }
    try {
      setRelations(await apiFetch<ContentRelationDto[]>(
        `/api/v1/content/items/${selected.id}/relations`))
    } catch (cause) {
      setRelationError(cause instanceof Error ? cause.message : t.biblioteca.relationError)
    }
  }

  const openRelationEditor = () => {
    setRelKind(relationKinds[0] ?? 'grants')
    setRelTarget(allItems.find((item) => item.id !== selectedId)?.id ?? '')
    setRelLabel('')
    setRelationError(null)
    setRelationEditorOpen(true)
  }

  const addRelation = async () => {
    if (!selected || !relTarget) {
      return
    }
    setRelationEditorOpen(false)
    setRelLabel('')
    try {
      await apiFetch<ContentRelationDto>(
        `/api/v1/content/items/${selected.id}/relations`,
        {
          method: 'POST',
          body: { kind: relKind, toItemId: relTarget, label: relLabel.trim() || null },
        })
      setNotice(t.biblioteca.relationAdded(relKind, nameOf(relTarget)))
      await reloadRelations()
    } catch (cause) {
      setRelationError(cause instanceof Error ? cause.message : t.biblioteca.relationError)
    }
  }

  const removeRelation = async (relation: ContentRelationDto) => {
    if (!selected) {
      return
    }
    try {
      await apiFetch<void>(
        `/api/v1/content/items/${selected.id}/relations/${relation.id}`,
        { method: 'DELETE' })
      setNotice(t.biblioteca.relationRemoved(relation.kind))
      await reloadRelations()
    } catch (cause) {
      setRelationError(cause instanceof Error ? cause.message : t.biblioteca.relationError)
    }
  }

  const navigateTo = (id: string) => {
    const target = allItems.find((item) => item.id === id)
    if (target) {
      setTypeId(target.typeId)
    }
    setSelectedId(id)
  }

  const renderNode = (item: ContentItemDto, depth: number) => {
    const children = childrenOf(item.id)
    const hasChildren = children.length > 0
    const isExpanded = expandedItems.has(item.id)
    return (
      <div key={item.id}>
        <button
          type="button"
          style={{ paddingLeft: `calc(var(--pv-space-3) + ${depth} * var(--pv-space-4))` }}
          className={`biblio-tree__item ${selectedId === item.id ? 'is-selected' : ''}`}
          onClick={() => {
            setSelectedId(item.id)
            if (hasChildren) {
              toggleSet(expandedItems, item.id, setExpandedItems)
            }
          }}
        >
          <span className="biblio-tree__label">{item.name}</span>
          {hasChildren && (
            <span className="biblio-tree__count" aria-hidden="true">
              {isExpanded ? '▾' : `▸ ${children.length}`}
            </span>
          )}
        </button>
        {hasChildren && isExpanded && children.map((child) => renderNode(child, depth + 1))}
      </div>
    )
  }

  if (error) {
    return (
      <div className="biblio">
        <div className="pv-list-error" role="alert">
          <p>{error}</p>
          <Button variant="secondary" onClick={() => { setError(null); setReloadKey((k) => k + 1) }}>
            {t.common.retry}
          </Button>
        </div>
      </div>
    )
  }

  if (types === null || items === null) {
    return (
      <div className="biblio">
        <div className="biblio-status" role="status">
          {t.biblioteca.loading}
        </div>
      </div>
    )
  }

  if (orderedTypes.length === 0) {
    return (
      <div className="biblio">
        <header className="biblio__head">
          <div>
            <h1 className="biblio__title">{t.biblioteca.title}</h1>
          </div>
        </header>
        <div className="biblio-empty">
          <p className="biblio-empty__title">{t.biblioteca.emptyLibraryTitle}</p>
          <p className="biblio-empty__text">{t.hero.bibliotecaText}</p>
        </div>
      </div>
    )
  }

  // Editor universal (PMB-4.4): sustituye al explorador mientras se edita o
  // se crea; al volver se recargan los items para reflejar lo guardado.
  if (editing) {
    return (
      <EditorView
        types={orderedTypes}
        items={allItems}
        packs={packs ?? []}
        target={editing}
        onBack={() => {
          setEditing(null)
          void refetchItems()
        }}
      />
    )
  }

  return (
    <div className="biblio">
      <header className="biblio__head">
        <div>
          <h1 className="biblio__title">{t.biblioteca.title}</h1>
          <p className="biblio__subtitle">{t.biblioteca.subtitle(system)}</p>
        </div>
        <div className="biblio__head-actions">
          <button type="button" className="pv-btn pv-btn--ghost"
            onClick={() => go({ kind: 'imagenes' })}>
            {t.biblioteca.openImages}
          </button>
          <button type="button" className="pv-btn pv-btn--ghost"
            onClick={() => go({ kind: 'dm' })}>
            {t.biblioteca.openDm}
          </button>
          <button
            type="button"
            className="pv-btn pv-btn--ghost"
            onClick={() => {
              setImportError(null)
              setImportOpen(true)
            }}
          >
            {t.biblioteca.importAction}
          </button>
          <button
            type="button"
            className="biblio__create"
            disabled={activeType === null}
            onClick={() => {
              if (activeType) {
                setEditing({ kind: 'create', typeId: activeType.id })
              }
            }}
          >
            {t.biblioteca.nuevoContenido}
          </button>
        </div>
      </header>

      {importReport && (
        <section className="biblio-import-report" role="status" aria-label={t.biblioteca.importReportLabel}>
          <strong>
            {t.biblioteca.importDone(importReport.packKey)}
          </strong>
          <span>{t.biblioteca.importSummary(
            importReport.types,
            importReport.created,
            importReport.updated,
            importReport.skipped,
          )}</span>
          {importReport.conflicts.length > 0 && (
            <>
              <span>{t.biblioteca.importConflicts(importReport.conflicts.length)}</span>
              <ul className="biblio-import-report__list">
                {importReport.conflicts.map((key) => <li key={key}>{key}</li>)}
              </ul>
            </>
          )}
          <button
            type="button"
            className="pv-btn pv-btn--ghost"
            onClick={() => setImportReport(null)}
          >
            {t.biblioteca.importClose}
          </button>
        </section>
      )}

      <section className="biblio-banner" aria-label={t.biblioteca.featuredLabel}>
        <div className="biblio-banner__art" aria-hidden="true">
          <img src="/art/fantasy/icon_dragon.png" alt="" />
          <img src="/art/fantasy/icon_bookholy.png" alt="" />
          <img src="/art/fantasy/icon_crystalblue.png" alt="" />
          <img src="/art/fantasy/icon_chest.png" alt="" />
        </div>
        <div className="biblio-banner__collections">
          {([
            ['race', 'Razas', 'icon_helmetgold.png'],
            ['creature', 'Criaturas', 'icon_dragon.png'],
            ['ability', 'Hechizos', 'icon_fireball.png'],
            ['item', 'Objetos', 'icon_sword.png'],
          ] as const).map(([key, label, art]) => {
            const type = orderedTypes.find((entry) => entry.key === key)
            if (!type) return null
            const count = countOf(type)
            return (
              <button type="button" key={key}
                aria-label={`Explorar ${key}`}
                className={`biblio-collection${typeId === type.id ? ' is-active' : ''}`}
                disabled={count === 0}
                title={count === 0 ? t.biblioteca.emptyCategory : undefined}
                onClick={() => selectType(type.id)}>
                <span className="biblio-collection__art">
                  <img src={`/art/fantasy/${art}`} alt="" aria-hidden="true" />
                </span>
                <span className="biblio-collection__text">
                  <strong>{label}</strong>
                  <small>{count} ítems</small>
                </span>
              </button>
            )
          })}
        </div>
      </section>

      <div className="biblio__body">
        {/* Izquierda — árbol de tipos y categorías (LIBRARY §2) */}
        <nav className="biblio-tree" aria-label={t.biblioteca.zoneTypes}>
          <h2 className="biblio-zone__title">{t.biblioteca.zoneTypes}</h2>
          {orderedTypes.map((type) => {
            const isExpanded = expandedTypes.has(type.id)
            const count = countOf(type)
            return (
              <div key={type.id}>
                <button
                  type="button"
                  className={`biblio-tree__type ${typeId === type.id ? 'is-active' : ''}`}
                  aria-expanded={isExpanded}
                  disabled={count === 0}
                  title={count === 0 ? t.biblioteca.emptyCategory : undefined}
                  onClick={() => {
                    selectType(type.id)
                    toggleSet(expandedTypes, type.id, setExpandedTypes)
                  }}
                >
                  <span className="biblio-tree__caret" aria-hidden="true">
                    {isExpanded ? '▾' : '▸'}
                  </span>
                  {contentIcon(type.key) && (
                    <img className="biblio-tree__icon" src={contentIcon(type.key)!} alt="" aria-hidden="true" />
                  )}
                  <span className="biblio-tree__label">{typeLabel(type)}</span>
                  <span className="biblio-tree__count">{count}</span>
                </button>
                {isExpanded && rootsOf(type).map((root) => renderNode(root, 1))}
              </div>
            )
          })}
          <button type="button" className="biblio-tree__add" disabled title={t.biblioteca.actionsNote}>
            {t.biblioteca.newCategory}
          </button>
          <div className="biblio-tree__packs">
            <h3 className="biblio-zone__title">{t.biblioteca.packsLabel}</h3>
            {(packs ?? []).map((pack) => (
              <div className="biblio-tree__pack" key={pack.key}>
                <span className="biblio-tree__label" title={pack.name}>{pack.name}</span>
                <button
                  type="button"
                  data-testid="library-export-pack"
                  disabled={exportKey === pack.key}
                  title={t.biblioteca.exportPackHint(pack.key)}
                  onClick={() => void downloadPack(pack.key)}
                >
                  {exportKey === pack.key
                    ? t.biblioteca.exportBusy
                    : t.biblioteca.exportPackAction}
                </button>
              </div>
            ))}
            {packs?.length === 0 && (
              <p className="biblio-tree__pack-empty">{t.biblioteca.packsNone}</p>
            )}
            {exportError && (
              <p className="biblio-status biblio-status--error" role="alert">
                {exportError}
              </p>
            )}
          </div>
        </nav>

        {/* Centro — rejilla de miniaturas compactas */}
        <section className="biblio-grid" aria-label={t.biblioteca.zoneGrid}>
          <div className="biblio-toolbar">
            <input
              className="biblio-toolbar__search"
              placeholder={t.biblioteca.searchPlaceholder}
              aria-label={t.biblioteca.searchPlaceholder}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <label className="biblio-toolbar__scope">
              {t.biblioteca.scopeLabel}
              <select
                aria-label={t.biblioteca.scopeLabel}
                value={scope}
                onChange={(event) => setScope(event.target.value as ScopeFilter)}
              >
                {(['todos', 'global', 'campaign', 'dm', 'user'] as const).map((value) => (
                  <option key={value} value={value}>{scopeLabel(value)}</option>
                ))}
              </select>
            </label>
            <button
              type="button"
              className="biblio-toolbar__filters-toggle"
              aria-expanded={filtersOpen}
              aria-label={filtersOpen ? t.biblioteca.filtersClose : t.biblioteca.filtersOpen}
              onClick={() => setFiltersOpen((current) => !current)}
            >
              {t.biblioteca.filters}
              {activeFilterCount > 0 && (
                <span className="biblio-toolbar__badge">{activeFilterCount}</span>
              )}
            </button>
            <label className="biblio-toolbar__sort">
              {t.biblioteca.sortLabel}
              <select value={sort} onChange={(event) => setSort(event.target.value as Sort)}>
                <option value="nombre">{t.biblioteca.sorts.nombre}</option>
                <option value="recientes">{t.biblioteca.sorts.recientes}</option>
                <option value="uso">{t.biblioteca.sorts.uso}</option>
              </select>
            </label>
            <span className="biblio-toolbar__count" data-testid="library-count">
              {t.biblioteca.count(displayedCount)}
            </span>
          </div>

          {filtersOpen && (
            <div className="biblio-toolbar__panel">
              <div className="biblio-toolbar__filters" role="group" aria-label={t.biblioteca.estadoLabel}>
                <span className="biblio-toolbar__label">{t.biblioteca.estadoLabel}</span>
                {(['todos', 'published', 'draft'] as const).map((value) => (
                  <button
                    key={value}
                    type="button"
                    className="biblio-chip"
                    aria-pressed={estado === value}
                    onClick={() => setEstado(value)}
                  >
                    {value === 'todos' ? t.biblioteca.scopes.todos : statusLabel(value)}
                  </button>
                ))}
              </div>
              <div className="biblio-toolbar__filters" role="group" aria-label={t.biblioteca.etiquetaLabel}>
                <span className="biblio-toolbar__label">{t.biblioteca.etiquetaLabel}</span>
                <button
                  type="button"
                  className="biblio-chip"
                  aria-pressed={tag === null}
                  onClick={() => setTag(null)}
                >
                  {t.biblioteca.tagAll}
                </button>
                {availableTags.map((value) => (
                  <button
                    key={value}
                    type="button"
                    className="biblio-chip"
                    aria-pressed={tag === value}
                    onClick={() => setTag(tag === value ? null : value)}
                  >
                    {value}
                  </button>
                ))}
                {availableTags.length === 0 && (
                  <span className="biblio-toolbar__label">{t.biblioteca.tagsNone}</span>
                )}
              </div>
              <button type="button" className="biblio-toolbar__clear" onClick={clearFilters}>
                {t.biblioteca.clearFilters}
              </button>
            </div>
          )}

          {typeItems.length === 0 ? (
            <div className="biblio-empty">
              <p className="biblio-empty__title">{t.biblioteca.emptyLibraryTitle}</p>
              <p className="biblio-empty__text">{t.hero.bibliotecaText}</p>
              <button type="button" className="biblio-empty__action"
                disabled={activeType === null}
                onClick={() => activeType && setEditing({ kind: 'create', typeId: activeType.id })}>
                {t.biblioteca.nuevoContenido}
              </button>
            </div>
          ) : visible.length === 0 ? (
            <div className="biblio-empty">
              <Icon name="magnifying-glass" />
              <p className="biblio-empty__title">{t.biblioteca.emptyFiltersTitle}</p>
              <p className="biblio-empty__text">{t.biblioteca.emptyFiltersText}</p>
              <button type="button" className="biblio-empty__action" onClick={clearFilters}>
                {t.biblioteca.clearFilters}
              </button>
            </div>
          ) : (
            <>
              <ul className="biblio-grid__tiles">
                {visible.slice(0, visibleCount).map((item) => (
                  <LibraryTile
                    key={item.id}
                    item={item}
                    selected={selectedId === item.id}
                    onSelect={setSelectedId}
                  />
                ))}
              </ul>
              {visible.length > visibleCount && (
                <div className="biblio-grid__more">
                  <button
                    type="button"
                    data-testid="library-show-more"
                    onClick={() => setVisibleCount((current) => current + PAGE_SIZE)}
                  >
                    {t.biblioteca.showMore(visible.length - visibleCount)}
                  </button>
                </div>
              )}
            </>
          )}
        </section>

        {/* Derecha — inspector */}
        <aside className="biblio-inspector" aria-label={t.biblioteca.zoneInspector}>
          <h2 className="biblio-zone__title">{t.biblioteca.zoneInspector}</h2>
          {!selected ? (
            <p className="biblio-inspector__empty">{t.biblioteca.selectHint}</p>
          ) : (
            <div className="biblio-inspector__body">
              <h3 className="biblio-inspector__name">{selected.name}</h3>
              <code className="biblio-inspector__key">
                {selected.packItemKey ?? `${selected.typeKey}/${selected.id}`}
              </code>
              <div className="biblio-inspector__tags">
                {selectedType && <span className="biblio-tag">{typeLabel(selectedType)}</span>}
                <span className={`biblio-tag ${selected.scope !== 'global' ? 'is-warn' : ''}`}>
                  {scopeLabel(selected.scope)}
                </span>
                <span
                  className={`biblio-tag ${selected.workflowState !== 'published' ? 'is-warn' : ''}`}
                >
                  {statusLabel(selected.workflowState)}
                </span>
                {selected.currentRevision > 0 && (
                  <span className="biblio-tag">
                    {t.biblioteca.revisionBadge(selected.currentRevision)}
                  </span>
                )}
                {selected.tags.map((tag) => (
                  <span key={tag} className="biblio-tag">
                    {tag}
                  </span>
                ))}
              </div>

              {selected.summary && (
                <p className="biblio-inspector__summary">{selected.summary}</p>
              )}
              {selected.description && (
                <>
                  <h4 className="biblio-inspector__section">{t.biblioteca.description}</h4>
                  <p className="biblio-inspector__summary">{selected.description}</p>
                </>
              )}

              <h4 className="biblio-inspector__section">{t.biblioteca.properties}</h4>
              <ul className="biblio-inspector__props">
                {Object.entries(selected.properties).length === 0 ? (
                  <li>—</li>
                ) : (
                  Object.entries(selected.properties).map(([key, value]) => (
                    <li key={key}>
                      {key}: <strong>{formatPropertyValue(value)}</strong>
                    </li>
                  ))
                )}
              </ul>

              {(parent || Object.keys(selected.inheritance).length > 0) && (
                <>
                  <h4 className="biblio-inspector__section">{t.biblioteca.inheritance}</h4>
                  {parent && (
                    <p className="biblio-inspector__inherit">
                      {t.biblioteca.extends(parent.name)}
                    </p>
                  )}
                  {Object.entries(selected.inheritance).map(([field, mode]) => (
                    <code key={field} className="biblio-inspector__key">
                      {field} → {mode}
                    </code>
                  ))}
                </>
              )}

              <h4 className="biblio-inspector__section">{t.biblioteca.relations}</h4>
              {relations === null ? (
                <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
              ) : outgoing.length === 0 && incoming.length === 0 ? (
                <p className="biblio-inspector__note">{t.biblioteca.relationsNone}</p>
              ) : (
                <ul className="biblio-rel__list">
                  {outgoing.map((relation) => (
                    <li key={relation.id}>
                      <span className="biblio-rel__kind">
                        {relationKindLabel(relation.kind)}
                      </span>
                      <span aria-hidden="true"> → </span>
                      <button
                        type="button"
                        className="biblio-rel__link"
                        onClick={() => navigateTo(relation.toItemId)}
                      >
                        {nameOf(relation.toItemId)}
                      </button>
                      {relation.label && (
                        <span className="biblio-rel__label"> · {relation.label}</span>
                      )}
                    </li>
                  ))}
                  {incoming.map((relation) => (
                    <li key={relation.id}>
                      <span className="biblio-rel__kind">
                        {relationKindLabel(relation.kind)}
                      </span>
                      <span aria-hidden="true"> ← </span>
                      <button
                        type="button"
                        className="biblio-rel__link"
                        onClick={() => navigateTo(relation.fromItemId)}
                      >
                        {nameOf(relation.fromItemId)}
                      </button>
                      {relation.label && (
                        <span className="biblio-rel__label"> · {relation.label}</span>
                      )}
                    </li>
                  ))}
                </ul>
              )}
              <button
                type="button"
                className="biblio-rel__edit"
                onClick={openRelationEditor}
              >
                {t.biblioteca.relationEdit}
              </button>
              <p className="biblio-inspector__note">{t.biblioteca.relationsHint}</p>
              {relationError && (
                <p className="biblio-status biblio-status--error" role="alert">
                  {relationError}
                </p>
              )}

              <h4 className="biblio-inspector__section">{t.biblioteca.provenance}</h4>
              {selectedPack ? (
                <div className="biblio-inspector__pack">
                  <span>{selectedPack.name}</span>
                  <span className="biblio-inspector__license">
                    v{selectedPack.version} ·{' '}
                    {selectedPack.licenseUrl ? (
                      <a href={selectedPack.licenseUrl} target="_blank" rel="noreferrer">
                        {selectedPack.license}
                      </a>
                    ) : (
                      selectedPack.license
                    )}
                  </span>
                  {selectedPack.attribution.map((line) => (
                    <p key={line} className="biblio-inspector__attribution">
                      {line}
                    </p>
                  ))}
                </div>
              ) : (
                <p className="biblio-inspector__pack">{t.biblioteca.noPack}</p>
              )}

              <h4 className="biblio-inspector__section">{t.biblioteca.revisions}</h4>
              <button
                type="button"
                className="biblio-revisions__toggle"
                aria-expanded={revisionsOpen}
                onClick={() => void loadRevisions()}
              >
                {revisionsOpen ? t.biblioteca.hideRevisions : t.biblioteca.showRevisions}
              </button>
              {revisionsOpen && (
                <>
                  {revisionsBusy && (
                    <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
                  )}
                  {revisionsError && (
                    <p className="biblio-status biblio-status--error" role="alert">
                      {revisionsError}
                    </p>
                  )}
                  {!revisionsBusy && revisions !== null && revisions.length === 0 && (
                    <p className="biblio-inspector__empty">{t.biblioteca.revisionsNone}</p>
                  )}
                  {!revisionsBusy && revisions && revisions.length > 0 && (
                    <ul className="biblio-revisions__list">
                      {revisions.map((revision) => (
                        <li key={revision.id}>
                          {t.biblioteca.revision(revision.number)}
                          {revision.message ? ` · ${revision.message}` : ''}
                          <span className="biblio-revisions__meta">
                            {new Date(revision.createdAt).toLocaleString('es-ES')}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              )}

              <div className="biblio-inspector__actions biblio-inspector__actions--live">
                <button
                  type="button"
                  disabled={selected.scope === 'global'}
                  title={selected.scope === 'global' ? t.editor.readOnlyBanner : undefined}
                  onClick={() => setEditing({ kind: 'edit', itemId: selected.id })}
                >
                  {t.biblioteca.actionEdit}
                </button>
                <button type="button" onClick={() => setDuplicateOpen(true)}>
                  {t.biblioteca.actionDuplicate}
                </button>
                <button
                  type="button"
                  data-testid="library-publish"
                  disabled={publishBusy || selected.workflowState === 'published'}
                  title={selected.workflowState === 'published'
                    ? t.biblioteca.publishAlready
                    : t.biblioteca.publishHint}
                  aria-busy={publishBusy}
                  onClick={() => void publishSelected()}
                >
                  {publishBusy ? t.biblioteca.publishBusy : t.biblioteca.actionPublish}
                </button>
                <button
                  type="button"
                  aria-expanded={showUsages}
                  onClick={() => void loadUsages()}
                >
                  {t.biblioteca.actionUsages}
                </button>
              </div>
              <p className="biblio-inspector__note">{t.biblioteca.actionsNote}</p>
              {duplicateError && (
                <p className="biblio-status biblio-status--error" role="alert">
                  {duplicateError}
                </p>
              )}
              {publishError && (
                <p className="biblio-status biblio-status--error" role="alert">
                  {publishError}
                </p>
              )}

              {showUsages && (
                <section className="biblio-usos" aria-label={t.biblioteca.usagesZone}>
                  <h4 className="biblio-inspector__section">{t.biblioteca.usagesHeading}</h4>
                  {usagesBusy && (
                    <p className="biblio-inspector__empty">{t.biblioteca.usagesLoading}</p>
                  )}
                  {usagesError && (
                    <p className="biblio-status biblio-status--error" role="alert">
                      {usagesError}
                    </p>
                  )}
                  {!usagesBusy && usages !== null && usages.length === 0 && (
                    <p className="biblio-inspector__note">{t.biblioteca.usagesNone}</p>
                  )}
                  {!usagesBusy && usages && usages.length > 0 && (
                    <ul className="biblio-usos__list">
                      {usages.map((usage, index) => (
                        <li key={`${usage.actorName}-${usage.createdAt}-${index}`}>
                          <strong>{usage.actorName}</strong>
                          <span className="biblio-usos__meta">
                            {usage.campaignName} ·{' '}
                            {t.biblioteca.revisionBadge(usage.revisionNumber)} ·{' '}
                            {new Date(usage.createdAt).toLocaleDateString('es-ES')}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                  <p className="biblio-inspector__note">{t.biblioteca.usagesNote}</p>
                </section>
              )}

              {notice && (
                <p className="biblio-inspector__note" role="status">
                  {notice}
                </p>
              )}
            </div>
          )}
        </aside>
      </div>

      {selected && (
        <ConfirmDialog
          open={duplicateOpen}
          title={t.biblioteca.duplicateTitle(selected.name)}
          text={t.biblioteca.duplicateText}
          confirmLabel={t.biblioteca.duplicateConfirm}
          cancelLabel={t.editor.actions.cancel}
          onConfirm={() => void confirmDuplicate()}
          onCancel={() => setDuplicateOpen(false)}
        />
      )}

      {selected && relationEditorOpen && (
        <div
          className="pv-confirm__overlay"
          onClick={() => setRelationEditorOpen(false)}
        >
          <div
            className="pv-confirm biblio-rel-editor"
            role="dialog"
            aria-modal="true"
            aria-label={t.biblioteca.relationEditorTitle(selected.name)}
            onClick={(event) => event.stopPropagation()}
          >
            <h2 className="pv-confirm__title">
              {t.biblioteca.relationEditorTitle(selected.name)}
            </h2>
            <div className="biblio-rel-editor__fields">
              <label>
                {t.biblioteca.relationKind}
                <select
                  value={relKind}
                  onChange={(event) => setRelKind(event.target.value)}
                >
                  {relationKinds.map((kind) => (
                    <option key={kind} value={kind}>
                      {relationKindLabel(kind)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                {t.biblioteca.relationTarget}
                <select
                  value={relTarget}
                  onChange={(event) => setRelTarget(event.target.value)}
                >
                  {allItems
                    .filter((item) => item.id !== selected.id)
                    .map((item) => (
                      <option key={item.id} value={item.id}>{item.name}</option>
                    ))}
                </select>
              </label>
              <label>
                {t.biblioteca.relationLabel}
                <input
                  value={relLabel}
                  onChange={(event) => setRelLabel(event.target.value)}
                  placeholder={t.biblioteca.relationLabel}
                />
              </label>
            </div>
            {outgoing.length > 0 && (
              <ul className="biblio-rel__list">
                {outgoing.map((relation) => (
                  <li key={relation.id}>
                    <span className="biblio-rel__kind">
                      {relationKindLabel(relation.kind)}
                    </span>
                    <span aria-hidden="true"> → </span>
                    {nameOf(relation.toItemId)}
                    <button
                      type="button"
                      className="biblio-rel__remove"
                      onClick={() => void removeRelation(relation)}
                    >
                      {t.biblioteca.relationRemove}
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {relationError && (
              <p className="biblio-status biblio-status--error" role="alert">
                {relationError}
              </p>
            )}
            <div className="pv-confirm__actions">
              <button
                type="button"
                className="pv-btn pv-btn--ghost"
                onClick={() => setRelationEditorOpen(false)}
              >
                {t.biblioteca.relationClose}
              </button>
              <button
                type="button"
                className="pv-btn pv-btn--primary"
                onClick={() => void addRelation()}
              >
                {t.biblioteca.relationAdd}
              </button>
            </div>
          </div>
        </div>
      )}

      <Dialog
        open={importOpen}
        title={t.biblioteca.importTitle}
        closeLabel={t.biblioteca.importClose}
        onClose={() => {
          if (!importBusy) {
            setImportOpen(false)
          }
        }}
        actions={
          <>
            <button
              type="button"
              className="pv-btn pv-btn--ghost"
              disabled={importBusy}
              onClick={() => setImportOpen(false)}
            >
              {t.biblioteca.importCancel}
            </button>
            <button
              type="button"
              className="pv-btn pv-btn--primary"
              disabled={!importFile || importBusy}
              aria-busy={importBusy}
              onClick={() => void submitImport()}
            >
              {importBusy ? t.biblioteca.importBusy : t.biblioteca.importSubmit}
            </button>
          </>
        }
      >
        <p className="biblio-inspector__note">{t.biblioteca.importHint}</p>
        <label className="biblio-import__field">
          {t.biblioteca.importFile}
          <input
            type="file"
            accept=".pvttpkg"
            disabled={importBusy}
            onChange={(event) => setImportFile(event.target.files?.[0] ?? null)}
          />
        </label>
        <label className="biblio-import__field">
          {t.biblioteca.importStrategy}
          <select
            value={importStrategy}
            disabled={importBusy}
            onChange={(event) => setImportStrategy(event.target.value as ImportStrategy)}
          >
            {IMPORT_STRATEGIES.map((strategy) => (
              <option key={strategy} value={strategy}>
                {t.biblioteca.importStrategies[strategy]}
              </option>
            ))}
          </select>
        </label>
        <p className="biblio-inspector__note">{t.biblioteca.importOperatorNote}</p>
        {importError && (
          <p className="biblio-status biblio-status--error" role="alert">
            {importError}
          </p>
        )}
      </Dialog>
    </div>
  )
}
