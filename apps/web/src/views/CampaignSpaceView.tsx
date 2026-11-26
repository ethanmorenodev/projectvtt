import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { ChangeEvent, KeyboardEvent } from 'react'
import { apiFetch } from '../api/client'
import { uploadCover, useCampaignCover } from '../api/covers'
import type {
  ActivityEntry,
  ActorDto,
  CampaignDetail,
  CampaignMember,
  ContentItemDto,
  SceneDetail,
  SceneSummary,
} from '../api/types'
import { Badge, Button, EmptyState, List, ListRow, useToast } from '../ui'
import { contentCover } from '../ui/contentIcons'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Icon } from '../ui/Icon'
import { t } from '../i18n'
import { CampaignManageDialog } from './CampaignManageDialog'
import { NotesPanel } from './NotesPanel'
import { CampaignRealtime } from '../realtime/hub'
import { CampaignThumb, plural, relativeTime, SceneThumb } from './presentation'
import { ScenesView } from './ScenesView'
import './campaign-space.css'

/**
 * Espacio de campaña (PMB-2.7, rediseñado por el consejo de diseño 2026-11-24):
 * distribución «Tablero del director» con una sola acción dominante, las 13
 * secciones agrupadas en cinco bloques e inspector acoplado que absorbe notas
 * e historial (antes ventanas flotantes). Datos reales del servidor; permisos
 * y validación siguen en backend. Cadenas en i18n (ADR-038).
 */

interface Props {
  campaign: CampaignDetail
  onOpenLore: () => void
  onOpenScene: (scene: SceneDetail) => void
  onCreateCharacter: () => void
  onOpenActor?: (actorId: string) => void
  onOpenDraft: (actorId: string) => void
  onChanged: () => void
}

type SectionId =
  | 'resumen' | 'mesa' | 'escenas' | 'personajes' | 'npc' | 'criaturas'
  | 'biblioteca' | 'homebrew' | 'lore' | 'facciones' | 'localizaciones'
  | 'participantes' | 'configuracion'

type InspectorTab = 'detalles' | 'notas' | 'historial'
type Selection = { kind: 'scene' | 'actor' | 'content'; id: string } | null

const GROUPS: Array<{ label: string; items: Array<{ id: SectionId; label: string; soon?: boolean }> }> = [
  {
    label: t.campana.groups.sesion,
    items: [
      { id: 'resumen', label: t.campana.sections.resumen },
      { id: 'mesa', label: t.campana.sections.mesa },
      { id: 'escenas', label: t.campana.sections.escenas },
    ],
  },
  {
    label: t.campana.groups.reparto,
    items: [
      { id: 'personajes', label: t.campana.sections.personajes },
      { id: 'npc', label: t.campana.sections.npc },
    ],
  },
  {
    label: t.campana.groups.mundo,
    items: [
      { id: 'criaturas', label: t.campana.sections.criaturas },
      { id: 'facciones', label: t.campana.sections.facciones, soon: true },
      { id: 'localizaciones', label: t.campana.sections.localizaciones, soon: true },
      { id: 'lore', label: t.campana.sections.lore },
    ],
  },
  {
    label: t.campana.groups.biblioteca,
    items: [
      { id: 'biblioteca', label: t.campana.sections.biblioteca },
      { id: 'homebrew', label: t.campana.sections.homebrew },
    ],
  },
  {
    label: t.campana.groups.gestion,
    items: [
      { id: 'participantes', label: t.campana.sections.participantes },
      { id: 'configuracion', label: t.campana.sections.configuracion },
    ],
  },
]

const SECTION_LABELS = Object.fromEntries(
  GROUPS.flatMap((group) => group.items.map((item) => [item.id, item.label])),
) as Record<SectionId, string>

const CATALOG_SECTIONS = ['npc', 'criaturas', 'biblioteca', 'homebrew'] as const
type CatalogSection = (typeof CATALOG_SECTIONS)[number]

/** Entrada de presentación: actores y elementos de contenido comparten fila. */
interface CatalogEntry {
  id: string
  name: string
  typeKey: string
  scope: string
  tags?: readonly string[]
  summary?: string | null
}

function catalogTypeLabel(typeKey: string): string {
  const base = typeKey.split(':')[0]
  return (t.biblioteca.typeLabels as Record<string, string>)[base]
    ?? (t.campana.personajes.kinds as Record<string, string>)[base]
    ?? typeKey
}

function catalogScopeLabel(scope: string): string {
  return (t.biblioteca.scopes as Record<string, string>)[scope] ?? scope
}

function ContentCatalogSection({
  title, items, emptyText, onSelect,
}: {
  title: string
  items: CatalogEntry[]
  emptyText: string
  onSelect: (id: string) => void
}) {
  if (items.length === 0) {
    return <EmptyState icon={<Icon name="open-book" />} title={title} text={emptyText} />
  }
  return (
    <List>
      {items.map((item) => {
        const cover = contentCover(item.typeKey, item.name, item.tags)
        return (
          <ListRow
            key={item.id}
            title={item.name}
            badge={<Badge tone="neutral">
              {catalogTypeLabel(item.typeKey)} · {catalogScopeLabel(item.scope)}
            </Badge>}
            subtitle={item.summary ?? undefined}
            thumb={<img className="space__catalog-thumb" src={cover.src} alt="" />}
            onClick={() => onSelect(item.id)}
          />
        )
      })}
    </List>
  )
}

const LORE_SECTIONS = ['lore', 'facciones', 'localizaciones'] as const
type LoreSection = (typeof LORE_SECTIONS)[number]
const TABS: Array<{ id: InspectorTab; label: string }> = [
  { id: 'detalles', label: t.campana.inspectorTabs.detalles },
  { id: 'notas', label: t.campana.inspectorTabs.notas },
  { id: 'historial', label: t.campana.inspectorTabs.historial },
]

function toList<T>(data: unknown): T[] {
  return Array.isArray(data) ? (data as T[]) : []
}

export function CampaignSpaceView({
  campaign,
  onOpenLore,
  onOpenScene,
  onCreateCharacter,
  onOpenActor,
  onOpenDraft,
  onChanged,
}: Props) {
  const [section, setSection] = useState<SectionId>('resumen')
  const [scenes, setScenes] = useState<SceneSummary[]>([])
  const [actors, setActors] = useState<ActorDto[]>([])
  const [contentItems, setContentItems] = useState<ContentItemDto[]>([])
  const [actorQuery, setActorQuery] = useState('')
  const [members, setMembers] = useState<CampaignMember[]>([])
  const [activity, setActivity] = useState<ActivityEntry[]>([])
  const [manageOpen, setManageOpen] = useState(false)
  const [sceneToDelete, setSceneToDelete] = useState<SceneSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<Selection>(null)
  const [tab, setTab] = useState<InspectorTab>('detalles')
  const [coverBusy, setCoverBusy] = useState(false)
  const tabRefs = useRef<Array<HTMLButtonElement | null>>([])

  const toast = useToast()
  const coverUrl = useCampaignCover(campaign.id, campaign.hasCover)
  const canManage = campaign.role === 'Owner' || campaign.role === 'GM'
  const canChangeCover = campaign.role === 'Owner'

  const load = useCallback(async () => {
    const [scenesResult, actorsResult, membersResult, activityResult, contentResult] =
      await Promise.allSettled([
        apiFetch<unknown>(`/api/v1/campaigns/${campaign.id}/scenes`),
        apiFetch<unknown>(`/api/v1/campaigns/${campaign.id}/actors`),
        apiFetch<unknown>(`/api/v1/campaigns/${campaign.id}/members`),
        apiFetch<unknown>('/api/v1/activity?limit=20'),
        apiFetch<unknown>('/api/v1/content/items'),
      ])
    if (scenesResult.status === 'fulfilled') {
      setScenes(toList<SceneSummary>(scenesResult.value))
    } else {
      setError(t.campana.errors.loadScenes)
    }
    setActors(actorsResult.status === 'fulfilled' ? toList<ActorDto>(actorsResult.value) : [])
    setContentItems(contentResult.status === 'fulfilled' ? toList<ContentItemDto>(contentResult.value) : [])
    setMembers(membersResult.status === 'fulfilled' ? toList<CampaignMember>(membersResult.value) : [])
    setActivity(
      activityResult.status === 'fulfilled'
        ? toList<ActivityEntry>(activityResult.value).filter(
            (entry) => entry.campaignId === campaign.id,
          )
        : [],
    )
  }, [campaign.id])

  useEffect(() => {
    void load()
  }, [load])

  // ADR-045: `SceneDeleted` llega mientras esta vista está abierta; la HTTP
  // sigue siendo la fuente de verdad (también en reconexión), así que solo
  // refrescamos la lista. Esta vista no tenía hub: se conecta únicamente para
  // este evento (los demás eventos no cambian esta pantalla).
  const reloadScenes = useCallback(async () => {
    try {
      setScenes(toList<SceneSummary>(
        await apiFetch<unknown>(`/api/v1/campaigns/${campaign.id}/scenes`),
      ))
    } catch {
      setError(t.campana.errors.loadScenes)
    }
  }, [campaign.id])

  useEffect(() => {
    const rt = CampaignRealtime.create({
      onSceneDeleted: (event) => {
        setSelected((current) =>
          current?.kind === 'scene' && current.id === event.sceneId ? null : current,
        )
        void reloadScenes()
      },
    })
    void rt.start()
      .then(() => rt.joinCampaign(campaign.id))
      .catch(() => undefined)
    return () => { void rt.stop().catch(() => undefined) }
  }, [campaign.id, reloadScenes])

  async function openScene(scene: SceneSummary) {
    try {
      onOpenScene(await apiFetch<SceneDetail>(`/api/v1/scenes/${scene.id}`))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campana.errors.openScene)
    }
  }

  async function deleteScene(scene: SceneSummary) {
    setError(null)
    try {
      await apiFetch(`/api/v1/scenes/${scene.id}`, { method: 'DELETE' })
      setSelected((current) =>
        current?.kind === 'scene' && current.id === scene.id ? null : current,
      )
      await load()
      onChanged()
      toast.show({ title: t.campana.mesa.deleteSceneDone, tone: 'success' })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campana.mesa.deleteSceneError)
    }
  }

  async function changeCover(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    setCoverBusy(true)
    try {
      await uploadCover(campaign.id, file)
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campana.errors.loadScenes)
    } finally {
      setCoverBusy(false)
    }
  }

  const onTabKey = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return
    event.preventDefault()
    const visible = canManage ? TABS : TABS.filter((item) => item.id !== 'notas')
    const next = (index + (event.key === 'ArrowRight' ? 1 : visible.length - 1)) % visible.length
    setTab(visible[next].id)
    tabRefs.current[next]?.focus()
  }

  const playingScene = scenes.find((scene) => selected?.kind === 'scene' && scene.id === selected.id)
    ?? scenes[0]
  const scene = selected?.kind === 'scene' ? scenes.find((item) => item.id === selected.id) : undefined
  const actor = selected?.kind === 'actor' ? actors.find((item) => item.id === selected.id) : undefined
  const content = selected?.kind === 'content'
    ? contentItems.find((item) => item.id === selected.id)
    : undefined
  const label = SECTION_LABELS[section]
  const loreSection = LORE_SECTIONS.includes(section as LoreSection) ? section as LoreSection : null
  const worldSoon = loreSection === 'facciones' || loreSection === 'localizaciones'
  const catalogEntries = useMemo<Record<CatalogSection, CatalogEntry[]>>(() => ({
    npc: actors.filter((item) => item.kind === 'npc').map((item) => ({
      id: item.id,
      name: item.name,
      typeKey: 'npc',
      scope: 'campaign',
    })),
    criaturas: contentItems.filter((item) => item.typeKey === 'creature'),
    biblioteca: contentItems.filter(
      (item) => item.scope === 'campaign' && item.campaignId === campaign.id,
    ),
    homebrew: contentItems.filter((item) => item.scope === 'user'),
  }), [actors, contentItems, campaign.id])
  const actorNeedle = actorQuery.trim().toLowerCase()
  const visibleActors = useMemo(() => {
    if (!actorNeedle) return actors
    return actors.filter((item) => {
      const kindLabel =
        (t.campana.personajes.kinds as Record<string, string>)[item.kind] ?? item.kind
      return [item.name, kindLabel, item.kind].some(
        (value) => value.toLowerCase().includes(actorNeedle),
      )
    })
  }, [actors, actorNeedle])

  return (
    <main className="space space--council">
      <nav className="space__subnav" aria-label={t.campana.subnavLabel}>
        {GROUPS.map((group) => (
          <div className="space__group" key={group.label}>
            <h3 className="space__group-label">{group.label}</h3>
            {group.items.map((item) => (
              <button
                key={item.id}
                type="button"
                className={[
                  'space__subnav-item',
                  section === item.id ? 'space__subnav-item--active' : '',
                  item.soon ? 'space__subnav-item--soon' : '',
                ].filter(Boolean).join(' ')}
                aria-current={section === item.id ? 'page' : undefined}
                onClick={() => setSection(item.id)}
              >
                {item.label}
              </button>
            ))}
          </div>
        ))}
      </nav>

      <div className="space__content">
        <header className="space__head">
          <div className="space__cover">
            {coverUrl ? (
              <img src={coverUrl} alt="" aria-hidden="true" />
            ) : (
              <span aria-hidden="true">{campaign.name.trim().charAt(0).toUpperCase()}</span>
            )}
            {canChangeCover ? (
              <label className="space__cover-change" title={t.campana.coverChange}>
                {coverBusy ? '…' : t.campana.coverChange}
                <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml"
                  aria-label={t.campana.coverChange} onChange={(event) => void changeCover(event)} />
              </label>
            ) : null}
          </div>

          <div className="space__identity">
            <span className="space__eyebrow">
              {t.campana.campaignHeading(campaign.name)}
            </span>
            <div className="space__titleline">
              <h2 className="space__title">{campaign.name}</h2>
              <Badge tone={campaign.archivedAt ? 'neutral' : 'accent'}>
                {campaign.archivedAt ? t.campana.stateArchived : t.campana.stateActive}
              </Badge>
            </div>
            <p className="space__desc">{campaign.description ?? ''}</p>
          </div>

          <div className="space__head-actions">
            {playingScene ? (
              <Button variant="primary" onClick={() => void openScene(playingScene)}>
                {t.campana.resumen.openTable} · {playingScene.name.split(' · ')[0]}
              </Button>
            ) : null}
            <Button variant="secondary" onClick={() => setManageOpen(true)}>
              {t.campana.manage}
            </Button>
          </div>
        </header>

        {error ? <p className="error">{error}</p> : null}

        <div className="space__layout">
          <div className="space__main">
            {section !== 'resumen' ? <h3 className="space__section-title">{label}</h3> : null}

            {section === 'resumen' ? (
              <>
                <div className="space__figures" aria-label={t.campana.sections.resumen}>
                  <div><strong>{scenes.length}</strong><span>{t.campana.resumen.scenes(scenes.length)}</span></div>
                  <div><strong>{actors.length}</strong><span>{t.campana.resumen.characters(actors.length)}</span></div>
                  <div><strong>{campaign.memberCount}</strong><span>{t.campana.resumen.players(campaign.memberCount)}</span></div>
                  <div>
                    <strong>{activity.length ? relativeTime(activity[0].createdAt) : '—'}</strong>
                    <span>{t.campana.resumen.lastActivity('')}</span>
                  </div>
                </div>

                <div className="space__cards">
                  <section className="space__card">
                    <h4 className="biblio-zone__title">{t.campana.resumen.scenesTitle}</h4>
                    {scenes.length === 0 ? (
                      <p className="biblio-inspector__empty">{t.campana.resumen.noScenes}</p>
                    ) : (
                      <List>
                        {scenes.slice(0, 4).map((scene) => (
                          <ListRow
                            key={scene.id}
                            title={scene.name}
                            badge={scene.id === playingScene?.id
                              ? <Badge tone="neutral">{t.campana.sceneDetails.inProgress}</Badge>
                              : undefined}
                            subtitle={t.campana.mesa.enter(scene.name)}
                            meta={<span className="space__time">{relativeTime(scene.createdAt)}</span>}
                            thumb={<SceneThumb name={scene.name} sceneId={scene.id}
                              hasCover={scene.hasCover} />}
                            selected={selected?.kind === 'scene' && selected.id === scene.id}
                            onClick={() => { setSelected({ kind: 'scene', id: scene.id }); setTab('detalles') }}
                          />
                        ))}
                      </List>
                    )}
                  </section>

                  <section className="space__card">
                    <h4 className="biblio-zone__title">{t.campana.resumen.charactersTitle}</h4>
                    {actors.length === 0 ? (
                      <p className="biblio-inspector__empty">{t.campana.resumen.noCharacters}</p>
                    ) : (
                      <List>
                        {actors.slice(0, 4).map((actor) => (
                          <ListRow
                            key={actor.id}
                            title={actor.name}
                            badge={<Badge tone="neutral">{(t.campana.personajes.kinds as Record<string, string>)[actor.kind] ?? actor.kind}</Badge>}
                            subtitle={relativeTime(actor.createdAt)}
                            thumb={<span className="space__thumb" aria-hidden="true">
                              {actor.name.trim().charAt(0).toUpperCase()}
                            </span>}
                            selected={selected?.kind === 'actor' && selected.id === actor.id}
                            onClick={() => { setSelected({ kind: 'actor', id: actor.id }); setTab('detalles') }}
                          />
                        ))}
                      </List>
                    )}
                    <Button variant="ghost" onClick={onCreateCharacter}>
                      {t.campana.resumen.newCharacter}
                    </Button>
                  </section>
                </div>

                <section className="space__activity">
                  <h4 className="biblio-zone__title">{t.campana.activity}</h4>
                  {activity.length === 0 ? (
                    <p className="biblio-inspector__empty">{t.campana.resumen.noActivity}</p>
                  ) : (
                    <ul>
                      {activity.slice(0, 6).map((entry, index) => (
                        <li key={`${entry.campaignId}-${index}`}>
                          <strong>{relativeTime(entry.createdAt)}</strong>
                          <span>{entry.summary}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              </>
            ) : null}

            {section === 'mesa' ? (
              scenes.length === 0 ? (
                <EmptyState
                  icon={<Icon name="gamepad" />}
                  title={t.campana.mesa.empty}
                  text={t.campana.mesa.emptyText}
                  action={
                    <Button variant="primary" onClick={() => setSection('escenas')}>
                      {t.campana.mesa.createScene}
                    </Button>
                  }
                />
              ) : (
                <div className="space__scene-list">
                  <List>
                    {scenes.map((scene) => (
                      <ListRow
                        key={scene.id}
                        title={scene.name}
                        meta={<span>{plural(scene.tokenCount, 'token', 'tokens')}</span>}
                        actions={
                          <div className="space__scene-actions">
                            <Button variant="ghost" onClick={() => void openScene(scene)}>
                              {t.campana.mesa.enter(scene.name)}
                            </Button>
                            {canManage ? (
                              <Button
                                variant="ghost"
                                className="space__scene-delete"
                                onClick={(event) => {
                                  event.stopPropagation()
                                  setSceneToDelete(scene)
                                }}
                              >
                                {t.campana.mesa.deleteScene}
                              </Button>
                            ) : null}
                          </div>
                        }
                        onClick={() => void openScene(scene)}
                      />
                    ))}
                  </List>
                </div>
              )
            ) : null}

            {section === 'escenas' ? (
              <ScenesView
                campaign={campaign}
                embedded
                onBack={() => setSection('resumen')}
                onOpenScene={onOpenScene}
                onSelectScene={(scene) => {
                  setSelected({ kind: 'scene', id: scene.id })
                  setTab('detalles')
                }}
              />
            ) : null}

            {section === 'personajes' ? (
              <>
                <div className="space__actions">
                  <input
                    className="pv-input space__search"
                    placeholder={t.campana.personajes.searchPlaceholder}
                    aria-label={t.campana.personajes.searchLabel}
                    value={actorQuery}
                    onChange={(event) => setActorQuery(event.target.value)}
                  />
                  <Button variant="primary" onClick={onCreateCharacter}>
                    {t.campana.personajes.create}
                  </Button>
                </div>
                {actors.length === 0 ? (
                  <EmptyState
                    icon={<Icon name="woman-elf-face" />}
                    title={t.campana.personajes.empty}
                    text={t.campana.personajes.emptyText}
                  />
                ) : visibleActors.length === 0 ? (
                  <EmptyState
                    icon={<Icon name="woman-elf-face" />}
                    title={t.campana.personajes.noResults(actorQuery.trim())}
                  />
                ) : (
                  <List>
                    {visibleActors.map((actor) => {
                      const kindLabel =
                        (t.campana.personajes.kinds as Record<string, string>)[actor.kind] ?? actor.kind
                      const excerpt = (actor.personality || actor.biography || '').trim()
                      return (
                        <ListRow
                          key={actor.id}
                          title={actor.name}
                          subtitle={excerpt.length > 0
                            ? `${t.campana.personajes.meta(kindLabel, actor.level)} · ${excerpt.slice(0, 80)}`
                            : t.campana.personajes.meta(kindLabel, actor.level)}
                          selected={selected?.kind === 'actor' && selected.id === actor.id}
                          onClick={() => { setSelected({ kind: 'actor', id: actor.id }); setTab('detalles') }}
                        />
                      )
                    })}
                  </List>
                )}
              </>
            ) : null}

            {CATALOG_SECTIONS.includes(section as CatalogSection) ? (
              <ContentCatalogSection
                title={t.campana.catalog.emptyTitle[section as CatalogSection]}
                items={catalogEntries[section as CatalogSection]}
                emptyText={t.campana.catalog.emptyText[section as CatalogSection]}
                onSelect={(id) => { setSelected({ kind: 'content', id }); setTab('detalles') }}
              />
            ) : null}

            {section === 'participantes' ? (
              members.length === 0 ? (
                <EmptyState
                  icon={<Icon name="family-tree" />}
                  title={t.campana.participantes.empty}
                  text={t.campana.participantes.emptyText}
                />
              ) : (
                <List>
                  {members.map((member) => (
                    <ListRow
                      key={member.userId}
                      title={member.userName}
                      subtitle={t.campana.participantes.joinedAt(relativeTime(member.joinedAt))}
                      badge={<Badge tone={member.role === 'Owner' ? 'accent' : 'neutral'}>
                        {t.campanas.roles[member.role] ?? member.role}
                      </Badge>}
                    />
                  ))}
                </List>
              )
            ) : null}

            {section === 'configuracion' ? (
              <>
                <div className="space__config">
                  <div>
                    <span className="pv-field__label">{t.campana.configuracion.descriptionLabel}</span>
                    <p className="muted">{campaign.description ?? t.campana.configuracion.noDescription}</p>
                  </div>
                  <div>
                    <span className="pv-field__label">{t.campana.configuracion.coverLabel}</span>
                    <CampaignThumb name={campaign.name} campaignId={campaign.id}
                      hasCover={campaign.hasCover} size={64} />
                  </div>
                </div>
                <div className="space__actions">
                  <Button variant="primary" onClick={() => setManageOpen(true)}>
                    {t.campana.configuracion.openManage}
                  </Button>
                </div>
                <p className="muted">{t.campana.configuracion.manageHint}</p>
              </>
            ) : null}

            {loreSection ? (
              <EmptyState
                icon={<Icon name="calendar" />}
                title={worldSoon ? t.campana.reserved.title(label) : label}
                text={worldSoon
                  ? t.campana.reserved.worldText
                  : t.campana.loreHints[loreSection]}
                action={<Button variant="primary" onClick={onOpenLore}>{t.campana.openLore}</Button>}
              />
            ) : null}
          </div>

          <aside className="space__inspector" aria-label={t.campana.inspector}>
            <h3 className="space__inspector-title">{t.campana.inspector}</h3>
            {!selected ? (
              <p className="space__hint">{t.campana.inspectorHint}</p>
            ) : (
              <>
                <div className="space__tabs" role="tablist" aria-label={t.campana.inspector}>
                  {TABS.filter((item) => canManage || item.id !== 'notas').map((item, index) => (
                    <button
                      key={item.id}
                      ref={(element) => { tabRefs.current[index] = element }}
                      type="button"
                      role="tab"
                      id={`space-tab-${item.id}`}
                      aria-controls={`space-panel-${item.id}`}
                      aria-selected={tab === item.id}
                      tabIndex={tab === item.id ? 0 : -1}
                      onClick={() => setTab(item.id)}
                      onKeyDown={(event) => onTabKey(event, index)}
                    >
                      {item.label}
                    </button>
                  ))}
                </div>
                <div id={`space-panel-${tab}`} role="tabpanel"
                  aria-labelledby={`space-tab-${tab}`} className="space__inspector-body">
                  {tab === 'detalles' ? (
                    <>
                      <h4>{scene?.name ?? actor?.name ?? content?.name}</h4>
                      {scene ? (
                        <dl>
                          <dt>{t.campana.sceneDetails.state}</dt>
                          <dd>{t.campana.sceneDetails.inProgress}</dd>
                          <dt>{t.campana.sceneDetails.grid}</dt>
                          <dd>{scene.gridShape} · {scene.gridCellSize} px</dd>
                          <dt>{t.campana.sceneDetails.tokens}</dt>
                          <dd>{scene.tokenCount}</dd>
                        </dl>
                      ) : actor ? (
                        <dl>
                          <dt>{t.campana.actorDetails.kind}</dt>
                          <dd>{(t.campana.personajes.kinds as Record<string, string>)[actor.kind] ?? actor.kind}</dd>
                          <dt>{t.campana.actorDetails.level}</dt>
                          <dd>{actor.level}</dd>
                        </dl>
                      ) : content ? (
                        <dl>
                          <dt>{t.campana.catalog.details.type}</dt>
                          <dd>{catalogTypeLabel(content.typeKey)}</dd>
                          <dt>{t.campana.catalog.details.scope}</dt>
                          <dd>{catalogScopeLabel(content.scope)}</dd>
                          {content.summary ? (
                            <>
                              <dt>{t.campana.catalog.details.summary}</dt>
                              <dd>{content.summary}</dd>
                            </>
                          ) : null}
                        </dl>
                      ) : null}
                      <div className="space__inspector-actions">
                        {scene ? (
                          <>
                            <Button variant="primary" onClick={() => void openScene(scene)}>
                              {t.campana.mesa.enter(scene.name)}
                            </Button>
                            {canManage ? (
                              <Button
                                variant="ghost"
                                className="space__scene-delete"
                                onClick={() => setSceneToDelete(scene)}
                              >
                                {t.campana.mesa.deleteScene}
                              </Button>
                            ) : null}
                          </>
                        ) : null}
                        {actor?.build ? (
                          <Button variant="secondary" onClick={() => onOpenDraft(actor.id)}>
                            {t.campana.personajes.continueDraft}
                          </Button>
                        ) : null}
                        {actor && !actor.build && onOpenActor ? (
                          <Button variant="secondary" onClick={() => onOpenActor(actor.id)}>
                            {t.campana.actorDetails.openSheet}
                          </Button>
                        ) : null}
                      </div>
                    </>
                  ) : null}
                  {tab === 'notas' ? <NotesPanel campaignId={campaign.id} /> : null}
                  {tab === 'historial' ? (
                    activity.length === 0 ? (
                      <p className="space__hint">{t.campana.resumen.noActivity}</p>
                    ) : (
                      <ul className="space__timeline">
                        {activity.slice(0, 12).map((entry, index) => (
                          <li key={`${entry.campaignId}-${index}`}>
                            <strong>{relativeTime(entry.createdAt)}</strong>
                            <span>{entry.summary}</span>
                          </li>
                        ))}
                      </ul>
                    )
                  ) : null}
                </div>
              </>
            )}
          </aside>
        </div>
      </div>

      <ConfirmDialog
        open={sceneToDelete !== null}
        title={t.campana.mesa.deleteSceneConfirmTitle}
        text={sceneToDelete ? t.campana.mesa.deleteSceneConfirmText(sceneToDelete.name) : undefined}
        confirmLabel={t.campana.mesa.deleteScene}
        cancelLabel={t.campana.mesa.deleteSceneCancel}
        destructive
        onConfirm={() => {
          const scene = sceneToDelete
          setSceneToDelete(null)
          if (scene) void deleteScene(scene)
        }}
        onCancel={() => setSceneToDelete(null)}
      />

      {manageOpen ? (
        <CampaignManageDialog
          campaign={campaign}
          open
          labels={{
            title: t.campanas.manage.title(campaign.name),
            close: t.campanas.manage.close,
          }}
          onClose={() => setManageOpen(false)}
          onChanged={() => {
            void load()
            onChanged()
          }}
        />
      ) : null}
    </main>
  )
}
