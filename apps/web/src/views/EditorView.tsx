import { useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiError } from '../api/client'
import { useImageUrl } from '../api/portraits'
import { t } from '../i18n'
import type {
  ContentItemDto,
  ContentPackDto,
  ContentTypeDto,
  ContentRelationDto,
  ContentRevisionDto,
  ImageAssetDto,
} from '../api/types'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Field, Input } from '../ui/Field'
import { coverWithImage } from '../ui/contentIcons'
import './library.css'
import './editor.css'

/**
 * Editor universal de contenido (PMB-4.4, LIBRARY.md §3): formulario por
 * secciones adaptado al TypeSchema del tipo, con herencia por campo,
 * estado/versionado a la derecha y árbol de contexto a la izquierda
 * (prototipo aprobado 2026-11-11, docs/visual/screens/pmb-4.4-prototipo.md).
 * El servidor es la autoridad: la UI solo refleja sus decisiones (permisos,
 * validación de esquema, ámbitos) y sus errores de campo.
 */

export type EditorTarget =
  | { kind: 'edit'; itemId: string }
  | { kind: 'create'; typeId: string }

type InheritanceMode = 'inherit' | 'override' | 'extend'
type SchemaField = { key: string; type: string; required?: boolean }

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

/** Kinds del modelo (CONTENT_MODEL §8); el servidor valida los permitidos. */
const RELATION_KINDS = [
  'grants', 'requires', 'excludes', 'memberOf', 'locatedIn', 'leadsTo', 'owns',
] as const

// --- utilidades --------------------------------------------------------

function typeLabel(type: ContentTypeDto): string {
  const labels = t.biblioteca.typeLabels as Record<string, string>
  return labels[type.key] ?? type.displayName
}

function scopeLabel(scope: string): string {
  const scopes = t.biblioteca.scopes as Record<string, string>
  return scopes[scope] ?? scope
}

function statusLabel(state: string): string {
  const status = t.biblioteca.status as Record<string, string>
  return status[state] ?? state
}

function relationKindLabel(kind: string): string {
  const labels = t.biblioteca.relationKinds as Record<string, string>
  return labels[kind] ?? kind
}

function schemaFields(type: ContentTypeDto): SchemaField[] {
  return type.schema?.fields ?? []
}

function parseList(text: string): string[] {
  return text
    .split(',')
    .map((entry) => entry.trim())
    .filter(Boolean)
}

/** Serialización estable (claves ordenadas) para comparar valores. */
function stableJson(value: unknown): string {
  const walk = (entry: unknown): unknown => {
    if (Array.isArray(entry)) {
      return entry.map(walk)
    }
    if (entry && typeof entry === 'object') {
      const source = entry as Record<string, unknown>
      const out: Record<string, unknown> = {}
      for (const key of Object.keys(source).sort()) {
        out[key] = walk(source[key])
      }
      return out
    }
    return entry
  }
  return JSON.stringify(walk(value)) ?? ''
}

/** Valor legible corto de un valor heredado (para los ayudantes de modo). */
function briefOf(value: unknown): string {
  if (value === undefined) {
    return ''
  }
  if (Array.isArray(value)) {
    return value.length === 0 ? t.editor.listEmpty : t.editor.listTotal(value.length)
  }
  if (value && typeof value === 'object') {
    return ''
  }
  return String(value)
}

function formatValue(value: unknown): string {
  if (value === undefined || value === null || value === '') {
    return '—'
  }
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return '—'
    }
    if (
      value.every((entry) => entry && typeof entry === 'object' && 'ability' in entry)
    ) {
      return value
        .map((entry) => {
          const bonus = entry as { ability: string; bonus: number }
          return `${bonus.ability} +${bonus.bonus}`
        })
        .join(', ')
    }
    return value
      .map((entry) =>
        entry && typeof entry === 'object' ? JSON.stringify(entry) : String(entry),
      )
      .join(' · ')
  }
  return String(value)
}

/** El campo del error del servidor ('properties.speed' → 'speed'). */
function controlField(field: string | undefined): string | undefined {
  if (!field) {
    return undefined
  }
  return field.startsWith('properties.')
    ? field.slice('properties.'.length)
    : field
}

type SaveError = { title: string; detail: string; field?: string }

function mapError(cause: unknown, fallbackTitle: string): SaveError {
  if (cause instanceof ApiError) {
    return {
      title: fallbackTitle,
      detail:
        cause.status === 403 ? t.editor.actions.forbidden : cause.message,
      field: controlField(cause.field),
    }
  }
  return {
    title: fallbackTitle,
    detail: cause instanceof Error ? cause.message : t.editor.loadError,
    field: undefined,
  }
}

// --- estado del formulario --------------------------------------------

type FormState = {
  name: string
  summary: string
  description: string
  tagsText: string
  imageId: string | null
  parentId: string | null
  properties: Record<string, unknown>
  inheritance: Record<string, InheritanceMode>
  isPrivate: boolean
  privateFieldsText: string
}

function buildForm(item: ContentItemDto | null): FormState {
  if (!item) {
    return {
      name: '',
      summary: '',
      description: '',
      tagsText: '',
      imageId: null,
      parentId: null,
      properties: {},
      inheritance: {},
      isPrivate: false,
      privateFieldsText: '',
    }
  }
  return {
    name: item.name,
    summary: item.summary ?? '',
    description: item.description ?? '',
    tagsText: item.tags.join(', '),
    imageId: item.imageId,
    parentId: item.parentId,
    properties: structuredClone(item.properties) as Record<string, unknown>,
    inheritance: { ...item.inheritance } as Record<string, InheritanceMode>,
    isPrivate: item.isPrivate,
    privateFieldsText: item.privateFields.join(', '),
  }
}

/** Las cadenas vacías se envían como ausentes (igual que en el servidor). */
function buildProperties(
  raw: Record<string, unknown>,
  fields: SchemaField[],
): Record<string, unknown> {
  const out: Record<string, unknown> = {}
  for (const field of fields) {
    const value = raw[field.key]
    if (value === undefined || value === null) {
      continue
    }
    if (typeof value === 'string' && value === '') {
      continue
    }
    out[field.key] = value
  }
  return out
}

/** Solo los campos cambiados (evita pisar datos que la API oculta). */
function buildPatch(
  initial: ContentItemDto,
  form: FormState,
  fields: SchemaField[],
  hasParent: boolean,
): Record<string, unknown> {
  const body: Record<string, unknown> = {}
  if (form.name !== initial.name) {
    body.name = form.name
  }
  if (form.summary !== (initial.summary ?? '')) {
    body.summary = form.summary
  }
  if (form.description !== (initial.description ?? '')) {
    body.description = form.description
  }
  const tags = parseList(form.tagsText)
  if (stableJson(tags) !== stableJson(initial.tags)) {
    body.tags = tags
  }
  if (form.imageId !== initial.imageId) {
    body.imageId = form.imageId
  }
  if (form.parentId !== initial.parentId) {
    body.parentId = form.parentId ?? EMPTY_GUID
  }
  const properties = buildProperties(form.properties, fields)
  if (stableJson(properties) !== stableJson(initial.properties)) {
    body.properties = properties
  }
  const inheritance = hasParent ? form.inheritance : {}
  if (stableJson(inheritance) !== stableJson(initial.inheritance)) {
    body.inheritance = inheritance
  }
  if (form.isPrivate !== initial.isPrivate) {
    body.isPrivate = form.isPrivate
  }
  const privateFields = parseList(form.privateFieldsText)
  if (stableJson(privateFields) !== stableJson(initial.privateFields)) {
    body.privateFields = privateFields
  }
  return body
}

// --- controles ---------------------------------------------------------

function ModeGroup({
  label,
  mode,
  disabled,
  onChange,
}: {
  label: string
  mode: InheritanceMode
  disabled: boolean
  onChange: (mode: InheritanceMode) => void
}) {
  return (
    <span className="editor-modes" role="group" aria-label={t.editor.modeGroup(label)}>
      {(['inherit', 'override', 'extend'] as const).map((value) => (
        <button
          key={value}
          type="button"
          className={`editor-modes__btn ${mode === value ? 'is-on' : ''}`}
          aria-pressed={mode === value}
          disabled={disabled}
          onClick={() => onChange(value)}
        >
          {t.editor.modes[value]}
        </button>
      ))}
    </span>
  )
}

type PropFieldProps = {
  field: SchemaField
  value: unknown
  mode: InheritanceMode
  parentName: string | null
  parentValue: unknown
  hasParent: boolean
  readOnly: boolean
  error?: string
  /** Claves de las filas de objetos (derivadas del esquema real implícito). */
  objectKeys?: string[]
  onChange: (value: unknown) => void
  onMode: (mode: InheritanceMode) => void
}

function PropField({
  field,
  value,
  mode,
  parentName,
  parentValue,
  hasParent,
  readOnly,
  error,
  objectKeys,
  onChange,
  onMode,
}: PropFieldProps) {
  const id = `${field.key}`
  const inherited = hasParent && mode === 'inherit'
  const describedBy = error ? `${id}-error` : `${id}-helper`
  const required = field.required !== false

  const brief = briefOf(parentValue)
  const modeHelper =
    hasParent && mode !== 'inherit' && parentName
      ? mode === 'extend'
        ? t.editor.modeExtend(brief)
        : t.editor.modeOverride(brief)
      : null

  const rows = Array.isArray(value) ? (value as unknown[]) : []
  const setRow = (index: number, entry: unknown) => {
    const next = [...rows]
    next[index] = entry
    onChange(next)
  }

  let control = null
  if (inherited) {
    control = (
      <output className="editor-prop__inherited" id={id}>
        {!parentName || parentValue === undefined ? (
          `${t.editor.inheritedFrom}${parentName ?? ''}`
        ) : (
          <>
            {t.editor.inheritedFrom}
            {parentName}: <strong>{formatValue(parentValue)}</strong>
          </>
        )}
      </output>
    )
  } else if (field.type === 'list') {
    control = (
      <div
        className="editor-list"
        role="group"
        id={id}
        aria-labelledby={`${id}-label`}
        aria-describedby={describedBy}
        tabIndex={-1}
        data-editor-field={field.key}
      >
        {rows.map((entry, index) => {
          const isObject =
            objectKeys &&
            objectKeys.length > 0 &&
            entry !== null &&
            typeof entry === 'object' &&
            !Array.isArray(entry)
          return (
            <div
              key={index}
              className={`editor-list__row ${isObject ? 'editor-list__row--keys' : ''}`}
            >
              {isObject ? (
                objectKeys.map((key) => {
                  const cell = (entry as Record<string, unknown>)[key]
                  const numeric = typeof cell === 'number'
                  return (
                    <Input
                      key={key}
                      type={numeric ? 'number' : 'text'}
                      aria-label={`${t.editor.rowValue(index + 1)} · ${key}`}
                      aria-required={required}
                      disabled={readOnly}
                      value={cell === undefined || cell === null ? '' : String(cell)}
                      onChange={(event) => {
                        const source = { ...(entry as Record<string, unknown>) }
                        source[key] = numeric
                          ? event.target.value === ''
                            ? undefined
                            : Number(event.target.value)
                          : event.target.value
                        setRow(index, source)
                      }}
                    />
                  )
                })
              ) : (
                <Input
                  aria-label={`${t.editor.row(index + 1)}`}
                  aria-required={required}
                  disabled={readOnly}
                  value={typeof entry === 'string' ? entry : JSON.stringify(entry ?? '')}
                  onChange={(event) => setRow(index, event.target.value)}
                />
              )}
              <button
                type="button"
                className="editor-list__remove"
                aria-label={t.editor.removeRow(index + 1)}
                disabled={readOnly}
                onClick={() => onChange(rows.filter((_, i) => i !== index))}
              >
                ×
              </button>
            </div>
          )
        })}
        <button
          type="button"
          className="editor-list__add"
          disabled={readOnly}
          onClick={() =>
            onChange(
              objectKeys && objectKeys.length > 0
                ? [
                    ...rows,
                    Object.fromEntries(
                      objectKeys.map((key) => [key, key === 'bonus' ? 1 : '']),
                    ),
                  ]
                : [...rows, ''],
            )
          }
        >
          {t.editor.addRow}
        </button>
      </div>
    )
  } else if (field.type === 'number') {
    control = (
      <Input
        id={id}
        type="number"
        data-editor-field={field.key}
        aria-required={required}
        aria-invalid={Boolean(error)}
        aria-describedby={describedBy}
        disabled={readOnly}
        value={typeof value === 'number' ? String(value) : typeof value === 'string' ? value : ''}
        onChange={(event) => onChange(event.target.value === '' ? '' : Number(event.target.value))}
      />
    )
  } else if (field.type === 'boolean') {
    control = (
      <input
        id={id}
        type="checkbox"
        data-editor-field={field.key}
        aria-required={required}
        aria-describedby={describedBy}
        disabled={readOnly}
        checked={value === true}
        onChange={(event) => onChange(event.target.checked)}
      />
    )
  } else {
    control = (
      <Input
        id={id}
        data-editor-field={field.key}
        aria-required={required}
        aria-invalid={Boolean(error)}
        aria-describedby={describedBy}
        disabled={readOnly}
        value={typeof value === 'string' ? value : value === undefined ? '' : String(value)}
        onChange={(event) => onChange(event.target.value)}
      />
    )
  }

  return (
    <div className="editor-prop">
      <div className="editor-prop__head">
        <span className="editor-prop__labelrow">
          {inherited || field.type === 'list' || field.type === 'boolean' ? (
            <span className="pv-field__label" id={`${id}-label`}>
              {field.key}
            </span>
          ) : (
            <label className="pv-field__label" htmlFor={id}>
              {field.key}
            </label>
          )}
          {required && (
            <span className="editor-prop__req" aria-hidden="true" title={t.editor.required}>
              *
            </span>
          )}
        </span>
        {hasParent && (
          <ModeGroup
            label={field.key}
            mode={mode}
            disabled={readOnly}
            onChange={onMode}
          />
        )}
      </div>
      {control}
      {error ? (
        <span className="pv-field__error" id={`${id}-error`}>
          {error}
        </span>
      ) : (
        <span className="pv-field__helper" id={`${id}-helper`}>
          {modeHelper ?? '\u00a0'}
        </span>
      )}
    </div>
  )
}

// --- panel (formulario + estado) --------------------------------------

type EditorPaneProps = {
  item: ContentItemDto | null
  type: ContentTypeDto
  items: ContentItemDto[]
  packs: ContentPackDto[]
  onDirtyChange: (dirty: boolean) => void
  onSaved: (item: ContentItemDto) => void
  onCreated: (item: ContentItemDto) => void
}

function EditorPane({
  item,
  type,
  items,
  packs,
  onDirtyChange,
  onSaved,
  onCreated,
}: EditorPaneProps) {
  const creating = item === null
  const readOnly = !creating && item.scope === 'global'
  const fields = schemaFields(type)

  const [initial, setInitial] = useState(item)
  const [form, setForm] = useState<FormState>(() => buildForm(item))
  const [busy, setBusy] = useState(false)
  const [saved, setSaved] = useState(false)
  const [saveError, setSaveError] = useState<SaveError | null>(null)
  const [publishOpen, setPublishOpen] = useState(false)
  const [proposeBusy, setProposeBusy] = useState(false)
  const [revisions, setRevisions] = useState<ContentRevisionDto[] | null>(null)
  const [revisionsError, setRevisionsError] = useState<string | null>(null)
  const [images, setImages] = useState<ImageAssetDto[] | null>(null)
  const [imagesError, setImagesError] = useState<string | null>(null)
  // La misma resolución de portada que la rejilla: imagen elegida → derivada.
  const previewImageUrl = useImageUrl(form.imageId)
  const previewCover = coverWithImage(
    previewImageUrl, type.key, form.name, parseList(form.tagsText),
  )
  const [relKind, setRelKind] = useState<string>('grants')
  const [relTarget, setRelTarget] = useState('')
  const [relBusy, setRelBusy] = useState(false)
  const [relNotice, setRelNotice] = useState<string | null>(null)
  const [relError, setRelError] = useState<string | null>(null)

  const parent = form.parentId
    ? items.find((entry) => entry.id === form.parentId) ?? null
    : null
  const hasParent = parent !== null

  const patch = useMemo(
    () => (initial ? buildPatch(initial, form, fields, hasParent) : {}),
    [initial, form, fields, hasParent],
  )
  const dirty = !creating && Object.keys(patch).length > 0

  useEffect(() => {
    onDirtyChange(dirty)
  }, [dirty, onDirtyChange])

  // El servidor devuelve los errores con su campo: se reflejan y se enfoca.
  useEffect(() => {
    const field = saveError?.field
    if (!field) {
      return
    }
    document
      .querySelector<HTMLElement>(`[data-editor-field="${field}"]`)
      ?.focus()
  }, [saveError])

  // Revisiones del item en edición (una petición por elemento).
  useEffect(() => {
    if (creating || !item) {
      setRevisions(null)
      return
    }
    let cancelled = false
    void (async () => {
      try {
        const loaded = await apiFetch<ContentRevisionDto[]>(
          `/api/v1/content/items/${item.id}/revisions`,
        )
        if (!cancelled) {
          setRevisions(loaded)
        }
      } catch (cause) {
        if (!cancelled) {
          setRevisionsError(
            cause instanceof Error ? cause.message : t.biblioteca.revisionsError,
          )
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [creating, item])

  // Imágenes disponibles para «Imagen principal» (PMB-4.7): la biblioteca
  // es la autoridad de los assets; aquí solo se elige su imageId.
  useEffect(() => {
    let cancelled = false
    void apiFetch<ImageAssetDto[]>('/api/v1/images')
      .then((loaded) => {
        if (!cancelled) {
          setImages(loaded)
        }
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setImagesError(cause instanceof Error ? cause.message : t.biblioteca.error)
        }
      })
    return () => {
      cancelled = true
    }
  }, [])

  const setProperty = (key: string, value: unknown) => {
    setForm((current) => {
      const properties = { ...current.properties }
      if (value === '' || value === null || value === undefined) {
        delete properties[key]
      } else {
        properties[key] = value
      }
      return { ...current, properties }
    })
    setSaved(false)
  }

  const setMode = (key: string, mode: InheritanceMode) => {
    setForm((current) => ({
      ...current,
      inheritance: { ...current.inheritance, [key]: mode },
    }))
    setSaved(false)
  }

  const update = (patchForm: Partial<FormState>) => {
    setForm((current) => ({ ...current, ...patchForm }))
    setSaved(false)
  }

  // Padres posibles: mismo tipo, sin self ni descendientes (el servidor
  // revalida ciclos y profundidad — PMB-4.3).
  const excludedParents = useMemo(() => {
    const excluded = new Set<string>()
    if (!item) {
      return excluded
    }
    excluded.add(item.id)
    let frontier = [item.id]
    while (frontier.length > 0) {
      const next: string[] = []
      for (const id of frontier) {
        for (const entry of items) {
          if (entry.parentId === id && !excluded.has(entry.id)) {
            excluded.add(entry.id)
            next.push(entry.id)
          }
        }
      }
      frontier = next
    }
    return excluded
  }, [item, items])

  const siblingParents = items.filter(
    (entry) => entry.typeId === type.id && !excludedParents.has(entry.id),
  )

  const pack = !creating && item.packKey
    ? packs.find((entry) => entry.key === item.packKey) ?? null
    : null

  // El servidor valida el kind contra el esquema del tipo y la visibilidad.
  const relationKinds: readonly string[] = type.schema?.allowedRelations?.length
    ? type.schema.allowedRelations
    : RELATION_KINDS
  const activeKind = relationKinds.includes(relKind) ? relKind : relationKinds[0] ?? 'grants'
  const nameOf = (id: string) => items.find((entry) => entry.id === id)?.name ?? id

  const addRelation = async () => {
    if (!initial || !relTarget || relBusy) {
      return
    }
    setRelBusy(true)
    setRelError(null)
    setRelNotice(null)
    try {
      const relation = await apiFetch<ContentRelationDto>(
        `/api/v1/content/items/${initial.id}/relations`,
        { method: 'POST', body: { kind: activeKind, toItemId: relTarget, label: null } },
      )
      setRelNotice(t.biblioteca.relationAdded(relation.kind, nameOf(relation.toItemId)))
      setRelTarget('')
    } catch (cause) {
      setRelError(cause instanceof Error ? cause.message : t.biblioteca.relationError)
    } finally {
      setRelBusy(false)
    }
  }

  const save = async () => {
    if (!initial) {
      return
    }
    setBusy(true)
    setSaveError(null)
    try {
      const dto = await apiFetch<ContentItemDto>(
        `/api/v1/content/items/${initial.id}`,
        { method: 'PATCH', body: patch },
      )
      setInitial(dto)
      setForm(buildForm(dto))
      setSaved(true)
      onSaved(dto)
    } catch (cause) {
      setSaveError(mapError(cause, t.editor.actions.saveError))
    } finally {
      setBusy(false)
    }
  }

  const create = async () => {
    setBusy(true)
    setSaveError(null)
    try {
      const dto = await apiFetch<ContentItemDto>('/api/v1/content/items', {
        method: 'POST',
        body: {
          typeId: type.id,
          name: form.name,
          summary: form.summary,
          description: form.description,
          parentId: form.parentId,
          tags: parseList(form.tagsText),
          imageId: form.imageId,
          properties: buildProperties(form.properties, fields),
          inheritance: hasParent ? form.inheritance : {},
          scope: 'user',
          isPrivate: form.isPrivate,
          privateFields: parseList(form.privateFieldsText),
        },
      })
      onCreated(dto)
    } catch (cause) {
      setSaveError(mapError(cause, t.editor.actions.createError))
    } finally {
      setBusy(false)
    }
  }

  const publish = async () => {
    if (!initial) {
      return
    }
    setBusy(true)
    setSaveError(null)
    try {
      const dto = await apiFetch<ContentItemDto>(
        `/api/v1/content/items/${initial.id}/publish`,
        { method: 'POST', body: { message: null } },
      )
      setInitial(dto)
      setForm(buildForm(dto))
      setSaved(true)
      onSaved(dto)
      const loaded = await apiFetch<ContentRevisionDto[]>(
        `/api/v1/content/items/${initial.id}/revisions`,
      )
      setRevisions(loaded)
    } catch (cause) {
      setSaveError(mapError(cause, t.editor.actions.saveError))
    } finally {
      setBusy(false)
      setPublishOpen(false)
    }
  }

  // PMB-4.8: el autor propone el borrador; el DM aprueba o rechaza.
  const propose = async () => {
    if (!initial) return
    setProposeBusy(true)
    setSaveError(null)
    try {
      const dto = await apiFetch<ContentItemDto>(
        `/api/v1/content/items/${initial.id}/propose`,
        { method: 'POST', body: {} })
      setInitial(dto)
      setForm(buildForm(dto))
      onSaved(dto)
    } catch (cause) {
      setSaveError(mapError(cause, t.editor.actions.saveError))
    } finally {
      setProposeBusy(false)
    }
  }

  const objectSample = (key: string): string[] | null => {
    const candidates: unknown[] = [form.properties[key]]
    for (const entry of items) {
      if (entry.typeId === type.id) {
        candidates.push(entry.properties[key])
      }
    }
    for (const candidate of candidates) {
      if (
        Array.isArray(candidate) &&
        candidate.length > 0 &&
        candidate.every((row) => row !== null && typeof row === 'object' && !Array.isArray(row))
      ) {
        return Object.keys(candidate.find((row) => typeof row === 'object') as object)
      }
    }
    return null
  }

  return (
    <>
      {/* Centro — formulario por secciones (LIBRARY §3) */}
      <section className="editor-form" aria-label={t.editor.zoneForm}>
        {readOnly && (
          <div className="editor-banner editor-banner--info" role="status">
            <strong>{t.editor.readOnlyBanner}</strong>
            <span className="editor-banner__hint">{t.editor.readOnlyHint}</span>
          </div>
        )}
        {saveError && (
          <div className="editor-banner" role="alert">
            <strong>{saveError.title}</strong>
            <span>{saveError.detail}</span>
          </div>
        )}

        {/* 1. Identidad */}
        <section className="editor-section">
          <h3 className="editor-section__title">{t.editor.sections.identity}</h3>
          <div className="editor-section__grid">
            <div className="is-wide">
              <Field label={t.editor.fields.name}>
                {(props) => (
                  <Input
                    {...props}
                    maxLength={200}
                    data-editor-field="name"
                    disabled={readOnly}
                    value={form.name}
                    onChange={(event) => update({ name: event.target.value })}
                  />
                )}
              </Field>
            </div>
            <div className="is-wide">
              <Field label={t.editor.fields.summary} helper={t.editor.fields.summaryHelper}>
                {(props) => (
                  <Input
                    {...props}
                    data-editor-field="summary"
                    disabled={readOnly}
                    value={form.summary}
                    onChange={(event) => update({ summary: event.target.value })}
                  />
                )}
              </Field>
            </div>
            <div className="is-wide">
              <Field label={t.editor.fields.description}>
                {(props) => (
                  <textarea
                    id={props.id}
                    className="pv-input"
                    rows={3}
                    data-editor-field="description"
                    disabled={readOnly}
                    aria-invalid={props['aria-invalid']}
                    aria-describedby={props['aria-describedby']}
                    value={form.description}
                    onChange={(event) => update({ description: event.target.value })}
                  />
                )}
              </Field>
            </div>
            <Field label={t.editor.fields.tags} helper={t.editor.fields.tagsHelper}>
              {(props) => (
                <Input
                  {...props}
                  data-editor-field="tags"
                  disabled={readOnly}
                  value={form.tagsText}
                  onChange={(event) => update({ tagsText: event.target.value })}
                />
              )}
            </Field>
            <Field
              label={t.editor.fields.image}
              helper={imagesError ?? t.editor.fields.imageHelper}
            >
              {(props) => (
                <select
                  id={props.id}
                  className="pv-input"
                  data-editor-field="imageId"
                  disabled={readOnly}
                  value={form.imageId ?? ''}
                  aria-invalid={props['aria-invalid']}
                  aria-describedby={props['aria-describedby']}
                  onChange={(event) =>
                    update({ imageId: event.target.value || null })
                  }
                >
                  <option value="">—</option>
                  {(images ?? []).map((image) => (
                    <option key={image.id} value={image.id}>
                      {image.fileName}
                    </option>
                  ))}
                </select>
              )}
            </Field>
          </div>
        </section>

        {/* 2. Organización */}
        <section className="editor-section">
          <h3 className="editor-section__title">{t.editor.sections.organization}</h3>
          <div className="editor-section__grid">
            <Field
              label={t.editor.fields.parent}
              helper={t.editor.fields.parentHelper}
            >
              {(props) => (
                <select
                  id={props.id}
                  className="pv-input"
                  data-editor-field="parentId"
                  disabled={readOnly}
                  value={form.parentId ?? ''}
                  aria-invalid={props['aria-invalid']}
                  aria-describedby={props['aria-describedby']}
                  onChange={(event) => update({ parentId: event.target.value || null })}
                >
                  <option value="">{t.editor.fields.parentRoot}</option>
                  {siblingParents.map((entry) => (
                    <option key={entry.id} value={entry.id}>
                      {entry.name}
                    </option>
                  ))}
                </select>
              )}
            </Field>
            <Field label={t.editor.fields.relations} helper={t.editor.fields.relationsHelper}>
              {(props) =>
                creating ? (
                  <output id={props.id} className="editor-scope-locked" aria-disabled="true">
                    {t.editor.publishNoteCreate}
                  </output>
                ) : (
                  <div
                    className="editor-relations"
                    style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--pv-space-2)' }}
                    aria-describedby={props['aria-describedby']}
                  >
                    <select
                      id={props.id}
                      className="pv-input"
                      aria-label={t.biblioteca.relationKind}
                      disabled={readOnly || relBusy}
                      value={activeKind}
                      onChange={(event) => setRelKind(event.target.value)}
                    >
                      {relationKinds.map((kind) => (
                        <option key={kind} value={kind}>
                          {relationKindLabel(kind)}
                        </option>
                      ))}
                    </select>
                    <select
                      className="pv-input"
                      aria-label={t.biblioteca.relationTarget}
                      disabled={readOnly || relBusy}
                      value={relTarget}
                      onChange={(event) => setRelTarget(event.target.value)}
                    >
                      <option value="">—</option>
                      {items
                        .filter((entry) => entry.id !== initial?.id)
                        .map((entry) => (
                          <option key={entry.id} value={entry.id}>
                            {entry.name}
                          </option>
                        ))}
                    </select>
                    <button
                      type="button"
                      className="pv-btn pv-btn--ghost"
                      data-editor-field="relations"
                      disabled={readOnly || relBusy || !relTarget}
                      onClick={() => void addRelation()}
                    >
                      {t.biblioteca.relationAdd}
                    </button>
                    {relNotice && (
                      <span className="editor-relations__notice" role="status">
                        {relNotice}
                      </span>
                    )}
                    {relError && (
                      <span className="pv-field__error" role="alert">
                        {relError}
                      </span>
                    )}
                  </div>
                )
              }
            </Field>
            {hasParent && (
              <div className="is-wide">
                <span className="editor-section__note">{t.editor.fields.parentExcluded}</span>
              </div>
            )}
          </div>
        </section>

        {/* 3. Propiedades — campos tipados del tipo (adaptativo) */}
        <section className="editor-section">
          <div className="editor-prop__head">
            <h3 className="editor-section__title">{t.editor.sections.properties}</h3>
            <span className="editor-section__note">{t.editor.propsHint}</span>
          </div>
          {fields.length === 0 && (
            <p className="editor-section__note">Este tipo no declara campos de esquema.</p>
          )}
          {fields.map((field) => (
            <PropField
              key={field.key}
              field={field}
              value={form.properties[field.key]}
              mode={form.inheritance[field.key] ?? 'inherit'}
              parentName={parent?.name ?? null}
              parentValue={parent?.properties[field.key]}
              hasParent={hasParent}
              readOnly={readOnly}
              error={
                saveError?.field && saveError.field === field.key
                  ? saveError.detail
                  : undefined
              }
              objectKeys={objectSample(field.key) ?? undefined}
              onChange={(value) => setProperty(field.key, value)}
              onMode={(mode) => setMode(field.key, mode)}
            />
          ))}
        </section>

        {/* 7. Visibilidad, permisos y privacidad */}
        <section className="editor-section">
          <h3 className="editor-section__title">{t.editor.sections.visibility}</h3>
          <div className="editor-section__grid">
            <Field
              label={t.editor.fields.scope}
              helper={creating ? t.editor.fields.scopeCreateHelper : t.editor.fields.scopeEditHelper}
            >
              {(props) =>
                creating ? (
                  <select
                    id={props.id}
                    className="pv-input"
                    data-editor-field="scope"
                    value="user"
                    aria-describedby={props['aria-describedby']}
                    onChange={() => undefined}
                  >
                    <option value="user">{t.biblioteca.scopes.user}</option>
                    <option value="campaign" disabled>
                      {t.biblioteca.scopes.campaign} — en la biblioteca de campaña
                    </option>
                    <option value="dm" disabled>
                      {t.biblioteca.scopes.dm} — en la biblioteca de campaña
                    </option>
                    <option value="global" disabled>
                      {t.biblioteca.scopes.global} — solo importación de paquetes
                    </option>
                  </select>
                ) : (
                  <output id={props.id} className="editor-scope-locked">
                    {scopeLabel(item.scope)}
                  </output>
                )
              }
            </Field>
            <div className="editor-prop">
              <label className="editor-toggle">
                <input
                  type="checkbox"
                  data-editor-field="isPrivate"
                  checked={form.isPrivate}
                  disabled={readOnly}
                  onChange={(event) => update({ isPrivate: event.target.checked })}
                />
                {t.editor.fields.privateToggle}
              </label>
              <span className="pv-field__helper">{t.editor.fields.privateHelper}</span>
            </div>
            <div className="is-wide">
              <Field
                label={t.editor.fields.privateFields}
                helper={
                  form.isPrivate
                    ? t.editor.fields.privateFieldsHelperOn
                    : t.editor.fields.privateFieldsHelperOff
                }
              >
                {(props) => (
                  <Input
                    {...props}
                    data-editor-field="privateFields"
                    disabled={readOnly || !form.isPrivate}
                    value={form.privateFieldsText}
                    onChange={(event) => update({ privateFieldsText: event.target.value })}
                  />
                )}
              </Field>
            </div>
          </div>
        </section>

        {!readOnly && (
          <footer className="editor-actions">
            <span className={`editor-actions__note ${dirty ? 'editor-actions__note--dirty' : ''}`}>
              {busy
                ? t.editor.actions.saving
                : dirty
                  ? t.editor.dirty
                  : saved
                    ? t.editor.saved
                    : creating
                      ? t.editor.actions.noteCreate
                      : t.editor.actions.noteEdit}
            </span>
            <button
              type="button"
              className="pv-btn pv-btn--ghost"
              disabled={busy}
              onClick={() => {
                setForm(buildForm(creating ? null : initial))
                setSaveError(null)
                setSaved(false)
                onDirtyChange(false)
              }}
            >
              {t.editor.actions.cancel}
            </button>
            <button
              type="button"
              className="pv-btn pv-btn--primary"
              disabled={busy}
              onClick={() => void (creating ? create() : save())}
            >
              {busy
                ? t.editor.actions.saving
                : creating
                  ? t.editor.actions.create
                  : t.editor.actions.save}
            </button>
          </footer>
        )}
      </section>

      {/* Derecha — previsualización, procedencia y versionado */}
      <aside className="biblio-inspector" aria-label={t.editor.zoneState}>
        <h2 className="biblio-zone__title">{t.editor.zoneState}</h2>

        <div className="editor-preview">
          <span className={`editor-preview__thumb biblio-tile__thumb--${previewCover.kind}`}
            aria-hidden="true">
            <img src={previewCover.src} alt="" />
          </span>
          <div className="editor-preview__body">
            <p className="editor-preview__name">
              {form.name.trim() || t.editor.preview.emptyName}
            </p>
            <p className="editor-preview__summary">
              {form.summary.trim() || t.editor.preview.emptySummary}
            </p>
            <div className="biblio-inspector__tags">
              <span className="biblio-tag">{typeLabel(type)}</span>
              <span
                className={`biblio-tag ${(creating ? 'user' : item.scope) !== 'global' ? 'is-warn' : ''}`}
              >
                {scopeLabel(creating ? 'user' : item.scope)}
              </span>
              <span
                className={`biblio-tag ${
                  (creating ? 'draft' : item.workflowState) === 'published' ? '' : 'is-warn'
                }`}
              >
                {statusLabel(creating ? 'draft' : item.workflowState)}
              </span>
              {!creating && item.currentRevision > 0 && (
                <span className="biblio-tag">
                  {t.biblioteca.revisionBadge(item.currentRevision)}
                </span>
              )}
              {parseList(form.tagsText).map((tag) => (
                <span key={tag} className="biblio-tag">
                  {tag}
                </span>
              ))}
            </div>
          </div>
        </div>

        <h4 className="biblio-inspector__section">{t.biblioteca.provenance}</h4>
        {creating ? (
          <p className="editor-side__note">{t.editor.provenanceNew}</p>
        ) : pack ? (
          <div className="biblio-inspector__pack">
            <span>{pack.name}</span>
            <span className="biblio-inspector__license">
              v{pack.version} ·{' '}
              {pack.licenseUrl ? (
                <a href={pack.licenseUrl} target="_blank" rel="noreferrer">
                  {pack.license}
                </a>
              ) : (
                pack.license
              )}
            </span>
            {pack.attribution.map((line) => (
              <p key={line} className="biblio-inspector__attribution">
                {line}
              </p>
            ))}
          </div>
        ) : (
          <p className="biblio-inspector__pack">{t.biblioteca.noPack}</p>
        )}

        <h4 className="biblio-inspector__section">{t.biblioteca.revisions}</h4>
        {creating ? (
          <p className="biblio-inspector__empty">{t.biblioteca.revisionsNone}</p>
        ) : revisionsError ? (
          <p className="biblio-status biblio-status--error" role="alert">
            {revisionsError}
          </p>
        ) : revisions === null ? (
          <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
        ) : revisions.length === 0 ? (
          <p className="biblio-inspector__empty">{t.biblioteca.revisionsNone}</p>
        ) : (
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

        <h4 className="biblio-inspector__section">{t.editor.sectionActions ?? 'Acciones'}</h4>
        <div className="editor-side__actions">
          <button
            type="button"
            className="pv-btn pv-btn--secondary"
            disabled={creating || readOnly || busy}
            onClick={() => setPublishOpen(true)}
          >
            {t.biblioteca.actionPublish}
          </button>
          <p className="editor-side__publish-note">
            {creating
              ? t.editor.publishNoteCreate
              : readOnly
                ? t.editor.publishNoteReadOnly
                : t.editor.publishNote}
          </p>
          <div className="editor-side__row">
            <button type="button" className="pv-btn pv-btn--ghost" disabled>
              {t.biblioteca.actionDuplicate}
            </button>
            <button type="button" className="pv-btn pv-btn--ghost" disabled>
              {t.biblioteca.actionUsages}
            </button>
          </div>
          <button
            type="button"
            className="pv-btn pv-btn--ghost"
            disabled={!initial || readOnly || busy || proposeBusy
              || initial.workflowState !== 'draft'}
            onClick={() => void propose()}
          >
            {t.editor.actionPropose}
          </button>
          <p className="editor-side__note">{t.editor.futureActions}</p>
        </div>
      </aside>

      <ConfirmDialog
        open={publishOpen}
        title={t.editor.publishConfirmTitle}
        text={t.editor.publishConfirmText}
        confirmLabel={t.editor.publishConfirm}
        cancelLabel={t.editor.actions.cancel}
        onConfirm={() => void publish()}
        onCancel={() => setPublishOpen(false)}
      />
    </>
  )
}

// --- vista -------------------------------------------------------------

type EditorViewProps = {
  types: ContentTypeDto[]
  items: ContentItemDto[]
  packs: ContentPackDto[]
  target: EditorTarget
  onBack: () => void
}

export function EditorView({ types, items, packs, target, onBack }: EditorViewProps) {
  const [list, setList] = useState(items)
  const [selectedId, setSelectedId] = useState<string | null>(
    target.kind === 'edit' ? target.itemId : null,
  )
  const [expandedTypes, setExpandedTypes] = useState<Set<string>>(() => new Set(types.map((x) => x.id)))
  const [expandedItems, setExpandedItems] = useState<Set<string>>(
    () => new Set(items.filter((x) => x.parentId !== null).map((x) => x.parentId as string)),
  )
  const [dirty, setDirty] = useState(false)
  const [savedFlag, setSavedFlag] = useState(false)
  const [pendingId, setPendingId] = useState<string | null>(null)

  const creating = target.kind === 'create' && selectedId === null
  const current = selectedId ? list.find((entry) => entry.id === selectedId) ?? null : null
  const type = creating
    ? types.find((entry) => entry.id === target.typeId) ?? null
    : current
      ? types.find((entry) => entry.id === current.typeId) ?? null
      : null

  const replaceItem = (dto: ContentItemDto) => {
    setList((entries) => entries.map((entry) => (entry.id === dto.id ? dto : entry)))
    setSavedFlag(true)
    setDirty(false)
  }

  const requestSelect = (id: string) => {
    if (id === selectedId) {
      return
    }
    if (dirty) {
      setPendingId(id)
      return
    }
    setSelectedId(id)
    setSavedFlag(false)
  }

  const goBack = () => {
    if (dirty) {
      setPendingId('__back__')
      return
    }
    onBack()
  }

  const countOf = (typeId: string) =>
    list.filter((entry) => entry.typeId === typeId).length
  const rootsOf = (typeId: string) =>
    list.filter((entry) => entry.typeId === typeId && entry.parentId === null)
  const childrenOf = (id: string) => list.filter((entry) => entry.parentId === id)

  const toggleSet = (
    current: Set<string>,
    key: string,
    setter: (next: Set<string>) => void,
  ) => {
    const next = new Set(current)
    if (next.has(key)) {
      next.delete(key)
    } else {
      next.add(key)
    }
    setter(next)
  }

  if (!type || (!creating && !current)) {
    return (
      <div className="biblio">
        <div className="biblio-status biblio-status--error" role="alert">
          {t.editor.missing}
        </div>
        <div className="biblio-empty">
          <button type="button" className="pv-btn pv-btn--primary" onClick={onBack}>
            {t.editor.back}
          </button>
        </div>
      </div>
    )
  }

  // Tras el guard: si no estamos creando, hay item seleccionado (el guard
  // descarta el resto). TS no lo deduce por la alias de `creating`.
  const opened = current as ContentItemDto

  return (
    <div className="biblio">
      <header className="biblio__head">
        <div>
          <button type="button" className="pv-btn pv-btn--ghost editor-back" onClick={goBack}>
            {t.editor.back}
          </button>
          <nav className="editor-crumb" aria-label={t.editor.crumbLabel}>
            {t.biblioteca.title}
            <span aria-hidden="true">/</span>
            {typeLabel(type)}
            <span aria-hidden="true">/</span>
            {creating ? t.editor.crumbNew : opened.name}
          </nav>
          <h1 className="biblio__title">
            {creating ? t.editor.crumbNew : opened.name}
          </h1>
          <div className="biblio-inspector__tags">
            <span className="biblio-tag">{typeLabel(type)}</span>
            <span
              className={`biblio-tag ${creating || opened.scope !== 'global' ? 'is-warn' : ''}`}
            >
              {scopeLabel(creating ? 'user' : opened.scope)}
            </span>
            <span
              className={`biblio-tag ${
                (creating ? 'draft' : opened.workflowState) === 'published' ? '' : 'is-warn'
              }`}
            >
              {statusLabel(creating ? 'draft' : opened.workflowState)}
            </span>
            {!creating && opened.currentRevision > 0 && (
              <span className="biblio-tag">
                {t.biblioteca.revisionBadge(opened.currentRevision)}
              </span>
            )}
          </div>
        </div>
        <div className="editor-head__state">
          {dirty && <span className="editor-state">{t.editor.dirty}</span>}
          {!dirty && savedFlag && <span className="editor-state">{t.editor.saved}</span>}
          <span className="editor-state">
            {creating
              ? t.editor.state.creating
              : readOnlyItem(opened)
                ? t.editor.state.readOnly
                : t.editor.state.editing}
          </span>
        </div>
      </header>

      <div className="biblio__body">
        {/* Izquierda — árbol de tipos (contexto mientras se edita) */}
        <nav className="biblio-tree" aria-label={t.biblioteca.zoneTypes}>
          <h2 className="biblio-zone__title">{t.biblioteca.zoneTypes}</h2>
          {types.map((entry) => {
            const isExpanded = expandedTypes.has(entry.id)
            return (
              <div key={entry.id}>
                <button
                  type="button"
                  className={`biblio-tree__type ${entry.id === type.id ? 'is-active' : ''}`}
                  aria-expanded={isExpanded}
                  onClick={() => toggleSet(expandedTypes, entry.id, setExpandedTypes)}
                >
                  <span className="biblio-tree__caret" aria-hidden="true">
                    {isExpanded ? '▾' : '▸'}
                  </span>
                  <span className="biblio-tree__label">{typeLabel(entry)}</span>
                  <span className="biblio-tree__count">{countOf(entry.id)}</span>
                </button>
                {isExpanded &&
                  rootsOf(entry.id).map((root) => {
                    const children = childrenOf(root.id)
                    const rootExpanded = expandedItems.has(root.id)
                    return (
                      <div key={root.id}>
                        <button
                          type="button"
                          className={`biblio-tree__item ${selectedId === root.id ? 'is-selected' : ''}`}
                          onClick={() => {
                            if (children.length > 0) {
                              toggleSet(expandedItems, root.id, setExpandedItems)
                            }
                            requestSelect(root.id)
                          }}
                        >
                          <span className="biblio-tree__label">{root.name}</span>
                          {children.length > 0 && (
                            <span className="biblio-tree__count" aria-hidden="true">
                              {rootExpanded ? '▾' : `▸ ${children.length}`}
                            </span>
                          )}
                        </button>
                        {rootExpanded &&
                          children.map((child) => (
                            <button
                              key={child.id}
                              type="button"
                              className={`biblio-tree__item biblio-tree__item--child ${
                                selectedId === child.id ? 'is-selected' : ''
                              }`}
                              onClick={() => requestSelect(child.id)}
                            >
                              <span className="biblio-tree__label">{child.name}</span>
                            </button>
                          ))}
                      </div>
                    )
                  })}
              </div>
            )
          })}
          <button
            type="button"
            className="biblio-tree__add"
            disabled
            title={t.biblioteca.actionsNote}
          >
            {t.biblioteca.newCategory}
          </button>
        </nav>

        {/* Centro + derecha — formulario y estado (se remontan al cambiar de item) */}
        <EditorPane
          key={creating ? `create-${type.id}` : opened.id}
          item={creating ? null : opened}
          type={type}
          items={list}
          packs={packs}
          onDirtyChange={(value) => {
            setDirty(value)
            if (value) {
              setSavedFlag(false)
            }
          }}
          onSaved={replaceItem}
          onCreated={(dto) => {
            setList((entries) => [...entries, dto])
            setSelectedId(dto.id)
            setSavedFlag(true)
            setDirty(false)
          }}
        />
      </div>

      <ConfirmDialog
        open={pendingId !== null}
        title={t.editor.discardTitle}
        text={t.editor.discardText}
        confirmLabel={t.editor.discardConfirm}
        cancelLabel={t.editor.actions.cancel}
        destructive
        onConfirm={() => {
          if (pendingId === '__back__') {
            setPendingId(null)
            setDirty(false)
            onBack()
            return
          }
          if (pendingId) {
            setSelectedId(pendingId)
          }
          setPendingId(null)
          setDirty(false)
          setSavedFlag(false)
        }}
        onCancel={() => setPendingId(null)}
      />
    </div>
  )
}

/** Solo lectura: el servidor rechaza editar alcance global (CanEdit). */
function readOnlyItem(item: ContentItemDto | null): boolean {
  return item !== null && item.scope === 'global'
}
