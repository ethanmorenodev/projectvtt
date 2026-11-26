import { useEffect, useMemo, useState } from 'react'
import { apiFetch } from '../api/client'
import type { ContentItemDto, ContentTypeDto } from '../api/types'
import { Button } from '../ui'
import { contentIcon } from '../ui/contentIcons'
import { t } from '../i18n'
import { useNavigate } from '../routing/router'
import './homebrewClass.css'

/**
 * Creador guiado de clases homebrew para DM (EXP-3). Reutiliza el modelo de
 * contenido real: crea un ContentItem de tipo `class` con los campos de su
 * esquema y publica con el flujo de revisiones existente. Sin JSON ni reglas
 * de negocio duplicadas en el cliente.
 */

const STEPS = [
  { id: 'origen', label: 'Origen', hint: 'Desde cero o duplicar' },
  { id: 'identidad', label: 'Identidad', hint: 'Nombre, concepto y estilo' },
  { id: 'atributos', label: 'Atributos y recursos', hint: 'Dado, atributos y salvaciones' },
  { id: 'equipo', label: 'Equipo inicial', hint: 'Con qué empieza' },
  { id: 'habilidades', label: 'Habilidades', hint: 'Qué puede hacer' },
  { id: 'progresion', label: 'Progresión y elecciones', hint: 'Niveles y decisiones del jugador' },
  { id: 'publicar', label: 'Revisar y publicar', hint: 'Vista previa y publicación' },
] as const

interface Form {
  name: string
  concept: string
  playstyle: string
  hitDie: number
  primaryAbilities: string
  savingThrows: string
  skillChoices: number
  armor: string
  weapons: string
  equipment: string
  abilities: string
  progression: string
  choices: string
}

const EMPTY: Form = {
  name: '', concept: '', playstyle: '', hitDie: 10, primaryAbilities: '', savingThrows: '',
  skillChoices: 2, armor: '', weapons: '', equipment: '', abilities: '', progression: '', choices: '',
}

const list = (value: string) => value.split(',').map((entry) => entry.trim()).filter(Boolean)

export function HomebrewClassView() {
  const navigate = useNavigate()
  const [stepIndex, setStepIndex] = useState(0)
  const [form, setForm] = useState<Form>(EMPTY)
  const [types, setTypes] = useState<ContentTypeDto[]>([])
  const [classes, setClasses] = useState<ContentItemDto[]>([])
  const [originId, setOriginId] = useState('')
  const [item, setItem] = useState<ContentItemDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const step = STEPS[stepIndex]

  useEffect(() => {
    let cancelled = false
    void Promise.all([
      apiFetch<ContentTypeDto[]>('/api/v1/content/types'),
      apiFetch<ContentItemDto[]>('/api/v1/content/items?typeKey=class'),
    ]).then(([typeList, itemList]) => {
      if (cancelled) return
      setTypes(typeList)
      setClasses(itemList.filter((entry) => entry.typeKey === 'class'))
    }).catch((cause) => { if (!cancelled) setError(cause instanceof Error ? cause.message : t.editor.loadError) })
    return () => { cancelled = true }
  }, [])

  const classType = useMemo(() => types.find((entry) => entry.key === 'class') ?? null, [types])

  const patch = (values: Partial<Form>) => setForm((current) => ({ ...current, ...values }))

  const duplicate = (id: string) => {
    setOriginId(id)
    const source = classes.find((entry) => entry.id === id)
    if (!source) return
    const props = source.properties as Record<string, unknown>
    patch({
      name: `${source.name} (copia)`,
      concept: source.summary ?? '',
      hitDie: Number(props.hitDie ?? 10),
      primaryAbilities: (props.primaryAbilities as string[] ?? []).join(', '),
      savingThrows: (props.savingThrows as string[] ?? []).join(', '),
      skillChoices: Number(props.skillChoices ?? 2),
      armor: (props.armorProficiencies as string[] ?? []).join(', '),
      weapons: (props.weaponProficiencies as string[] ?? []).join(', '),
    })
    setNotice(`Clase «${source.name}» duplicada como base.`)
  }

  const payload = () => ({
    name: form.name.trim(),
    summary: form.concept.trim() || form.playstyle.trim(),
    description: [
      form.concept && `Concepto: ${form.concept}`,
      form.playstyle && `Estilo de juego: ${form.playstyle}`,
      form.equipment && `Equipo inicial: ${form.equipment}`,
      form.abilities && `Habilidades: ${form.abilities}`,
      form.progression && `Progresión: ${form.progression}`,
      form.choices && `Elecciones del jugador: ${form.choices}`,
    ].filter(Boolean).join('\n'),
    properties: {
      hitDie: form.hitDie,
      primaryAbilities: list(form.primaryAbilities),
      savingThrows: list(form.savingThrows),
      skillChoices: form.skillChoices,
      armorProficiencies: list(form.armor),
      weaponProficiencies: list(form.weapons),
    },
  })

  const saveDraft = async () => {
    if (!classType || !form.name.trim()) { setError('Ponle un nombre a la clase.'); return }
    setBusy(true); setError(null)
    try {
      if (item) {
        const updated = await apiFetch<ContentItemDto>(`/api/v1/content/items/${item.id}`, {
          method: 'PATCH', body: payload(),
        })
        setItem(updated)
      } else {
        const created = await apiFetch<ContentItemDto>('/api/v1/content/items', {
          method: 'POST', body: { typeId: classType.id, scope: 'user', ...payload() },
        })
        setItem(created)
      }
      setNotice('Borrador guardado. Puedes seguir editando sin perderlo.')
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.biblioteca.error)
    } finally { setBusy(false) }
  }

  const publish = async () => {
    if (!item) { await saveDraft(); return }
    if (item.workflowState === 'published') { setNotice('Esta clase ya está publicada.'); return }
    setBusy(true); setError(null)
    try {
      await apiFetch<ContentItemDto>(`/api/v1/content/items/${item.id}/publish`, {
        method: 'POST', body: { message: null },
      })
      setNotice('Clase publicada. Los personajes que ya la usan deciden si actualizan.')
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo publicar la clase.')
    } finally { setBusy(false) }
  }

  const field = (label: string, control: React.ReactNode) => (
    <label className="hc-field"><span>{label}</span>{control}</label>
  )
  const input = (key: keyof Form, placeholder?: string) => (
    <input className="pv-input" value={form[key] as string} placeholder={placeholder}
      onChange={(event) => patch({ [key]: event.target.value } as Partial<Form>)} />
  )
  const textarea = (key: keyof Form, placeholder?: string) => (
    <textarea className="pv-input" value={form[key] as string} placeholder={placeholder} rows={4}
      onChange={(event) => patch({ [key]: event.target.value } as Partial<Form>)} />
  )

  return (
    <main className="hc">
      <header className="hc__head">
        <div>
          <h1>Crear clase</h1>
          <p>Paso {stepIndex + 1} de {STEPS.length} · {step.label}. Sin programar ni editar datos técnicos.</p>
        </div>
        <div className="hc__head-actions">
          <Button variant="ghost" onClick={() => navigate({ kind: 'biblioteca' })}>Volver a la biblioteca</Button>
          <Button variant="secondary" disabled={busy} onClick={() => void saveDraft()}>
            {item ? 'Guardar cambios' : 'Guardar borrador'}
          </Button>
        </div>
      </header>

      {error ? <div className="pv-list-error" role="alert"><p>{error}</p></div> : null}
      {notice ? <p className="hc__notice" role="status">{notice}</p> : null}

      <div className="hc__body">
        <nav className="hc__steps" aria-label="Pasos de la clase">
          {STEPS.map((entry, index) => (
            <button key={entry.id} type="button"
              className={`hc__step${entry.id === step.id ? ' is-active' : ''}${index < stepIndex ? ' is-done' : ''}`}
              onClick={() => setStepIndex(index)}>
              <span className="hc__step-num">{index < stepIndex ? '✓' : index + 1}</span>
              <span><strong>{entry.label}</strong><small>{entry.hint}</small></span>
            </button>
          ))}
        </nav>

        <section className="hc__content">
          <h2>{step.label}</h2>
          <p className="hc__hint">{step.hint}</p>

          {step.id === 'origen' ? (
            <div className="hc__cards">
              <button type="button" className={`hc__base${!originId ? ' is-selected' : ''}`}
                onClick={() => { setOriginId(''); setForm(EMPTY) }}>
                <img src="/icons/dice.svg" alt="" /><strong>Desde cero</strong><small>Todo se define a mano</small>
              </button>
              {classes.map((entry) => (
                <button key={entry.id} type="button"
                  className={`hc__base${originId === entry.id ? ' is-selected' : ''}`}
                  onClick={() => duplicate(entry.id)}>
                  <img src={contentIcon(entry.typeKey, entry.name) ?? '/icons/broadsword.svg'} alt="" />
                  <strong>{entry.name}</strong><small>Copia y ajusta sus valores</small>
                </button>
              ))}
            </div>
          ) : null}

          {step.id === 'identidad' ? (
            <div className="hc__form">
              {field('Nombre', input('name', 'Guardián del Umbral'))}
              {field('Concepto', textarea('concept', 'Una breve idea de quién es esta clase.'))}
              {field('Estilo de juego', input('playstyle', 'Defensa y control'))}
            </div>
          ) : null}

          {step.id === 'atributos' ? (
            <div className="hc__form">
              {field('Dado de golpe', <select className="pv-input" value={form.hitDie}
                onChange={(event) => patch({ hitDie: Number(event.target.value) })}>
                <option value={6}>d6</option><option value={8}>d8</option>
                <option value={10}>d10</option><option value={12}>d12</option>
              </select>)}
              {field('Atributos principales (separados por coma)', input('primaryAbilities', 'Fuerza, Constitución'))}
              {field('Salvaciones', input('savingThrows', 'Fuerza, Sabiduría'))}
              {field('Nº de habilidades elegibles', <input className="pv-input" type="number" value={form.skillChoices}
                onChange={(event) => patch({ skillChoices: Number(event.target.value) })} />)}
              {field('Armaduras', input('armor', 'Ligera, media'))}
              {field('Armas', input('weapons', 'Simples, marciales'))}
            </div>
          ) : null}

          {step.id === 'equipo' ? (
            <div className="hc__form">
              {field('Equipo inicial', textarea('equipment', 'Espada larga, arco corto, cota de malla'))}
              <p className="hc__hint">Puedes añadir objetos concretos desde la biblioteca y vincularlos después en el editor.</p>
            </div>
          ) : null}

          {step.id === 'habilidades' ? (
            <div className="hc__form">
              {field('Habilidades y pasivas', textarea('abilities', 'Segundo viento, Ojo de águila'))}
              <p className="hc__hint">Escribe los nombres de habilidades de la biblioteca que quieres reutilizar. El editor permite vincularlas como relaciones.</p>
            </div>
          ) : null}

          {step.id === 'progresion' ? (
            <div className="hc__form">
              {field('Progresión por niveles', textarea('progression', 'Nivel 3: sendero del Sellador · Nivel 7: resistencia'))}
              {field('Elecciones del jugador', textarea('choices', 'Al crear: estilo de combate · Al subir de nivel: especialización'))}
            </div>
          ) : null}

          {step.id === 'publicar' ? (
            <div className="hc__publish">
              <h3>Así la verán los jugadores</h3>
              <div className="hc__preview">
                <img src="/icons/broadsword.svg" alt="" />
                <div><strong>{form.name || 'Nueva clase'}</strong><span>{form.concept || form.playstyle || 'Sin concepto todavía'}</span></div>
              </div>
              <p className="hc__notice">
                Publicar crea una revisión. Los personajes que ya usan esta clase conservan su versión hasta que deciden actualizar.
              </p>
              <Button variant="primary" disabled={busy} onClick={() => void publish()}>
                {item?.workflowState === 'published' ? 'Clase publicada ✓' : 'Guardar y publicar'}
              </Button>
            </div>
          ) : null}

          <div className="hc__nav">
            <Button variant="secondary" disabled={stepIndex === 0} onClick={() => setStepIndex((i) => i - 1)}>Atrás</Button>
            {stepIndex < STEPS.length - 1 ? (
              <Button variant="primary" onClick={() => setStepIndex((i) => i + 1)}>Siguiente</Button>
            ) : null}
          </div>
        </section>

        <aside className="hc__aside" aria-label="Resumen de la clase">
          <span className="hc__label">TU CLASE</span>
          <div><span>Nombre</span><strong>{form.name || 'Sin nombre'}</strong></div>
          <div><span>Origen</span><strong>{originId ? classes.find((c) => c.id === originId)?.name : 'Desde cero'}</strong></div>
          <div><span>Dado</span><strong>d{form.hitDie}</strong></div>
          <div><span>Estado</span><strong>{item ? item.workflowState : 'Borrador sin guardar'}</strong></div>
        </aside>
      </div>
    </main>
  )
}
