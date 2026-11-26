import { useCallback, useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { apiFetch, apiFetchBlob, getApiToken } from '../api/client'
import type { CampaignDetail, ContentItemDto } from '../api/types'
import type { BlueprintStepDef, BuildPreviewResponse } from '../api/types'
import {
  fetchBlueprint,
  fetchChoiceContent,
  finishCharacter,
  previewBuild,
  saveDraftActor,
  type BuildStepsState,
} from '../api/characters'
import { Button, useToast } from '../ui'
import { contentIcon } from '../ui/contentIcons'
import { t } from '../i18n'
import { useNavigate } from '../routing/router'
import './character-creator.css'

/**
 * Creador de personajes real (PMB-3.5, ADR-034): asistente por pasos
 * dirigido por `CharacterBlueprint` (datos), con elecciones sobre el
 * contenido real de la campaña (SRD incluido), efectos visibles, validación
 * y estadísticas derivadas del **servidor** (`build/preview`), borrador
 * persistente en el Actor (`build`) y contenido fijado con `ItemUsage` al
 * terminar. Composición del prototipo aprobado (pmb-3.4-creador.md).
 */

const ABILITIES = ['strength', 'dexterity', 'constitution', 'intelligence', 'wisdom', 'charisma']
const POINT_BUY_COST: Record<number, number> = {
  8: 0, 9: 1, 10: 2, 11: 3, 12: 4, 13: 5, 14: 7, 15: 9,
}

/**
 * Bonificación pendiente de elegir: el servidor la marca `applied:false`
 * cuando el contenido no fija el atributo. Las opciones viajan en el propio
 * objeto cuando el sistema las declara; sin ellas solo se muestra un texto
 * honesto (nunca un control que no pueda aplicarse).
 */
type PendingBonus = {
  ability: string
  bonus: number
  applied: boolean
  options?: Array<string | { ability?: string; name?: string; value?: string }>
}

function bonusKey(bonus: PendingBonus, index: number): string {
  return `${index}:${bonus.ability}:${bonus.bonus}`
}

function bonusOptions(bonus: PendingBonus): string[] {
  return (bonus.options ?? [])
    .map((option) =>
      typeof option === 'string'
        ? option
        : option.ability ?? option.name ?? option.value ?? '',
    )
    .filter((option) => option.trim().length > 0)
}

interface Props {
  campaign: CampaignDetail
  /** Borrador guardado (continuar después). */
  draftActorId?: string
  onDone: () => void
  onExit: () => void
}

interface DraftActor {
  id: string
  version: number
  name: string
}

function modifier(score: number): number {
  return Math.floor((score - 10) / 2)
}

function modText(score: number): string {
  const mod = modifier(score)
  return mod >= 0 ? `+${mod}` : `${mod}`
}

/** Efectos visibles de una elección (propiedades del contenido real). */
function itemEffects(item: ContentItemDto, includeTraits = true): string[] {
  const props = item.properties as Record<string, unknown>
  const lines: string[] = []
  const bonuses = props.abilityBonuses as Array<{ ability: string; bonus: number }> | undefined
  if (Array.isArray(bonuses)) {
    lines.push(bonuses.map((b) => `${b.ability} +${b.bonus}`).join(', '))
  }
  if (typeof props.speed === 'number') {
    lines.push(t.creador.speed(props.speed))
  }
  const languages = props.languages as string[] | undefined
  if (Array.isArray(languages) && languages.length > 0) {
    lines.push(t.creador.languages(languages.join(', ')))
  }
  if (typeof props.hitDie === 'number') {
    const saves = props.savingThrows as string[] | undefined
    lines.push(`d${props.hitDie}${saves ? ` · ${saves.join(', ')}` : ''}`)
  }
  const skills = props.skillProficiencies as string[] | undefined
  if (Array.isArray(skills) && skills.length > 0) {
    lines.push(skills.join(', '))
  }
  if (typeof props.feature === 'string') {
    lines.push(props.feature)
  }
  if (includeTraits) {
    const traits = props.traits as string[] | undefined
    if (Array.isArray(traits)) {
      lines.push(...traits)
    }
  }
  return lines
}

/**
 * Los mensajes del servidor citan claves de paso ('identity'): se muestran
 * con el título localizado del blueprint («Identidad»).
 */
function localizeErrors(messages: string[], stepsDef: BlueprintStepDef[]): string[] {
  return messages.map((message) => {
    let result = message
    for (const entry of stepsDef) {
      result = result.replaceAll(`'${entry.key}'`, `«${entry.title}»`)
    }
    return result
  })
}

export function CharacterCreatorView({ campaign, draftActorId, onDone, onExit }: Props) {
  const toast = useToast()
  const navigate = useNavigate()
  const [stepsDef, setStepsDef] = useState<BlueprintStepDef[] | null>(null)
  const [blueprintKey, setBlueprintKey] = useState('standard')
  const [raceItems, setRaceItems] = useState<ContentItemDto[]>([])
  const [classes, setClasses] = useState<ContentItemDto[]>([])
  const [backgrounds, setBackgrounds] = useState<ContentItemDto[]>([])
  const [steps, setSteps] = useState<BuildStepsState>({
    attributes: {
      method: 'pointBuy',
      values: Object.fromEntries(ABILITIES.map((a) => [a, 8])),
    },
    skills: { chosen: [] },
  })
  const [draft, setDraft] = useState<DraftActor | null>(null)
  const [portraitId, setPortraitId] = useState<string | null>(null)
  const [portraitUrl, setPortraitUrl] = useState<string | null>(null)
  const [frameMode, setFrameMode] = useState(false)
  const [crop, setCrop] = useState({ scale: 1, x: 50, y: 50 })
  const dragRef = useRef<{ x: number; y: number; startX: number; startY: number } | null>(null)
  const [imageNotice, setImageNotice] = useState<string | null>(null)
  const [stepIndex, setStepIndex] = useState(0)
  const [preview, setPreview] = useState<BuildPreviewResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  // Los errores del servidor solo se muestran tras interactuar (polido:
  // entrar al asistente no debe saludar con un error).
  const [interacted, setInteracted] = useState(false)

  // --- Carga inicial (blueprint + contenido + borrador) ----------------
  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const blueprint = await fetchBlueprint()
        const [raceList, classList, backgroundList] = await Promise.all([
          fetchChoiceContent('race', campaign.id),
          fetchChoiceContent('class', campaign.id),
          fetchChoiceContent('background', campaign.id),
        ])
        if (cancelled) {
          return
        }
        setBlueprintKey(blueprint.key)
        setStepsDef(blueprint.steps)
        setRaceItems(raceList)
        setClasses(classList.filter((item) => item.parentId === null))
        setBackgrounds(backgroundList.filter((item) => item.parentId === null))

        if (draftActorId) {
          const actor = await apiFetch<{
            id: string
            version: number
            name: string
            build: Record<string, unknown> | null
            portraitId: string | null
          }>(`/api/v1/actors/${draftActorId}`)
          if (cancelled) {
            return
          }
          setDraft({ id: actor.id, version: actor.version, name: actor.name })
          if (actor.portraitId) setPortraitId(actor.portraitId)
          if (actor.build && typeof actor.build === 'object') {
            const restored = actor.build as BuildStepsState & {
              step?: number
              portraitCrop?: { scale: number; x: number; y: number }
            }
            setSteps(restored)
            if (typeof restored.step === 'number') {
              setStepIndex(restored.step)
            }
            if (restored.portraitCrop) setCrop(restored.portraitCrop)
          }
        }
      } catch (cause) {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : t.creador.loadError)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [campaign.id, draftActorId])

  // --- Prevalidación en servidor (autoridad de reglas) -----------------
  const stepsKey = JSON.stringify(steps)
  useEffect(() => {
    if (!stepsDef) {
      return
    }
    let cancelled = false
    void previewBuild(campaign.id, blueprintKey, steps)
      .then((response) => {
        if (!cancelled) {
          setPreview(response)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setPreview(null)
        }
      })
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stepsKey, stepsDef, campaign.id, blueprintKey])

  // La imagen del personaje se sirve como blob autenticado (sin credenciales
  // en la URL). Cambiarla no altera el borrador ni las elecciones (EXP-1).
  useEffect(() => {
    let cancelled = false
    let objectUrl: string | null = null
    if (!portraitId) {
      setPortraitUrl(null)
      return
    }
    void apiFetchBlob(`/api/v1/images/${portraitId}/file`)
      .then((blob) => {
        if (!cancelled) {
          objectUrl = URL.createObjectURL(blob)
          setPortraitUrl(objectUrl)
        }
      })
      .catch(() => { if (!cancelled) setPortraitUrl(null) })
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [portraitId])

  async function uploadPortrait(file: File) {
    if (!file.type.startsWith('image/')) {
      setImageNotice(t.creador.portraitError)
      return
    }
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const response = await fetch('/api/v1/images', {
        method: 'POST',
        headers: { Authorization: `Bearer ${getApiToken() ?? ''}` },
        body: form,
      })
      if (!response.ok) throw new Error('upload failed')
      const asset = (await response.json()) as { id: string }
      setPortraitId(asset.id)
      setImageNotice(t.creador.portraitUploaded)
    } catch {
      setImageNotice(t.creador.portraitError)
    } finally {
      setBusy(false)
    }
  }

  const update = useCallback((patch: BuildStepsState) => {
    setInteracted(true)
    setSteps((current) => ({ ...current, ...patch }))
  }, [])

  /**
   * La elección vive en el borrador (`build.bonusChoices`): el servidor sigue
   * siendo la autoridad de las reglas y solo aplica las bonificaciones que
   * resuelve desde el contenido.
   */
  const chooseBonus = useCallback((key: string, ability: string) => {
    setInteracted(true)
    setSteps((current) => {
      const choices = (current.bonusChoices ?? {}) as Record<string, string>
      return { ...current, bonusChoices: { ...choices, [key]: ability } }
    })
  }, [])

  const bonusChoices = (steps.bonusChoices ?? {}) as Record<string, string>

  const stepResults = useMemo(() => {
    const map = new Map<string, { ok: boolean; errors: string[] }>()
    for (const result of preview?.stepResults ?? []) {
      map.set(result.step, { ok: result.ok, errors: result.errors })
    }
    return map
  }, [preview])

  const goToStep = useCallback((index: number) => {
    setInteracted(true)
    setStepIndex(index)
  }, [])

  const baseRaces = raceItems.filter((item) => item.parentId === null)
  const raceItem = raceItems.find((item) => item.id === steps.race?.itemId) ?? null
  const raceVariants = raceItem
    ? raceItems.filter((item) => item.parentId === raceItem.id)
    : []
  const classItem = classes.find((item) => item.id === steps.class?.itemId) ?? null
  const backgroundItem = backgrounds.find((item) => item.id === steps.background?.itemId) ?? null
  const classProperties = (classItem?.properties ?? {}) as Record<string, unknown>
  const skillQuota = typeof classProperties.skillChoices === 'number' ? classProperties.skillChoices : 0

  const spent = ABILITIES.reduce(
    (total, ability) => total + (POINT_BUY_COST[steps.attributes?.values?.[ability] ?? 8] ?? 0),
    0,
  )

  async function saveDraft() {
    const name = steps.identity?.name?.trim()
    if (!name) {
      setError(t.creador.noName)
      return
    }
    setBusy(true)
    setError(null)
    try {
      const actor = await saveDraftActor(
        campaign.id,
        name,
        { ...steps, step: stepIndex, portraitCrop: crop },
        draft ? { id: draft.id, version: draft.version } : undefined,
        portraitId,
      )
      setDraft({ id: actor.id, version: actor.version, name: actor.name })
      toast.show({ title: t.creador.savedToast, tone: 'success' })
      onExit()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.creador.saveError)
    } finally {
      setBusy(false)
    }
  }

  async function create() {
    setBusy(true)
    setError(null)
    try {
      const response = await previewBuild(campaign.id, blueprintKey, steps)
      setPreview(response)
      if (!response.valid || !steps.identity?.name?.trim()) {
        setError(t.creador.stepErrors)
        setBusy(false)
        return
      }
      const abilities = Object.fromEntries(
        ABILITIES.map((ability) => [ability, response.derivedStats.abilities[ability]?.score ?? 10]),
      )
      const usageItemIds = [
        steps.race?.itemId,
        steps.race?.variantItemId,
        steps.class?.itemId,
        steps.background?.itemId,
      ].filter((id): id is string => Boolean(id))
      await finishCharacter({
        campaign,
        actor: draft ? { id: draft.id, version: draft.version } : undefined,
        portraitId,
        name: steps.identity.name.trim(),
        attributes: abilities,
        systemData: {
          blueprint: `dnd5e/${blueprintKey}`,
          choices: {
            race: steps.race ?? null,
            class: steps.class ?? null,
            background: steps.background ?? null,
          },
          derived: response.derivedStats,
          portraitCrop: crop,
        },
        usageItemIds,
      })
      toast.show({ title: t.creador.createdToast, tone: 'success' })
      onDone()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.creador.createError)
    } finally {
      setBusy(false)
    }
  }

  if (!stepsDef) {
    return (
      <main className="creador">
        <p className="creador-hint">{error ?? t.creador.loading}</p>
      </main>
    )
  }

  const step = stepsDef[Math.min(stepIndex, stepsDef.length - 1)]
  const currentResult = stepResults.get(step.key)
  const isLast = stepIndex === stepsDef.length - 1

  return (
    <main className="creador">
      <header className="creador__head">
        <h1 className="creador__title">
          {draft ? t.creador.continueTitle(campaign.name) : t.creador.title(campaign.name)}
        </h1>
        {draft ? <span className="creador__draft">{t.creador.draftBadge}</span> : null}
        <span className="creador__step">
          {t.creador.stepCounter(stepIndex + 1, stepsDef.length)}
        </span>
      </header>
      <div className="creador__body">
        <nav className="creador__rail" aria-label={step.title}>
          {stepsDef.map((entry, index) => {
            const result = stepResults.get(entry.key)
            return (
              <button
                key={entry.key}
                type="button"
                className={`creador__rail-item${index === stepIndex ? ' creador__rail-item--active' : ''}`}
                aria-current={index === stepIndex ? 'page' : undefined}
                onClick={() => goToStep(index)}
              >
                {result?.ok ? '✓ ' : result && !result.ok ? '! ' : ''}
                {entry.title}
                {entry.optional ? ` (${t.creador.optional})` : ''}
              </button>
            )
          })}
        </nav>

        <div className="creador__content">
          <section className="creador-section">
            <h2 className="creador-section__title">{step.title}</h2>
            {interacted && currentResult && currentResult.errors.length > 0 ? (
              <ul className="creador-errors" role="alert">
                {localizeErrors(currentResult.errors, stepsDef).map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            ) : null}

            {step.kind === 'identity' ? (
              <>
                <label className="creador-field">
                  {t.creador.nameLabel}
                  <input
                    className="pv-input"
                    value={steps.identity?.name ?? ''}
                    maxLength={100}
                    placeholder={t.creador.namePlaceholder}
                    onChange={(event) =>
                      update({ identity: { ...steps.identity, name: event.target.value } })
                    }
                  />
                </label>
                <label className="creador-field">
                  {t.creador.descriptionLabel}
                  <textarea
                    className="pv-input"
                    rows={3}
                    value={steps.identity?.description ?? ''}
                    onChange={(event) =>
                      update({ identity: { ...steps.identity, description: event.target.value } })
                    }
                  />
                </label>
                <label className="creador-field">
                  {t.creador.personalityLabel}
                  <textarea
                    className="pv-input"
                    rows={3}
                    value={steps.identity?.personality ?? ''}
                    onChange={(event) =>
                      update({ identity: { ...steps.identity, personality: event.target.value } })
                    }
                  />
                </label>
              </>
            ) : null}

            {step.kind === 'choice' && step.contentType ? (
              <ChoiceStep
                items={
                  step.contentType === 'race'
                    ? baseRaces
                    : step.contentType === 'class'
                      ? classes
                      : backgrounds
                }
                selectedId={
                  step.contentType === 'race'
                    ? steps.race?.itemId
                    : step.contentType === 'class'
                      ? steps.class?.itemId
                      : steps.background?.itemId
                }
                variantId={steps.race?.variantItemId ?? null}
                variants={step.contentType === 'race' ? raceVariants : []}
                onSelect={(itemId) =>
                  update(
                    step.contentType === 'race'
                      ? { race: { itemId, variantItemId: null } }
                      : step.contentType === 'class'
                        ? { class: { itemId } }
                        : { background: { itemId } },
                  )
                }
                onSelectVariant={(variantItemId) =>
                  update({ race: { ...steps.race, variantItemId } })
                }
              />
            ) : null}

            {step.contentType === 'class' ? (
              <div className="creador-create-class">
                <Button variant="ghost" onClick={() => navigate({ kind: 'clase-crear' })}>
                  ＋ Crear clase propia
                </Button>
                <span>Crea una clase homebrew y publícala cuando quieras usarla en el creador.</span>
              </div>
            ) : null}

            {step.kind === 'choice' && step.validation?.countFromClass === true ? (
              <SkillsStep
                options={step.options ?? []}
                chosen={steps.skills?.chosen ?? []}
                quota={skillQuota}
                needClass={!classItem}
                onChange={(chosen) => update({ skills: { chosen } })}
              />
            ) : null}

            {step.kind === 'attributes' ? (
              <AttributesStep
                method={steps.attributes?.method ?? 'pointBuy'}
                values={steps.attributes?.values ?? Object.fromEntries(ABILITIES.map((a) => [a, 8]))}
                spent={spent}
                onChange={(method, values) => update({ attributes: { method, values } })}
              />
            ) : null}

            {step.kind === 'equipment' ? <p className="creador-hint">{t.creador.equipmentNote}</p> : null}
            {step.kind === 'text' ? <p className="creador-hint">{t.creador.storyNote}</p> : null}

            {step.kind === 'review' ? (
              <ReviewStep
                steps={steps}
                preview={preview}
                race={raceItem}
                raceVariant={raceVariants.find((v) => v.id === steps.race?.variantItemId) ?? null}
                classItem={classItem}
                background={backgroundItem}
                bonusChoices={bonusChoices}
                onChooseBonus={chooseBonus}
              />
            ) : null}
          </section>

          <div className="creador__nav">
            <Button
              variant="secondary"
              disabled={stepIndex === 0}
              onClick={() => goToStep(Math.max(0, stepIndex - 1))}
            >
              {t.creador.back}
            </Button>
            <Button variant="ghost" disabled={busy} onClick={() => void saveDraft()}>
              {t.creador.saveExit}
            </Button>
            {isLast ? (
              <Button variant="primary" disabled={busy} onClick={() => void create()}>
                {t.creador.create}
              </Button>
            ) : (
              <Button
                variant="primary"
                onClick={() => goToStep(Math.min(stepsDef.length - 1, stepIndex + 1))}
              >
                {t.creador.next}
              </Button>
            )}
          </div>
          {error ? <p className="error" role="alert">{error}</p> : null}
        </div>

        <aside className="creador__portrait" aria-label={t.creador.portraitLabel}>
          <span className="creador__label">{t.creador.portraitLabel}</span>
          <div
            className={`creador__portrait-frame${frameMode ? ' is-framing' : ''}`}
            onPointerDown={(event: ReactPointerEvent<HTMLDivElement>) => {
              if (!frameMode || !portraitUrl) return
              dragRef.current = { x: event.clientX, y: event.clientY, startX: crop.x, startY: crop.y }
              event.currentTarget.setPointerCapture(event.pointerId)
            }}
            onPointerMove={(event: ReactPointerEvent<HTMLDivElement>) => {
              const drag = dragRef.current
              if (!drag) return
              const rect = event.currentTarget.getBoundingClientRect()
              setCrop((current) => ({
                ...current,
                x: Math.max(0, Math.min(100,
                  drag.startX - (event.clientX - drag.x) / rect.width * 100)),
                y: Math.max(0, Math.min(100,
                  drag.startY - (event.clientY - drag.y) / rect.height * 100)),
              }))
            }}
            onPointerUp={() => { dragRef.current = null }}
            onPointerCancel={() => { dragRef.current = null }}
          >
            {portraitUrl ? (
              <img src={portraitUrl} alt="" className="creador__portrait-image"
                style={{ objectPosition: `${crop.x}% ${crop.y}%`, transform: `scale(${crop.scale})` }} />
            ) : (
              <img src="/icons/rogue.svg" alt="" className="creador__portrait-placeholder" />
            )}
            {frameMode ? <div className="creador__crop-grid" aria-hidden="true" /> : null}
          </div>
          {frameMode && portraitUrl ? (
            <label className="creador__zoom">
              Zoom
              <input type="range" min={1} max={2} step={0.05} value={crop.scale}
                onChange={(event) => setCrop((current) => ({ ...current, scale: Number(event.target.value) }))} />
            </label>
          ) : null}
          <div className="creador__portrait-actions">
            <label className="pv-btn pv-btn--primary creador__upload">
              {t.creador.portraitUpload}
              <input type="file" accept="image/png,image/jpeg,image/webp" hidden
                disabled={busy}
                onChange={(event) => {
                  const file = event.target.files?.[0]
                  if (file) void uploadPortrait(file)
                  event.target.value = ''
                }}
              />
            </label>
            <Button variant="ghost" disabled={!portraitId || busy}
              onClick={() => {
                const input = document.querySelector<HTMLInputElement>('.creador__upload input')
                input?.click()
              }}>
              {t.creador.portraitReplace}
            </Button>
            <Button variant="ghost" disabled={!portraitId}
              onClick={() => setFrameMode((current) => !current)}>
              {frameMode ? t.creador.portraitFrameDone : t.creador.portraitFrame}
            </Button>
          </div>
          <p className="creador__notice" role="status">
            {imageNotice ?? t.creador.portraitHint}
          </p>
          <div className="creador__live">
            <span className="creador__label">{t.creador.liveSummary}</span>
            <p>{steps.identity?.name || t.creador.noName}</p>
            <p>{raceItems.find((item) => item.id === steps.race?.itemId)?.name ?? t.creador.noRaceYet}</p>
            <p>{classes.find((item) => item.id === steps.class?.itemId)?.name ?? t.creador.noClassYet}</p>
          </div>
        </aside>
      </div>
    </main>
  )
}

function ChoiceStep({
  items,
  selectedId,
  variantId,
  variants,
  onSelect,
  onSelectVariant,
}: {
  items: ContentItemDto[]
  selectedId?: string
  variantId: string | null
  variants: ContentItemDto[]
  onSelect: (itemId: string) => void
  onSelectVariant: (variantItemId: string | null) => void
}) {
  const selected = items.find((item) => item.id === selectedId) ?? null
  const [expandedId, setExpandedId] = useState<string | null>(null)
  return (
    <>
      <div className="creador-grid">
        {items.map((item, index) => {
          const icon = contentIcon(item.typeKey, item.name, item.tags)
          const effects = itemEffects(item, false)
          return (
            <article
              key={item.id}
              className={`creador-card${selectedId === item.id ? ' creador-card--on' : ''}`}
            >
              <button type="button" className="creador-card__pick"
                aria-pressed={selectedId === item.id}
                onClick={() => onSelect(item.id)}>
                <span className="creador-card__art">
                  {icon ? <img src={icon} alt="" /> : item.name.slice(0, 1).toUpperCase()}
                </span>
                <span className="creador-card__copy">
                  <strong>{item.name}</strong>
                  {item.summary ? <span className="creador-card__tagline">{item.summary}</span> : null}
                  {index === 0 ? (
                    <span className="creador-card__recommended" title={t.creador.recommendedWhy}>
                      {t.creador.recommendedForNew}
                    </span>
                  ) : null}
                </span>
              </button>
              <div className="creador-card__foot">
                <button type="button" onClick={() => setExpandedId(expandedId === item.id ? null : item.id)}>
                  {expandedId === item.id ? t.creador.hideDetails : t.creador.whatItGives}
                </button>
                {selectedId === item.id ? <span className="creador-card__picked">✓</span> : null}
              </div>
              {expandedId === item.id ? (
                <div className="creador-card__detail">
                  {effects.length > 0 ? <ul>{effects.map((line) => <li key={line}>{line}</li>)}</ul> : null}
                  {index === 0 ? <p className="creador-card__why">{t.creador.recommendedWhy}</p> : null}
                </div>
              ) : null}
            </article>
          )
        })}
      </div>
      {variants.length > 0 ? (
        <div className="creador-chips">
          <span className="creador-chips__label">{t.creador.variantLabel}</span>
          {variants.map((variant) => (
            <button
              key={variant.id}
              type="button"
              className={`creador-chip${variantId === variant.id ? ' creador-chip--on' : ''}`}
              onClick={() => onSelectVariant(variant.id)}
            >
              {variant.name}
            </button>
          ))}
        </div>
      ) : null}
      {selected ? (
        <div className="creador-effects">
          <h3 className="creador-effects__title">{t.creador.effectsTitle}</h3>
          <ul>
            {itemEffects(selected).map((line) => (
              <li key={line}>{line}</li>
            ))}
            {variants
              .filter((variant) => variant.id === variantId)
              .map((variant) =>
                itemEffects(variant).map((line) => <li key={`${variant.id}-${line}`}>{line}</li>),
              )}
          </ul>
        </div>
      ) : (
        <p className="creador-empty">{t.creador.emptyChoice}</p>
      )}
    </>
  )
}

function SkillsStep({
  options,
  chosen,
  quota,
  needClass,
  onChange,
}: {
  options: string[]
  chosen: string[]
  quota: number
  needClass: boolean
  onChange: (chosen: string[]) => void
}) {
  return (
    <>
      <h3 className="creador-effects__title">{t.creador.skillsTitle(quota)}</h3>
      <div className="creador-chips">
        {options.map((skill) => (
          <button
            key={skill}
            type="button"
            className={`creador-chip${chosen.includes(skill) ? ' creador-chip--on' : ''}`}
            onClick={() =>
              onChange(
                chosen.includes(skill)
                  ? chosen.filter((entry) => entry !== skill)
                  : [...chosen, skill],
              )
            }
          >
            {skill}
          </button>
        ))}
      </div>
      <p className="creador-hint">
        {needClass
          ? t.creador.skillsNeedClass
          : t.creador.skillsChosen(chosen.length, quota, chosen.join(', ') || t.creador.skillsNone)}
      </p>
    </>
  )
}

function AttributesStep({
  method,
  values,
  spent,
  onChange,
}: {
  method: 'manual' | 'pointBuy'
  values: Record<string, number>
  spent: number
  onChange: (method: 'manual' | 'pointBuy', values: Record<string, number>) => void
}) {
  function bump(ability: string, delta: number) {
    const next = Math.min(15, Math.max(8, (values[ability] ?? 8) + delta))
    onChange(method, { ...values, [ability]: next })
  }

  return (
    <>
      <div className="creador-segmented" role="group" aria-label={t.creador.methodPointBuy}>
        <button
          type="button"
          className={`creador-chip${method === 'pointBuy' ? ' creador-chip--on' : ''}`}
          onClick={() =>
            onChange('pointBuy', Object.fromEntries(ABILITIES.map((a) => [a, Math.min(values[a] ?? 8, 15)])))
          }
        >
          {t.creador.methodPointBuy}
        </button>
        <button
          type="button"
          className={`creador-chip${method === 'manual' ? ' creador-chip--on' : ''}`}
          onClick={() => onChange('manual', values)}
        >
          {t.creador.methodManual}
        </button>
      </div>
      {method === 'pointBuy' ? (
        <p className="creador-budget">{t.creador.budget(spent)}</p>
      ) : null}
      <div className="creador-abilities">
        {ABILITIES.map((ability) => (
          <div key={ability} className="creador-ability">
            <span>{(t.creador.abilities as Record<string, string>)[ability]}</span>
            {method === 'pointBuy' ? (
              <>
                <button type="button" aria-label={t.creador.minusAbility(ability)} onClick={() => bump(ability, -1)}>
                  −
                </button>
                <strong>{values[ability] ?? 8}</strong>
                <button type="button" aria-label={t.creador.plusAbility(ability)} onClick={() => bump(ability, 1)}>
                  +
                </button>
                <span className="creador-ability__cost">
                  {POINT_BUY_COST[values[ability] ?? 8] ?? 0} pts
                </span>
              </>
            ) : (
              <input
                className="pv-input"
                type="number"
                min={3}
                max={20}
                value={values[ability] ?? 8}
                aria-label={ability}
                onChange={(event) =>
                  onChange('manual', {
                    ...values,
                    [ability]: Math.min(20, Math.max(3, Number(event.target.value) || 3)),
                  })
                }
              />
            )}
            <span className="creador-ability__mod">{modText(values[ability] ?? 8)}</span>
          </div>
        ))}
      </div>
      <p className="creador-hint">{t.creador.serverRules}</p>
    </>
  )
}

function ReviewStep({
  steps,
  preview,
  race,
  raceVariant,
  classItem,
  background,
  bonusChoices,
  onChooseBonus,
}: {
  steps: BuildStepsState
  preview: BuildPreviewResponse | null
  race: ContentItemDto | null
  raceVariant: ContentItemDto | null
  classItem: ContentItemDto | null
  background: ContentItemDto | null
  bonusChoices: Record<string, string>
  onChooseBonus: (key: string, ability: string) => void
}) {
  const derived = preview?.derivedStats
  return (
    <>
      <div className="creador-review">
        <div className="creador-effects">
          <h3 className="creador-effects__title">{t.creador.reviewIdentity}</h3>
          <ul>
            <li>{steps.identity?.name?.trim() || t.creador.noName}</li>
            <li>{t.creador.level(derived?.level ?? 1)}</li>
          </ul>
        </div>
        <div className="creador-effects">
          <h3 className="creador-effects__title">{t.creador.reviewRace}</h3>
          <ul>
            <li>
              {race
                ? `${race.name}${raceVariant ? ` (${raceVariant.name})` : ''}`
                : t.creador.noRace}
            </li>
            {race ? itemEffects(raceVariant ?? race).map((line) => <li key={line}>{line}</li>) : null}
          </ul>
        </div>
        <div className="creador-effects">
          <h3 className="creador-effects__title">{t.creador.reviewClassBg}</h3>
          <ul>
            <li>{classItem ? classItem.name : t.creador.noClass}</li>
            <li>{background ? background.name : t.creador.noBackground}</li>
            {(steps.skills?.chosen ?? []).length > 0 ? (
              <li>{(steps.skills?.chosen ?? []).join(', ')}</li>
            ) : null}
          </ul>
        </div>
        <div className="creador-effects">
          <h3 className="creador-effects__title">{t.creador.reviewAbilities}</h3>
          <ul>
            {ABILITIES.map((ability) => {
              const entry = derived?.abilities[ability]
              return (
                <li key={ability}>
                  {(t.creador.abilities as Record<string, string>)[ability]}{' '}
                  {entry ? `${entry.score} (${modText(entry.score)})` : '—'}
                </li>
              )
            })}
            {derived ? <li>{t.creador.speed(derived.speed)}</li> : null}
            {derived ? <li>{t.creador.languages(derived.languages.join(', '))}</li> : null}
            {(derived?.pendingBonuses ?? []).map((entry, index) => {
              const bonus = entry as PendingBonus
              const key = bonusKey(bonus, index)
              const options = bonusOptions(bonus)
              const abilityLabel = (ability: string) =>
                (t.creador.abilities as Record<string, string>)[ability] ?? ability
              if (!bonus.applied && options.length > 0) {
                return (
                  <li key={key}>
                    <select
                      aria-label={t.creador.pendingBonus(bonus.bonus, bonus.ability)}
                      value={bonusChoices[bonus.ability] ?? ''}
                      onChange={(event) => onChooseBonus(bonus.ability, event.target.value)}
                    >
                      <option value="">
                        {t.creador.pendingBonus(bonus.bonus, bonus.ability)}
                      </option>
                      {options.map((option) => (
                        <option key={option} value={option}>
                          +{bonus.bonus} {abilityLabel(option)}
                        </option>
                      ))}
                    </select>
                  </li>
                )
              }
              return (
                <li key={key}>
                  {bonus.applied
                    ? `+${bonus.bonus} ${abilityLabel(bonus.ability)}`
                    : t.creador.pendingBonus(bonus.bonus, bonus.ability)}
                </li>
              )
            })}
          </ul>
        </div>
      </div>
    </>
  )
}
