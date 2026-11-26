import { useId, useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import { Field, Input } from '../../Field'
import { ITEMS, PACK, TYPES, type ProtoItem } from './bibliotecaData'
import {
  EMPTY_NEW,
  HOMEBREW,
  HOMEBREW_ERROR,
  MODE_LABELS,
  PARENT_DWARF,
  RACE_SCHEMA,
  SRD_HILL_DWARF,
  TREE_HOMEBREW,
  type AbilityBonus,
  type EditorItem,
  type InheritanceMode,
  type SchemaField,
} from './editorData'
import '../../../views/library.css'
import '../../../views/editor.css'

/**
 * Prototipo del editor universal (PMB-4.4) — solo desarrollo
 * (`#prototipo-editor[-error|-nuevo|-solo-lectura]`). Arquetipo «Editor»
 * de LIBRARY.md §3: formulario por secciones que se adapta al TypeSchema
 * (campos reales de `race`) + panel de estado y versionado, montado sobre
 * el frame de tres zonas aprobado en PMB-4.1. Sin lógica de negocio: los
 * datos son el seed SRD real y un homebrew de campaña etiquetado.
 */

type Variant = 'normal' | 'error' | 'nuevo' | 'solo-lectura'

const SCOPE_LABELS: Record<string, string> = {
  user: 'Personal',
  campaign: 'Campaña',
  dm: 'DM',
  global: 'Global',
  world: 'Mundo',
}

/** Valor legible para cajas heredadas y ayudantes. */
function formatValue(value: unknown): string {
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return '—'
    }
    if (
      value.every(
        (entry) => entry && typeof entry === 'object' && 'ability' in entry,
      )
    ) {
      return value
        .map((entry) => {
          const bonus = entry as AbilityBonus
          return `${bonus.ability} +${bonus.bonus}`
        })
        .join(', ')
    }
    return value.map(String).join(' · ')
  }
  if (value === null || value === undefined || value === '') {
    return '—'
  }
  return String(value)
}

/** Resumen corto del valor del padre para los ayudantes de herencia. */
function parentBrief(value: unknown): string {
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return 'sin elementos'
    }
    if (
      value.every(
        (entry) => entry && typeof entry === 'object' && 'ability' in entry,
      )
    ) {
      return formatValue(value)
    }
    return `${value.length} en total`
  }
  return String(value)
}

/** Control segmentado de modo de herencia (COMPONENT_RULES: grupo etiquetado). */
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
    <span className="editor-modes" role="group" aria-label={`Herencia de ${label}`}>
      {(['inherit', 'override', 'extend'] as const).map((value) => (
        <button
          key={value}
          type="button"
          className={`editor-modes__btn ${mode === value ? 'is-on' : ''}`}
          aria-pressed={mode === value}
          disabled={disabled}
          onClick={() => onChange(value)}
        >
          {MODE_LABELS[value]}
        </button>
      ))}
    </span>
  )
}

type PropFieldProps = {
  field: SchemaField
  value: unknown
  mode: InheritanceMode
  parentName: string
  parentValue: unknown
  hasParent: boolean
  readOnly: boolean
  error?: string
  autoFocus?: boolean
  onChange: (value: unknown) => void
  onMode: (mode: InheritanceMode) => void
}

/** Campo adaptativo del TypeSchema (lista, número o texto) + herencia. */
function PropField({
  field,
  value,
  mode,
  parentName,
  parentValue,
  hasParent,
  readOnly,
  error,
  autoFocus,
  onChange,
  onMode,
}: PropFieldProps) {
  const id = useId()
  const inherited = hasParent && mode === 'inherit'
  const describedBy = error ? `${id}-error` : `${id}-helper`

  const modeHelper =
    hasParent && mode !== 'inherit'
      ? mode === 'extend'
        ? parentValue === undefined
          ? `Se acumula con los valores de ${parentName}.`
          : `Se acumula con los del padre (${parentBrief(parentValue)}).`
        : parentValue === undefined
          ? `Sustituye el valor de ${parentName}.`
          : `Sustituye el valor del padre (${parentBrief(parentValue)}).`
      : null

  const updateList = (next: unknown[]) => onChange(next)
  const control = inherited ? (
    <output className="editor-prop__inherited" id={id}>
      {parentValue === undefined
        ? `Heredado de ${parentName}.`
        : <>Heredado de {parentName}: <strong>{formatValue(parentValue)}</strong></>}
    </output>
  ) : field.kind === 'abilityList' ? (
    <div
      className="editor-list"
      role="group"
      id={id}
      aria-labelledby={`${id}-label`}
      aria-describedby={describedBy}
    >
      {(Array.isArray(value) ? (value as AbilityBonus[]) : []).map((entry, index) => (
        <div className="editor-list__row editor-list__row--pair" key={index}>
          <Input
            value={entry.ability}
            aria-label={`Atributo ${index + 1}`}
            aria-required="true"
            disabled={readOnly}
            onChange={(event) => {
              const rows = [...(value as AbilityBonus[])]
              rows[index] = { ...entry, ability: event.target.value }
              updateList(rows)
            }}
          />
          <Input
            type="number"
            value={String(entry.bonus)}
            aria-label={`Bonificación ${index + 1}`}
            aria-required="true"
            disabled={readOnly}
            onChange={(event) => {
              const rows = [...(value as AbilityBonus[])]
              rows[index] = {
                ...entry,
                bonus: event.target.value === '' ? 0 : Number(event.target.value),
              }
              updateList(rows)
            }}
          />
          <button
            type="button"
            className="editor-list__remove"
            aria-label={`Quitar bonificación ${index + 1}`}
            disabled={readOnly}
            onClick={() =>
              updateList((value as AbilityBonus[]).filter((_, i) => i !== index))
            }
          >
            ×
          </button>
        </div>
      ))}
      <button
        type="button"
        className="editor-list__add"
        disabled={readOnly}
        onClick={() =>
          updateList([...(Array.isArray(value) ? (value as AbilityBonus[]) : []), { ability: '', bonus: 1 }])
        }
      >
        + Añadir {field.item ?? 'elemento'}
      </button>
    </div>
  ) : field.kind === 'stringList' ? (
    <div
      className="editor-list"
      role="group"
      id={id}
      aria-labelledby={`${id}-label`}
      aria-describedby={describedBy}
    >
      {(Array.isArray(value) ? (value as string[]) : []).map((entry, index) => (
        <div className="editor-list__row" key={index}>
          <Input
            value={entry}
            aria-label={`${field.item ?? 'Elemento'} ${index + 1}`}
            aria-required="true"
            disabled={readOnly}
            onChange={(event) => {
              const rows = [...(value as string[])]
              rows[index] = event.target.value
              updateList(rows)
            }}
          />
          <button
            type="button"
            className="editor-list__remove"
            aria-label={`Quitar ${field.item ?? 'elemento'} ${index + 1}`}
            disabled={readOnly}
            onClick={() => updateList((value as string[]).filter((_, i) => i !== index))}
          >
            ×
          </button>
        </div>
      ))}
      <button
        type="button"
        className="editor-list__add"
        disabled={readOnly}
        onClick={() =>
          updateList([...(Array.isArray(value) ? (value as string[]) : []), ''])
        }
      >
        + Añadir {field.item ?? 'elemento'}
      </button>
    </div>
  ) : field.kind === 'number' ? (
    <Input
      id={id}
      type="number"
      aria-required="true"
      aria-invalid={Boolean(error)}
      aria-describedby={describedBy}
      autoFocus={autoFocus}
      disabled={readOnly}
      value={typeof value === 'number' ? String(value) : typeof value === 'string' ? value : ''}
      onChange={(event) =>
        onChange(event.target.value === '' ? '' : Number(event.target.value))
      }
    />
  ) : (
    <Input
      id={id}
      aria-required="true"
      aria-invalid={Boolean(error)}
      aria-describedby={describedBy}
      disabled={readOnly}
      value={typeof value === 'string' ? value : ''}
      onChange={(event) => onChange(event.target.value)}
    />
  )

  const label = (
    <span className="editor-prop__labelrow">
      {inherited || field.kind === 'abilityList' || field.kind === 'stringList' ? (
        <span className="pv-field__label" id={`${id}-label`}>
          {field.label}
        </span>
      ) : (
        <label className="pv-field__label" htmlFor={id}>
          {field.label}
        </label>
      )}
      <span className="editor-prop__req" aria-hidden="true" title="Obligatorio">
        *
      </span>
    </span>
  )

  return (
    <div className="editor-prop">
      <div className="editor-prop__head">
        {label}
        {hasParent && (
          <ModeGroup label={field.label} mode={mode} disabled={readOnly} onChange={onMode} />
        )}
      </div>
      {control}
      {error ? (
        <span className="pv-field__error" id={`${id}-error`}>
          {error}
        </span>
      ) : (
        <span className="pv-field__helper" id={`${id}-helper`}>
          {modeHelper ?? field.helper ?? '\u00a0'}
        </span>
      )}
    </div>
  )
}

type EditorPaneProps = {
  item: EditorItem
  readOnly: boolean
  variant: Variant
}

/** Formulario + panel de estado (centro y derecha); el estado vive aquí. */
function EditorPane({ item, readOnly, variant }: EditorPaneProps) {
  const creating = item.key === ''
  const showError = variant === 'error' && item.key === HOMEBREW.key

  const [name, setName] = useState(item.name)
  const [summary, setSummary] = useState(item.summary)
  const [description, setDescription] = useState(item.description)
  const [tagsText, setTagsText] = useState(item.tags.join(', '))
  const [parentId, setParentId] = useState(item.parentKey ?? '')
  const [properties, setProperties] = useState<Record<string, unknown>>(() => {
    const clone = structuredClone(item.properties) as Record<string, unknown>
    return clone
  })
  const [inheritance, setInheritance] = useState<Record<string, InheritanceMode>>(() => ({
    ...item.inheritance,
  }))
  const [scope, setScope] = useState(item.scope)
  const [isPrivate, setIsPrivate] = useState(false)
  const [privateFieldsText, setPrivateFieldsText] = useState('')
  const [dirty, setDirty] = useState(false)

  const touch = () => setDirty(true)

  // Validación equivalente a la del servidor (ContentSchema/InputGuard):
  // las claves vacías no se envían, así que un campo obligatorio sin valor
  // llega ausente y el servidor lo rechaza.
  const errors: Record<string, string> = {}
  if (showError && !readOnly) {
    if (!('speed' in properties)) {
      errors.speed = "Falta la propiedad obligatoria 'speed'."
    }
    if (!('size' in properties)) {
      errors.size = "Falta la propiedad obligatoria 'size'."
    }
  }

  const setProperty = (key: string, value: unknown) => {
    setProperties((current) => {
      const next = { ...current }
      if (value === '' || value === null || value === undefined) {
        delete next[key]
      } else {
        next[key] = value
      }
      return next
    })
    touch()
  }

  const setMode = (key: string, mode: InheritanceMode) => {
    setInheritance((current) => ({ ...current, [key]: mode }))
    touch()
  }

  const raceRoots = ITEMS.filter((entry) => entry.key.startsWith('race/') && !entry.parent)
  const parentRoot = raceRoots.find((entry) => entry.key === parentId) ?? null
  const parentName = parentRoot?.name ?? 'el padre'
  const parentProperties =
    parentId === 'race/dwarf' ? PARENT_DWARF.properties : null
  const hasParent = parentId !== ''
  const tags = tagsText
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean)

  return (
    <>
      {/* Centro — formulario por secciones (LIBRARY §3) */}
      <section className="editor-form" aria-label="Formulario de contenido">
        {readOnly && (
          <div className="editor-banner editor-banner--info" role="status">
            <strong>Contenido oficial del SRD — solo lectura.</strong>
            <span className="editor-banner__hint">
              Duplicar para crear tu propia variante llega con PMB-4.5.
            </span>
          </div>
        )}
        {showError && !readOnly && (
          <div className="editor-banner" role="alert">
            <strong>No se pudo guardar el borrador.</strong>
            <span>
              Faltan propiedades obligatorias del esquema de «Raza»: revisa los campos
              marcados.
            </span>
            <span className="editor-banner__hint">
              El servidor valida las propiedades contra el TypeSchema antes de guardar.
            </span>
          </div>
        )}

        {/* 1. Identidad */}
        <section className="editor-section">
          <h3 className="editor-section__title">Identidad</h3>
          <div className="editor-section__grid">
            <div className="is-wide">
              <Field label="Nombre">
                {(props) => (
                  <Input
                    {...props}
                    maxLength={200}
                    disabled={readOnly}
                    value={name}
                    onChange={(event) => {
                      setName(event.target.value)
                      touch()
                    }}
                  />
                )}
              </Field>
            </div>
            <div className="is-wide">
              <Field label="Resumen" helper="Una línea para la rejilla y el inspector.">
                {(props) => (
                  <Input
                    {...props}
                    disabled={readOnly}
                    value={summary}
                    onChange={(event) => {
                      setSummary(event.target.value)
                      touch()
                    }}
                  />
                )}
              </Field>
            </div>
            <div className="is-wide">
              <Field label="Descripción">
                {(props) => (
                  <textarea
                    id={props.id}
                    className="pv-input"
                    rows={3}
                    disabled={readOnly}
                    aria-invalid={props['aria-invalid']}
                    aria-describedby={props['aria-describedby']}
                    value={description}
                    onChange={(event) => {
                      setDescription(event.target.value)
                      touch()
                    }}
                  />
                )}
              </Field>
            </div>
            <Field label="Etiquetas" helper="Separadas por coma.">
              {(props) => (
                <Input
                  {...props}
                  disabled={readOnly}
                  value={tagsText}
                  onChange={(event) => {
                    setTagsText(event.target.value)
                    touch()
                  }}
                />
              )}
            </Field>
            <Field label="Imagen principal" helper="Biblioteca de imágenes: PMB-4.7.">
              {(props) => <Input {...props} value="" placeholder="Sin imagen" disabled />}
            </Field>
          </div>
        </section>

        {/* 2. Organización */}
        <section className="editor-section">
          <h3 className="editor-section__title">Organización</h3>
          <div className="editor-section__grid">
            <Field
              label="Categoría o variante (padre)"
              helper="Padre del mismo tipo o categoría del mismo sistema (PMB-4.3)."
            >
              {(props) => (
                <select
                  id={props.id}
                  className="pv-input"
                  disabled={readOnly}
                  value={parentId}
                  aria-invalid={props['aria-invalid']}
                  aria-describedby={props['aria-describedby']}
                  onChange={(event) => {
                    setParentId(event.target.value)
                    touch()
                  }}
                >
                  <option value="">Sin padre — raíz de Razas</option>
                  {raceRoots.map((root) => (
                    <option key={root.key} value={root.key}>
                      {root.name}
                    </option>
                  ))}
                </select>
              )}
            </Field>
            <Field label="Relaciones" helper="Relaciones tipadas (race → ability): PMB-4.6.">
              {(props) => <Input {...props} value="" placeholder="Sin relaciones" disabled />}
            </Field>
          </div>
        </section>

        {/* 3. Propiedades — campos tipados del tipo (adaptativo) */}
        <section className="editor-section">
          <div className="editor-prop__head">
            <h3 className="editor-section__title">Propiedades</h3>
            <span className="editor-section__note">
              Se validan contra el esquema de «Raza» en el servidor.
            </span>
          </div>
          {RACE_SCHEMA.map((field) => (
            <PropField
              key={field.key}
              field={field}
              value={properties[field.key]}
              mode={inheritance[field.key] ?? 'inherit'}
              parentName={parentName}
              parentValue={parentProperties ? parentProperties[field.key as never] : undefined}
              hasParent={hasParent}
              readOnly={readOnly}
              error={errors[field.key]}
              autoFocus={showError && !readOnly && field.key === 'speed'}
              onChange={(value) => setProperty(field.key, value)}
              onMode={(mode) => setMode(field.key, mode)}
            />
          ))}
        </section>

        {/* 4-6. Secciones futuras: honestas y desactivadas */}
        <section className="editor-section editor-section--future">
          <h3 className="editor-section__title">Capacidades</h3>
          <p className="editor-section__note">
            Pasivas/habilidades (hasAbilities), equipo (hasEquipment) y contenedores
            (isContainer) — motor de reglas, Fase 5 (PMB-5.2).
          </p>
        </section>
        <section className="editor-section editor-section--future">
          <h3 className="editor-section__title">Requisitos y restricciones</h3>
          <p className="editor-section__note">
            Requisitos para el creador de personajes — Fase 5.
          </p>
        </section>
        <section className="editor-section editor-section--future">
          <h3 className="editor-section__title">Opciones elegibles</h3>
          <p className="editor-section__note">
            Elecciones configurables (ChoiceGroups) — Fase 5 (PMB-5.3).
          </p>
        </section>

        {/* 7. Visibilidad, permisos y privacidad */}
        <section className="editor-section">
          <h3 className="editor-section__title">Visibilidad y permisos</h3>
          <div className="editor-section__grid">
            <Field
              label="Ámbito"
              helper={
                creating
                  ? 'En la biblioteca global solo se crea contenido personal; el ámbito de campaña se elige en la biblioteca de campaña.'
                  : 'El ámbito se fija al crear el item; la API no permite editarlo.'
              }
            >
              {(props) =>
                creating ? (
                  <select
                    id={props.id}
                    className="pv-input"
                    value={scope}
                    aria-describedby={props['aria-describedby']}
                    onChange={(event) => {
                      setScope(event.target.value)
                      touch()
                    }}
                  >
                    <option value="user">Personal</option>
                    <option value="campaign" disabled>
                      Campaña — en la biblioteca de campaña
                    </option>
                    <option value="dm" disabled>
                      DM — en la biblioteca de campaña
                    </option>
                    <option value="global" disabled>
                      Global — solo importación de paquetes
                    </option>
                    <option value="world" disabled>
                      Mundo — reservado (ADR-032)
                    </option>
                  </select>
                ) : (
                  <output id={props.id} className="editor-scope-locked">
                    {SCOPE_LABELS[scope] ?? scope}
                  </output>
                )
              }
            </Field>
            <div className="editor-prop">
              <label className="editor-toggle">
                <input
                  type="checkbox"
                  checked={isPrivate}
                  disabled={readOnly}
                  onChange={(event) => {
                    setIsPrivate(event.target.checked)
                    touch()
                  }}
                />
                Solo DM (campo privado)
              </label>
              <span className="pv-field__helper">
                Oculta estos valores a los jugadores: el servidor los filtra al serializar
                (ADR-031 §6).
              </span>
            </div>
            <div className="is-wide">
              <Field
                label="Campos privados"
                helper={
                  isPrivate
                    ? 'Claves separadas por coma (p. ej. description, notes).'
                    : 'Activa «Solo DM» para marcar campos.'
                }
              >
                {(props) => (
                  <Input
                    {...props}
                    disabled={readOnly || !isPrivate}
                    value={privateFieldsText}
                    onChange={(event) => {
                      setPrivateFieldsText(event.target.value)
                      touch()
                    }}
                  />
                )}
              </Field>
            </div>
          </div>
        </section>

        {!readOnly && (
          <footer className="editor-actions">
            <span
              className={`editor-actions__note ${dirty ? 'editor-actions__note--dirty' : ''}`}
            >
              {dirty
                ? 'Cambios sin guardar'
                : creating
                  ? 'Las propiedades con * son obligatorias.'
                  : 'Se guarda como borrador; «Publicar» crea una revisión.'}
            </span>
            <button type="button" className="pv-btn pv-btn--ghost">
              Cancelar
            </button>
            <button type="button" className="pv-btn pv-btn--primary">
              {creating ? 'Crear borrador' : 'Guardar borrador'}
            </button>
          </footer>
        )}
      </section>

      {/* Derecha — previsualización, procedencia y versionado */}
      <aside className="biblio-inspector" aria-label="Estado y versionado">
        <h2 className="biblio-zone__title">Estado y versionado</h2>

        <div className="editor-preview">
          <span className="editor-preview__thumb" aria-hidden="true">
            {(name.trim()[0] ?? '?').toUpperCase()}
          </span>
          <div className="editor-preview__body">
            <p className="editor-preview__name">{name.trim() || 'Sin nombre'}</p>
            <p className="editor-preview__summary">
              {summary.trim() || 'Sin resumen — la previsualización se actualiza al escribir.'}
            </p>
            <div className="biblio-inspector__tags">
              <span className="biblio-tag">Raza</span>
              <span className={`biblio-tag ${scope === 'global' ? '' : 'is-warn'}`}>
                {SCOPE_LABELS[scope] ?? scope}
              </span>
              <span className={`biblio-tag ${item.status === 'Publicado' ? '' : 'is-warn'}`}>
                {item.status}
              </span>
              {item.revision > 0 && <span className="biblio-tag">rev. {item.revision}</span>}
              {tags.map((tag) => (
                <span key={tag} className="biblio-tag">
                  {tag}
                </span>
              ))}
            </div>
          </div>
        </div>

        <h4 className="biblio-inspector__section">Procedencia</h4>
        {creating ? (
          <p className="editor-side__note">
            Se creará en tu biblioteca personal (sin paquete de origen).
          </p>
        ) : item.scope === 'global' ? (
          <div className="biblio-inspector__pack">
            <span>{PACK.name}</span>
            <span className="biblio-inspector__license">
              v{PACK.version} ·{' '}
              <a href={PACK.licenseUrl} target="_blank" rel="noreferrer">
                {PACK.license}
              </a>
            </span>
            <p className="biblio-inspector__attribution">{PACK.attribution}</p>
          </div>
        ) : (
          <p className="biblio-inspector__pack">
            Contenido homebrew de la campaña · autor DM · revisión {item.revision}
          </p>
        )}

        <h4 className="biblio-inspector__section">Revisiones</h4>
        {item.revisions.length === 0 ? (
          <p className="biblio-inspector__empty">Sin revisiones publicadas todavía.</p>
        ) : (
          <ul className="biblio-revisions__list">
            {item.revisions.map((revision) => (
              <li key={revision.number}>
                Revisión {revision.number}
                {revision.message ? ` · ${revision.message}` : ''}
                <span className="biblio-revisions__meta">
                  {new Date(revision.date).toLocaleString('es-ES')}
                </span>
              </li>
            ))}
          </ul>
        )}

        <h4 className="biblio-inspector__section">Acciones</h4>
        <div className="editor-side__actions">
          <button
            type="button"
            className="pv-btn pv-btn--secondary"
            disabled={readOnly || creating}
          >
            Publicar
          </button>
          <p className="editor-side__publish-note">
            {creating
              ? 'Disponible tras crear el borrador.'
              : readOnly
                ? 'Contenido oficial: sin publicación propia.'
                : 'Crea una revisión nueva; los personajes deciden actualizar o conservar la suya (ADR-031 §7).'}
          </p>
          <div className="editor-side__row">
            <button type="button" className="pv-btn pv-btn--ghost" disabled>
              Duplicar
            </button>
            <button type="button" className="pv-btn pv-btn--ghost" disabled>
              Ver usos
            </button>
          </div>
          <button type="button" className="pv-btn pv-btn--ghost" disabled>
            Proponer
          </button>
          <p className="editor-side__note">
            Duplicar · Ver usos (PMB-4.5) y Proponer (PMB-4.8) llegan después del editor.
          </p>
        </div>
      </aside>
    </>
  )
}

export function EditorPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const [selectedKey, setSelectedKey] = useState<string>(() =>
    variant === 'nuevo'
      ? ''
      : variant === 'solo-lectura'
        ? SRD_HILL_DWARF.key
        : HOMEBREW.key,
  )
  const [expandedTypes, setExpandedTypes] = useState<Set<string>>(() => new Set(['race']))
  const [expandedItems, setExpandedItems] = useState<Set<string>>(
    () => new Set(['race/dwarf']),
  )

  const creating = selectedKey === ''
  const readOnly = selectedKey === SRD_HILL_DWARF.key
  const item: EditorItem = creating
    ? EMPTY_NEW
    : readOnly
      ? SRD_HILL_DWARF
      : variant === 'error'
        ? HOMEBREW_ERROR
        : HOMEBREW

  const treeItems: ProtoItem[] = creating ? ITEMS : [...ITEMS, TREE_HOMEBREW]
  const known = (key: string) => key === HOMEBREW.key || key === SRD_HILL_DWARF.key

  const countOf = (typeId: string) =>
    treeItems.filter((entry) => entry.key.startsWith(`${typeId}/`)).length
  const rootsOf = (typeId: string) =>
    treeItems.filter((entry) => entry.key.startsWith(`${typeId}/`) && !entry.parent)
  const childrenOf = (key: string) => treeItems.filter((entry) => entry.parent === key)

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

  return (
    <PrototypeShell active="biblioteca">
      <div className="biblio">
        <header className="biblio__head">
          <div>
            <nav className="editor-crumb" aria-label="Ruta de navegación">
              Biblioteca<span aria-hidden="true">/</span>Razas
              <span aria-hidden="true">/</span>
              {creating ? 'Nueva' : item.name}
            </nav>
            <h1 className="biblio__title">{creating ? 'Nuevo contenido' : item.name}</h1>
            <div className="biblio-inspector__tags">
              <span className="biblio-tag">Raza</span>
              <span className={`biblio-tag ${item.scope === 'global' ? '' : 'is-warn'}`}>
                {SCOPE_LABELS[item.scope] ?? item.scope}
              </span>
              <span className={`biblio-tag ${item.status === 'Publicado' ? '' : 'is-warn'}`}>
                {item.status}
              </span>
              {item.revision > 0 && <span className="biblio-tag">rev. {item.revision}</span>}
            </div>
          </div>
          <div className="editor-head__state">
            <span className="editor-state">
              {creating ? 'Nuevo' : readOnly ? 'Solo lectura' : 'Editando'}
            </span>
          </div>
        </header>

        <div className="biblio__body">
          {/* Izquierda — árbol de tipos y categorías (contexto) */}
          <nav className="biblio-tree" aria-label="Tipos de contenido">
            <h2 className="biblio-zone__title">Tipos</h2>
            {TYPES.map((type) => {
              const isExpanded = expandedTypes.has(type.id)
              return (
                <div key={type.id}>
                  <button
                    type="button"
                    className={`biblio-tree__type ${type.id === 'race' ? 'is-active' : ''}`}
                    aria-expanded={isExpanded}
                    onClick={() => toggleSet(expandedTypes, type.id, setExpandedTypes)}
                  >
                    <span className="biblio-tree__caret" aria-hidden="true">
                      {isExpanded ? '▾' : '▸'}
                    </span>
                    <span className="biblio-tree__label">{type.label}</span>
                    <span className="biblio-tree__count">{countOf(type.id)}</span>
                  </button>
                  {isExpanded &&
                    rootsOf(type.id).map((root) => {
                      const children = childrenOf(root.key)
                      const rootExpanded = expandedItems.has(root.key)
                      return (
                        <div key={root.key}>
                          <button
                            type="button"
                            className={`biblio-tree__item ${selectedKey === root.key ? 'is-selected' : ''}`}
                            title={
                              known(root.key) || children.length > 0
                                ? undefined
                                : 'Solo los items con datos del prototipo se abren'
                            }
                            onClick={() => {
                              if (children.length > 0) {
                                toggleSet(expandedItems, root.key, setExpandedItems)
                              }
                              if (known(root.key)) {
                                setSelectedKey(root.key)
                              }
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
                                key={child.key}
                                type="button"
                                className={`biblio-tree__item biblio-tree__item--child ${selectedKey === child.key ? 'is-selected' : ''}`}
                                title={
                                  known(child.key)
                                    ? undefined
                                    : 'Solo los items con datos del prototipo se abren'
                                }
                                onClick={() => {
                                  if (known(child.key)) {
                                    setSelectedKey(child.key)
                                  }
                                }}
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
              title="Acciones del explorador: PMB-4.5."
            >
              + Nueva categoría
            </button>
          </nav>

          {/* Centro + derecha — formulario y estado (se remontan al cambiar de item) */}
          <EditorPane
            key={selectedKey || 'nuevo'}
            item={item}
            readOnly={readOnly}
            variant={variant}
          />
        </div>
      </div>
    </PrototypeShell>
  )
}
