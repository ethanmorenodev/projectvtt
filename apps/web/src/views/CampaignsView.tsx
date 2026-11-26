import { useEffect, useMemo, useState } from 'react'
import { apiFetch } from '../api/client'
import type { CampaignDetail, CampaignSummary } from '../api/types'
import {
  Badge,
  Button,
  Dialog,
  EmptyState,
  Field,
  FilterChip,
  Icon,
  Input,
  useToast,
} from '../ui'
import { t } from '../i18n'
import { useCampaignCover } from '../api/covers'
import { CampaignCoverActions } from '../ui/CampaignCoverActions'
import { CampaignManageDialog } from './CampaignManageDialog'
import { plural, relativeTime } from './presentation'
import './campaigns.css'

/**
 * Campañas (PMB-2.4): lista densa con miniaturas, descripción, estado,
 * participantes y última actividad (layout aprobado en
 * docs/visual/screens/pmb-2.3-campanas.md), búsqueda y filtros reales, y
 * gestión de campaña con las siete capacidades de API (auditoría §2.3).
 * Cadenas en el catálogo i18n (ADR-038).
 */

interface Props {
  onOpen: (campaign: CampaignDetail) => void
}

type Filter = 'todas' | 'propias' | 'compartidas' | 'archivadas'

const FILTERS: Array<{ id: Filter; label: string }> = [
  { id: 'todas', label: t.campanas.filters.todas },
  { id: 'propias', label: t.campanas.filters.propias },
  { id: 'compartidas', label: t.campanas.filters.compartidas },
  { id: 'archivadas', label: t.campanas.filters.archivadas },
]

const CARD_ART = [
  'icon_dragon.png', 'icon_potionred.png', 'icon_aurared.png',
  'icon_swordblood.png', 'icon_bookholy.png', 'icon_crystalblue.png',
] as const

function CampaignArtCard({ campaign, onOpen, onManage, onCoversChanged }: {
  campaign: CampaignSummary
  onOpen: () => void
  onManage: () => void
  onCoversChanged: () => void
}) {
  const cover = useCampaignCover(campaign.id, campaign.hasCover)
  const seed = [...campaign.id].reduce((sum, char) => sum + char.charCodeAt(0), 0)
  const art = CARD_ART[seed % CARD_ART.length]

  return (
    <article className="campaign-card">
      <button type="button" className="campaign-card__main" onClick={onOpen}>
        <span className="campaign-card__cover" aria-hidden="true">
          <img src={cover ?? `/art/fantasy/${art}`} alt="" />
        </span>
        <span className="campaign-card__body">
          <span className="campaign-card__badges">
            <Badge tone={campaign.archivedAt ? 'neutral' : 'accent'}>
              {campaign.archivedAt ? t.campanas.stateArchived : t.campanas.stateActive}
            </Badge>
            <span className="campaign-card__role">
              {t.campanas.roles[campaign.role] ?? campaign.role}
            </span>
          </span>
          <strong>{campaign.name}</strong>
          <span className="campaign-card__desc">
            {campaign.description ?? t.campanas.subtitle}
          </span>
          <span className="campaign-card__meta">
            <span>{plural(campaign.memberCount, 'participante', 'participantes')}</span>
            <span>{relativeTime(campaign.lastActivityAt) ||
              t.inicio.created(relativeTime(campaign.createdAt))}</span>
          </span>
        </span>
      </button>
      <CampaignCoverActions campaign={campaign} onChanged={onCoversChanged} />
      <button type="button" className="campaign-card__manage"
        aria-label={t.campanas.manageAction(campaign.name)} onClick={onManage}>
        <Icon name="cog" size={16} />
      </button>
    </article>
  )
}

export function CampaignsView({ onOpen }: Props) {
  const toast = useToast()
  const [campaigns, setCampaigns] = useState<CampaignSummary[] | null>(null)
  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState<Filter>('todas')
  const [error, setError] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [joinOpen, setJoinOpen] = useState(false)
  const [manage, setManage] = useState<CampaignSummary | null>(null)
  const [newName, setNewName] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [inviteCode, setInviteCode] = useState('')
  const [busy, setBusy] = useState(false)

  async function load() {
    try {
      setCampaigns(await apiFetch<CampaignSummary[]>('/api/v1/campaigns'))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.loadError)
      setCampaigns(null)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return (campaigns ?? []).filter((campaign) => {
      if (filter === 'propias' && campaign.role !== 'Owner') {
        return false
      }
      if (filter === 'compartidas' && (campaign.role === 'Owner' || campaign.archivedAt)) {
        return false
      }
      if (filter === 'archivadas' && !campaign.archivedAt) {
        return false
      }
      if (filter !== 'archivadas' && filter !== 'todas' && campaign.archivedAt) {
        return false
      }
      return (
        needle.length === 0 ||
        campaign.name.toLowerCase().includes(needle) ||
        (campaign.description ?? '').toLowerCase().includes(needle)
      )
    })
  }, [campaigns, query, filter])

  async function create() {
    setBusy(true)
    setError(null)
    try {
      const created = await apiFetch<CampaignDetail>('/api/v1/campaigns', {
        method: 'POST',
        body: { name: newName, description: newDescription || undefined },
      })
      setCreateOpen(false)
      setNewName('')
      setNewDescription('')
      onOpen(created)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.createDialog.error)
    } finally {
      setBusy(false)
    }
  }

  async function join() {
    setBusy(true)
    setError(null)
    try {
      await apiFetch(`/api/v1/invites/${inviteCode.trim()}/redeem`, { method: 'POST' })
      setJoinOpen(false)
      setInviteCode('')
      toast.show({ title: t.inicio.joinedToast, tone: 'success' })
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.joinDialog.error)
    } finally {
      setBusy(false)
    }
  }

  async function open(campaign: CampaignSummary) {
    try {
      onOpen(await apiFetch<CampaignDetail>(`/api/v1/campaigns/${campaign.id}`))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.openError)
    }
  }

  return (
    <main className="f2-area campaigns-view">
      <header className="campaigns-hero">
        <div className="campaigns-hero__art" aria-hidden="true" />
        <div className="campaigns-hero__content">
          <span className="campaigns-hero__eyebrow">{t.campanas.heroEyebrow}</span>
          <h1 className="f2-page-title">{t.campanas.title}</h1>
          <p className="f2-page-subtitle">{t.campanas.subtitle}</p>
        </div>
        <div className="f2-page-actions">
          <Button variant="secondary" onClick={() => setJoinOpen(true)}>{t.campanas.joinWithCode}</Button>
          <Button variant="primary" onClick={() => setCreateOpen(true)}>{t.campanas.createCampaign}</Button>
        </div>
      </header>

      <div className="f2-toolbar">
        <input
          className="pv-input f2-search"
          placeholder={t.campanas.searchPlaceholder}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          aria-label={t.campanas.searchLabel}
        />
        {FILTERS.map((entry) => (
          <FilterChip
            key={entry.id}
            label={entry.label}
            selected={filter === entry.id}
            onClick={() => setFilter(entry.id)}
          />
        ))}
      </div>

      {error ? (
        <div className="pv-list-error" role="alert">
          <p>{error}</p>
          <Button variant="secondary" onClick={() => { setError(null); void load() }}>
            {t.common.retry}
          </Button>
        </div>
      ) : campaigns === null ? (
        <p className="muted">{t.campanas.loading}</p>
      ) : campaigns.length === 0 ? (
        <EmptyState
          icon={<Icon name="house" />}
          title={t.campanas.noCampaigns}
          text={t.campanas.noCampaignsText}
          action={
            <Button variant="primary" onClick={() => setCreateOpen(true)}>
              {t.campanas.createCampaign}
            </Button>
          }
        />
      ) : visible.length === 0 ? (
        <EmptyState
          title={query.trim() ? t.campanas.noResults(query.trim()) : t.campanas.noResultsInFilter}
          text={t.campanas.noResultsText}
          action={
            query.trim() ? (
              <Button variant="secondary" onClick={() => setQuery('')}>{t.campanas.clearSearch}</Button>
            ) : undefined
          }
        />
      ) : (
        <div className="campaigns-grid">
          {visible.map((campaign) => (
            <CampaignArtCard
              key={campaign.id}
              campaign={campaign}
              onOpen={() => void open(campaign)}
              onManage={() => setManage(campaign)}
              onCoversChanged={() => void load()}
            />
          ))}
        </div>
      )}

      <Dialog
        open={createOpen}
        title={t.campanas.createDialog.title}
        closeLabel={t.campanas.createDialog.close}
        onClose={() => setCreateOpen(false)}
        actions={
          <>
            <Button variant="ghost" onClick={() => setCreateOpen(false)}>{t.campanas.createDialog.cancel}</Button>
            <Button variant="primary" disabled={busy} onClick={() => void create()}>{t.campanas.createDialog.confirm}</Button>
          </>
        }
      >
        <Field label={t.campanas.createDialog.nameLabel}>
          {({ id, ...aria }) => (
            <Input id={id} value={newName} maxLength={100} autoFocus
              onChange={(event) => setNewName(event.target.value)} {...aria} />
          )}
        </Field>
        <Field label={t.campanas.createDialog.descriptionLabel} helper={t.campanas.createDialog.descriptionHelper}>
          {({ id, ...aria }) => (
            <Input id={id} value={newDescription} maxLength={500}
              onChange={(event) => setNewDescription(event.target.value)} {...aria} />
          )}
        </Field>
      </Dialog>

      <Dialog
        open={joinOpen}
        title={t.campanas.joinDialog.title}
        closeLabel={t.campanas.joinDialog.close}
        onClose={() => setJoinOpen(false)}
        actions={
          <>
            <Button variant="ghost" onClick={() => setJoinOpen(false)}>{t.campanas.joinDialog.cancel}</Button>
            <Button variant="primary" disabled={busy} onClick={() => void join()}>{t.campanas.joinDialog.confirm}</Button>
          </>
        }
      >
        <Field label={t.campanas.joinDialog.codeLabel}>
          {({ id, ...aria }) => (
            <Input id={id} value={inviteCode} maxLength={64} autoFocus
              placeholder={t.campanas.joinDialog.codePlaceholder}
              onChange={(event) => setInviteCode(event.target.value)} {...aria} />
          )}
        </Field>
      </Dialog>

      {manage ? (
        <CampaignManageDialog
          campaign={manage}
          open
          labels={{ title: t.campanas.manage.title(manage.name), close: t.campanas.manage.close }}
          onClose={() => setManage(null)}
          onChanged={() => void load()}
        />
      ) : null}
    </main>
  )
}
