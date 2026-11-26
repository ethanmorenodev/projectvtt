import { useMemo, useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import { ConfirmDialog } from '../../ConfirmDialog'
import {
  HOMEBREW_ACCIONES,
  HOMEBREW_EXTREMO,
  ITEMS,
  PACK,
  RELATIONS,
  RELATION_KINDS,
  TYPES,
  USAGE_SAMPLE,
  type ProtoRelation,
  type ProtoType,
} from './bibliotecaData'
import '../../../views/library.css'

/**
 * Prototipo del explorador de Biblioteca (PMB-4.1) — solo desarrollo
 * (`#prototipo-biblioteca[-vacio|-extremo|-acciones]`). Arquetipo «Explorador» de
 * LIBRARY.md §2: árbol de tipos/categorías · rejilla de miniaturas
 * cuadradas (thumb-md) · inspector con resumen, propiedades, herencia,
 * procedencia (paquete + licencia) y acciones. Datos = seed SRD real.
 *
 * Variante `acciones` (PMB-4.5, pendiente de aprobación): filtros de estado
 * y etiqueta, orden por «Uso», inspector con acciones desbloqueadas
 * (Duplicar con confirmación, Mover como atajo al editor, Ver usos con panel
 * inline) y Publicar deshabilitado. Etiquetas y usos son datos de MUESTRA:
 * el seed SRD real no tiene ninguno de los dos.
 *
 * Variante `relaciones[-vacio]` (PMB-4.6, pendiente de aprobación): inspector
 * con relaciones tipadas entrantes/salientes, navegación al elemento relacionado
 * y editor local (tipo, destino y etiqueta). Las relaciones son de MUESTRA: el
 * seed no trae ninguna y el servidor validará los tipos permitidos.
 */

type Variant = 'normal' | 'vacio' | 'extremo' | 'acciones' | 'relaciones' | 'relaciones-vacio'
type Scope = 'todos' | 'srd' | 'campaña'
type Sort = 'nombre' | 'recientes' | 'uso'
type Estado = 'todos' | 'publicado' | 'borrador'

export function BibliotecaPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const extremo = variant === 'extremo'
  const vacio = variant === 'vacio'

  const acciones = variant === 'acciones'
  const relaciones = variant === 'relaciones' || variant === 'relaciones-vacio'
  const relacionesVacias = variant === 'relaciones-vacio'

  const allItems = useMemo(
    () =>
      acciones
        ? [...ITEMS, ...HOMEBREW_ACCIONES]
        : extremo
          ? [...ITEMS, HOMEBREW_EXTREMO]
          : ITEMS,
    [acciones, extremo],
  )

  const [typeId, setTypeId] = useState<ProtoType>('race')
  const [selectedKey, setSelectedKey] = useState<string | null>(
    acciones
      ? 'race/guardia-scaldar'
      : extremo
        ? HOMEBREW_EXTREMO.key
        : vacio
          ? null
          : 'race/dwarf',
  )
  const [query, setQuery] = useState('')
  const [scope, setScope] = useState<Scope>(vacio ? 'campaña' : 'todos')
  const [sort, setSort] = useState<Sort>('nombre')
  // Filtros y flujos de la variante «acciones» (PMB-4.5).
  const [estado, setEstado] = useState<Estado>('todos')
  const [tag, setTag] = useState<string | null>(null)
  const [showUsages, setShowUsages] = useState(false)
  const [duplicateOpen, setDuplicateOpen] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  // PMB-4.6: estado local del prototipo; la integración lo obtendrá de la API.
  const [relations, setRelations] = useState<ProtoRelation[]>(() =>
    relaciones && !relacionesVacias ? [...RELATIONS] : [])
  const [relationEditorOpen, setRelationEditorOpen] = useState(false)
  const [relKind, setRelKind] = useState<ProtoRelation['kind']>('grants')
  const [relTarget, setRelTarget] = useState('class/fighter')
  const [relLabel, setRelLabel] = useState('')
  const [expanded, setExpanded] = useState<Set<string>>(
    () =>
      new Set(
        acciones
          ? ['race', 'class', 'background', 'race/guardia-scaldar']
          : extremo
            ? ['race', 'class', 'background', 'race/dwarf', 'race/elf']
            : ['race', 'race/dwarf'],
      ),
  )

  const countOf = (type: ProtoType) => allItems.filter((i) => i.key.startsWith(`${type}/`)).length

  // Etiquetas disponibles (derivadas de los items con tags; SRD no tiene).
  const availableTags = useMemo(() => {
    const set = new Set<string>()
    for (const item of allItems) {
      for (const t of item.tags ?? []) set.add(t)
    }
    return [...set].sort((a, b) => a.localeCompare(b, 'es'))
  }, [allItems])

  const visible = useMemo(() => {
    let list = allItems.filter((i) => i.key.startsWith(`${typeId}/`))
    if (scope === 'srd') list = list.filter((i) => i.scope !== 'campaña')
    if (scope === 'campaña') list = list.filter((i) => i.scope === 'campaña')
    if (estado === 'publicado') list = list.filter((i) => i.status !== 'Borrador')
    if (estado === 'borrador') list = list.filter((i) => i.status === 'Borrador')
    if (tag) list = list.filter((i) => (i.tags ?? []).includes(tag))
    const q = query.trim().toLowerCase()
    if (q) {
      list = list.filter(
        (i) => i.name.toLowerCase().includes(q) || i.summary.toLowerCase().includes(q),
      )
    }
    if (sort === 'nombre') list = [...list].sort((a, b) => a.name.localeCompare(b.name, 'en'))
    if (sort === 'recientes') list = [...list].reverse()
    if (sort === 'uso') {
      list = [...list].sort((a, b) => (b.usages ?? 0) - (a.usages ?? 0) || a.name.localeCompare(b.name, 'en'))
    }
    return list
  }, [allItems, typeId, scope, estado, tag, query, sort])

  const selected = allItems.find((i) => i.key === selectedKey) ?? null
  const selectedTypeLabel = selected
    ? TYPES.find((t) => t.id === selected.key.split('/')[0])?.label ?? ''
    : ''

  // --- Relaciones tipadas (PMB-4.6) -------------------------------------
  const nameOf = (key: string) => allItems.find((i) => i.key === key)?.name ?? key
  const outgoing = relations.filter((relation) => relation.from === selectedKey)
  const incoming = relations.filter((relation) => relation.to === selectedKey)

  const navigateTo = (key: string) => {
    setSelectedKey(key)
    const type = key.split('/')[0] as ProtoType
    if (TYPES.some((entry) => entry.id === type)) setTypeId(type)
    setNotice(`Navegado a «${nameOf(key)}».`)
  }

  const addRelation = () => {
    if (!selectedKey || relTarget === selectedKey) return
    setRelations((current) => [
      ...current,
      {
        from: selectedKey,
        kind: relKind,
        to: relTarget,
        label: relLabel.trim() || undefined,
      },
    ])
    setRelationEditorOpen(false)
    setRelLabel('')
    setNotice(`Relación «${relKind}» añadida a «${nameOf(relTarget)}».`)
  }

  const removeRelation = (relation: ProtoRelation) => {
    setRelations((current) => current.filter((entry) => entry !== relation))
    setNotice(`Relación «${relation.kind}» eliminada.`)
  }

  const toggle = (key: string) =>
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })

  const rootsOf = (type: ProtoType) =>
    allItems.filter((i) => i.key.startsWith(`${type}/`) && i.key.split('/').length === 2)
  const childrenOf = (key: string) => allItems.filter((i) => i.parent === key)

  const clearFilters = () => {
    setScope('todos')
    setQuery('')
    setEstado('todos')
    setTag(null)
  }

  return (
    <PrototypeShell active="biblioteca">
      <div className="biblio">
        <header className="biblio__head">
          <div>
            <h1 className="biblio__title">Biblioteca</h1>
            <p className="biblio__subtitle">
              Explorador de contenido · sistema <code>dnd5e</code>
            </p>
          </div>
          <button type="button" className="biblio__create">
            Nuevo contenido
          </button>
        </header>

        <div className="biblio__body">
          {/* Izquierda — árbol de tipos y categorías (LIBRARY §2) */}
          <nav className="biblio-tree" aria-label="Tipos de contenido">
            <h2 className="biblio-zone__title">Tipos</h2>
            {TYPES.map((type) => (
              <div key={type.id}>
                <button
                  type="button"
                  className={`biblio-tree__type ${typeId === type.id ? 'is-active' : ''}`}
                  aria-expanded={expanded.has(type.id)}
                  onClick={() => {
                    setTypeId(type.id)
                    toggle(type.id)
                  }}
                >
                  <span className="biblio-tree__caret" aria-hidden="true">
                    {expanded.has(type.id) ? '▾' : '▸'}
                  </span>
                  <span className="biblio-tree__label">{type.label}</span>
                  <span className="biblio-tree__count">{countOf(type.id)}</span>
                </button>
                {expanded.has(type.id) &&
                  rootsOf(type.id).map((item) => (
                    <div key={item.key}>
                      <button
                        type="button"
                        className={`biblio-tree__item ${selectedKey === item.key ? 'is-selected' : ''}`}
                        onClick={() => {
                          setSelectedKey(item.key)
                          if (childrenOf(item.key).length > 0) toggle(item.key)
                        }}
                      >
                        <span className="biblio-tree__label">{item.name}</span>
                        {childrenOf(item.key).length > 0 && (
                          <span className="biblio-tree__count" aria-hidden="true">
                            {expanded.has(item.key) ? '▾' : `▸ ${childrenOf(item.key).length}`}
                          </span>
                        )}
                      </button>
                      {expanded.has(item.key) &&
                        childrenOf(item.key).map((child) => (
                          <button
                            key={child.key}
                            type="button"
                            className={`biblio-tree__item biblio-tree__item--child ${selectedKey === child.key ? 'is-selected' : ''}`}
                            onClick={() => setSelectedKey(child.key)}
                          >
                            <span className="biblio-tree__label">{child.name}</span>
                          </button>
                        ))}
                    </div>
                  ))}
              </div>
            ))}
            <button type="button" className="biblio-tree__add">
              + Nueva categoría
            </button>
          </nav>

          {/* Centro — rejilla de miniaturas compactas */}
          <section className="biblio-grid" aria-label="Rejilla de contenido">
            <div className="biblio-toolbar">
              <input
                className="biblio-toolbar__search"
                placeholder="Buscar por nombre, etiqueta o tipo…"
                aria-label="Buscar en la biblioteca"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
              <div className="biblio-toolbar__filters" role="group" aria-label="Ámbito">
                <span className="biblio-toolbar__label">Ámbito:</span>
                {(['todos', 'srd', 'campaña'] as const).map((value) => (
                  <button
                    key={value}
                    type="button"
                    className="biblio-chip"
                    aria-pressed={scope === value}
                    onClick={() => setScope(value)}
                  >
                    {value === 'todos' ? 'Todos' : value === 'srd' ? 'SRD' : 'Campaña'}
                  </button>
                ))}
              </div>
              {acciones && (
                <>
                  <div className="biblio-toolbar__filters" role="group" aria-label="Estado">
                    <span className="biblio-toolbar__label">Estado:</span>
                    {(['todos', 'publicado', 'borrador'] as const).map((value) => (
                      <button
                        key={value}
                        type="button"
                        className="biblio-chip"
                        aria-pressed={estado === value}
                        onClick={() => setEstado(value)}
                      >
                        {value === 'todos'
                          ? 'Todos'
                          : value === 'publicado'
                            ? 'Publicado'
                            : 'Borrador'}
                      </button>
                    ))}
                  </div>
                  <div className="biblio-toolbar__filters" role="group" aria-label="Etiqueta">
                    <span className="biblio-toolbar__label">Etiqueta:</span>
                    <button
                      type="button"
                      className="biblio-chip"
                      aria-pressed={tag === null}
                      onClick={() => setTag(null)}
                    >
                      Todas
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
                      <span className="biblio-toolbar__label">sin etiquetas (muestra)</span>
                    )}
                  </div>
                </>
              )}
              <label className="biblio-toolbar__sort">
                Orden
                <select value={sort} onChange={(event) => setSort(event.target.value as Sort)}>
                  <option value="nombre">Nombre</option>
                  <option value="recientes">Recientes</option>
                  <option value="uso">Uso</option>
                </select>
              </label>
              <span className="biblio-toolbar__count">{visible.length} elementos</span>
            </div>

            {visible.length === 0 ? (
              <div className="biblio-empty">
                <p className="biblio-empty__title">No hay elementos con estos filtros.</p>
                <p className="biblio-empty__text">
                  Prueba con otro ámbito o borra la búsqueda.
                </p>
                <button type="button" className="biblio-empty__action" onClick={clearFilters}>
                  Limpiar filtros
                </button>
              </div>
            ) : (
              <ul className="biblio-grid__tiles">
                {visible.map((item) => (
                  <li key={item.key}>
                    <button
                      type="button"
                      className={`biblio-tile ${selectedKey === item.key ? 'is-selected' : ''}`}
                      onClick={() => setSelectedKey(item.key)}
                    >
                      <span className="biblio-tile__thumb" aria-hidden="true">
                        {item.name.slice(0, 1).toUpperCase()}
                      </span>
                      <span className="biblio-tile__name">{item.name}</span>
                      <span className="biblio-tile__meta">
                        <span className={`biblio-tag ${item.scope === 'campaña' ? 'is-warn' : ''}`}>
                          {item.scope === 'campaña' ? 'Campaña' : 'SRD'}
                        </span>
                        {item.status && <span className="biblio-tag is-warn">{item.status}</span>}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {/* Derecha — inspector */}
          <aside className="biblio-inspector" aria-label="Inspector">
            <h2 className="biblio-zone__title">Inspector</h2>
            {!selected ? (
              <p className="biblio-inspector__empty">
                Selecciona un elemento de la rejilla para ver su detalle.
              </p>
            ) : (
              <div className="biblio-inspector__body">
                <h3 className="biblio-inspector__name">{selected.name}</h3>
                <code className="biblio-inspector__key">{selected.key}</code>
                <div className="biblio-inspector__tags">
                  <span className="biblio-tag">{selectedTypeLabel}</span>
                  <span className={`biblio-tag ${selected.scope === 'campaña' ? 'is-warn' : ''}`}>
                    {selected.scope === 'campaña' ? 'Campaña' : 'SRD'}
                  </span>
                  <span className={`biblio-tag ${selected.status ? 'is-warn' : ''}`}>
                    {selected.status ?? 'Publicado'}
                  </span>
                  {acciones &&
                    (selected.tags ?? []).map((t) => (
                      <span key={t} className="biblio-tag">
                        {t}
                      </span>
                    ))}
                </div>
                <p className="biblio-inspector__summary">{selected.summary}</p>

                <h4 className="biblio-inspector__section">Propiedades</h4>
                <ul className="biblio-inspector__props">
                  {selected.props.map((prop) => (
                    <li key={prop}>{prop}</li>
                  ))}
                </ul>

                {selected.parent && (
                  <>
                    <h4 className="biblio-inspector__section">Herencia</h4>
                    <p className="biblio-inspector__inherit">
                      Extiende:{' '}
                      <strong>{allItems.find((i) => i.key === selected.parent)?.name}</strong>
                    </p>
                    <code className="biblio-inspector__key">{selected.inheritance}</code>
                  </>
                )}

                {relaciones && (
                  <>
                    <h4 className="biblio-inspector__section">Relaciones</h4>
                    {outgoing.length === 0 && incoming.length === 0 ? (
                      <p className="biblio-inspector__note">
                        Sin relaciones registradas para este elemento.
                      </p>
                    ) : (
                      <ul className="biblio-rel__list">
                        {outgoing.map((relation, index) => (
                          <li key={`sal-${index}`}>
                            <span className="biblio-rel__kind">{relation.kind}</span>
                            <span aria-hidden="true"> → </span>
                            <button
                              type="button"
                              className="biblio-rel__link"
                              onClick={() => navigateTo(relation.to)}
                            >
                              {nameOf(relation.to)}
                            </button>
                            {relation.label && (
                              <span className="biblio-rel__label"> · {relation.label}</span>
                            )}
                          </li>
                        ))}
                        {incoming.map((relation, index) => (
                          <li key={`ent-${index}`}>
                            <span className="biblio-rel__kind">{relation.kind}</span>
                            <span aria-hidden="true"> ← </span>
                            <button
                              type="button"
                              className="biblio-rel__link"
                              onClick={() => navigateTo(relation.from)}
                            >
                              {nameOf(relation.from)}
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
                      onClick={() => setRelationEditorOpen(true)}
                    >
                      Editar relaciones
                    </button>
                    <p className="biblio-inspector__note">
                      Relaciones de muestra: los tipos permitidos los valida el servidor por sistema.
                    </p>
                  </>
                )}

                <h4 className="biblio-inspector__section">Procedencia</h4>
                {selected.scope === 'campaña' ? (
                  <p className="biblio-inspector__pack">
                    Contenido homebrew de la campaña · autor DM · revisión 1
                  </p>
                ) : (
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
                )}

                {acciones ? (
                  <>
                    <div className="biblio-inspector__actions biblio-inspector__actions--live">
                      <button type="button" className="biblio-inspector__primary">
                        Editar
                      </button>
                      <button type="button" onClick={() => setDuplicateOpen(true)}>
                        Duplicar
                      </button>
                      <button
                        type="button"
                        onClick={() =>
                          setNotice('Mover cambia la jerarquía desde el editor (sección Organización).')
                        }
                      >
                        Mover
                      </button>
                      <button
                        type="button"
                        aria-expanded={showUsages}
                        onClick={() => setShowUsages((open) => !open)}
                      >
                        Ver usos
                      </button>
                      <button type="button" disabled title="Se publica desde el editor.">
                        Publicar
                      </button>
                    </div>
                    <p className="biblio-inspector__note">
                      Publicar vive en el editor (PMB-4.4); Mover reutiliza su sección Organización.
                    </p>

                    {showUsages && (
                      <section className="biblio-usos" aria-label="Usos del elemento">
                        <h4 className="biblio-inspector__section">Usos</h4>
                        {(USAGE_SAMPLE[selected.key] ?? []).length === 0 ? (
                          <p className="biblio-inspector__note">
                            Sin usos registrados: ningún personaje usa este elemento todavía.
                          </p>
                        ) : (
                          <ul className="biblio-usos__list">
                            {(USAGE_SAMPLE[selected.key] ?? []).map((usage) => (
                              <li key={`${usage.actorName}-${usage.createdAt}`}>
                                <strong>{usage.actorName}</strong>
                                <span className="biblio-usos__meta">
                                  {usage.campaignName} · rev. {usage.revisionNumber} ·{' '}
                                  {new Date(usage.createdAt).toLocaleDateString('es-ES')}
                                </span>
                              </li>
                            ))}
                          </ul>
                        )}
                        <p className="biblio-inspector__note">
                          Solo se listan usos de campañas donde eres miembro; los snapshots no se
                          muestran aquí (datos de muestra, PMB-4.5).
                        </p>
                      </section>
                    )}

                  </>
                ) : (
                  <div className="biblio-inspector__actions">
                    <button type="button" className="biblio-inspector__primary">
                      Editar
                    </button>
                    <button type="button">Duplicar</button>
                    <button type="button">Publicar</button>
                    <button type="button">Ver usos</button>
                  </div>
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
      </div>

      {acciones && selected && (
        <ConfirmDialog
          open={duplicateOpen}
          title={`Duplicar «${selected.name}»`}
          text="Se creará una copia en tu biblioteca personal (ámbito personal), sin usos ni historial. El original no cambia."
          confirmLabel="Duplicar"
          cancelLabel="Cancelar"
          onConfirm={() => {
            setDuplicateOpen(false)
            setNotice(`Copia creada en tu biblioteca personal: «${selected.name} (copia)».`)
          }}
          onCancel={() => setDuplicateOpen(false)}
        />
      )}

      {relaciones && relationEditorOpen && selected && (
        <div className="pv-confirm__overlay" onClick={() => setRelationEditorOpen(false)}>
          <div
            className="pv-confirm biblio-rel-editor"
            role="dialog"
            aria-label="Editar relaciones"
            onClick={(event) => event.stopPropagation()}
          >
            <h2 className="pv-confirm__title">
              Editar relaciones de «{selected.name}»
            </h2>
            <div className="biblio-rel-editor__fields">
              <label>
                Tipo
                <select
                  value={relKind}
                  onChange={(event) =>
                    setRelKind(event.target.value as ProtoRelation['kind'])
                  }
                >
                  {RELATION_KINDS.map((kind) => (
                    <option key={kind} value={kind}>{kind}</option>
                  ))}
                </select>
              </label>
              <label>
                Destino
                <select value={relTarget} onChange={(event) => setRelTarget(event.target.value)}>
                  {allItems
                    .filter((item) => item.key !== selected.key)
                    .map((item) => (
                      <option key={item.key} value={item.key}>{item.name}</option>
                    ))}
                </select>
              </label>
              <label>
                Etiqueta (opcional)
                <input
                  value={relLabel}
                  onChange={(event) => setRelLabel(event.target.value)}
                  placeholder="Etiqueta opcional"
                />
              </label>
            </div>
            {outgoing.length > 0 && (
              <ul className="biblio-rel__list">
                {outgoing.map((relation, index) => (
                  <li key={`editar-${index}`}>
                    <span className="biblio-rel__kind">{relation.kind}</span>
                    <span aria-hidden="true"> → </span>
                    {nameOf(relation.to)}
                    <button
                      type="button"
                      className="biblio-rel__remove"
                      onClick={() => removeRelation(relation)}
                    >
                      Quitar
                    </button>
                  </li>
                ))}
              </ul>
            )}
            <div className="pv-confirm__actions">
              <button
                type="button"
                className="pv-btn pv-btn--ghost"
                onClick={() => setRelationEditorOpen(false)}
              >
                Cerrar
              </button>
              <button type="button" className="pv-btn pv-btn--primary" onClick={addRelation}>
                Añadir relación
              </button>
            </div>
          </div>
        </div>
      )}
    </PrototypeShell>
  )
}
