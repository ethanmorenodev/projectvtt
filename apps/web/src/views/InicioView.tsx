import { useCallback, useEffect, useRef, useState } from 'react'
import { apiFetch } from '../api/client'
import { useCampaignCover } from '../api/covers'
import type {
  ActivityEntry, ActorDto, CampaignDetail, CampaignSummary, ContentItemDto,
} from '../api/types'
import { Button, EmptyState, List, ListRow, useToast } from '../ui'
import { portraitForName } from '../ui/contentIcons'
import { useImageUrl } from '../api/portraits'
import { Icon } from '../ui/Icon'
import { CampaignCoverActions } from '../ui/CampaignCoverActions'
import { t } from '../i18n'
import { plural, relativeTime } from './presentation'
import './inicioEditorial.css'

/**
 * Inicio editorial (D1, EXP-7): hero «Destacado» con collage CC0 y carrusel
 * «Continuar jugando» con la portada real de cada campaña (o degradado con
 * ilustración de respaldo). Datos reales del servidor: campañas, actividad,
 * contenido y unión por código. La lógica de permisos y de portadas sigue en
 * el servidor; aquí solo se compone la presentación.
 */

interface Props {
  onOpenCampaign: (campaign: CampaignDetail) => void
  onOpenAll: () => void
  onOpenLibrary: () => void
}

const COLLAGE = [
  { file: 'icon_dragon.png', x: 6, y: -8, size: 250, rotate: -12, blur: 1, opacity: 0.55 },
  { file: 'icon_fireball.png', x: 72, y: -14, size: 230, rotate: 10, blur: 2, opacity: 0.5 },
  { file: 'icon_magicsword.png', x: 40, y: 52, size: 210, rotate: -7, blur: 1, opacity: 0.42 },
  { file: 'icon_helmetgold.png', x: 84, y: 48, size: 170, rotate: 14, blur: 2, opacity: 0.5 },
  { file: 'icon_bookholy.png', x: 18, y: 58, size: 190, rotate: 8, blur: 2, opacity: 0.4 },
  { file: 'icon_crystalblue.png', x: 58, y: -20, size: 160, rotate: -5, blur: 3, opacity: 0.45 },
] as const

const FALLBACK_ART = [
  { art: 'icon_dragon.png', tone: 'purple' },
  { art: 'icon_potionred.png', tone: 'red' },
  { art: 'icon_aurared.png', tone: 'blue' },
  { art: 'icon_swordblood.png', tone: 'gold' },
] as const

function PartyPortrait({ actor }: { actor: ActorDto }) {
  const url = useImageUrl(actor.portraitId)
  return (
    <div className="ie-party__portrait">
      <img src={url ?? portraitForName(actor.name)} alt="" aria-hidden="true" />
    </div>
  )
}

function CampaignCard({ campaign, onOpen, onReload }: {
  campaign: CampaignSummary
  onOpen: (campaign: CampaignSummary) => void
  onReload: () => void
}) {
  const cover = useCampaignCover(campaign.id, campaign.hasCover)
  const seed = [...campaign.id].reduce((sum, char) => sum + char.charCodeAt(0), 0)
  const fallback = FALLBACK_ART[seed % FALLBACK_ART.length]

  return (
    <div className={`ie-campaign ie-campaign--${fallback.tone}`}>
      <button type="button" className="ie-campaign__main" onClick={() => onOpen(campaign)}>
        <span className="ie-campaign__art" aria-hidden="true">
          <img src={cover ?? `/art/fantasy/${fallback.art}`} alt="" />
        </span>
        <span className="ie-campaign__body">
          <span className="ie-campaign__eyebrow">
            {t.campanas.roles[campaign.role] ?? campaign.role} ·{' '}
            {plural(campaign.memberCount, 'participante', 'participantes')}
          </span>
          <strong>{campaign.name}</strong>
          <span className="ie-campaign__cta">{t.inicio.continueActionShort} →</span>
        </span>
      </button>
      <CampaignCoverActions campaign={campaign} onChanged={onReload} />
    </div>
  )
}

export function InicioView({ onOpenCampaign, onOpenAll, onOpenLibrary }: Props) {
  const toast = useToast()
  const carouselRef = useRef<HTMLDivElement>(null)
  const [campaigns, setCampaigns] = useState<CampaignSummary[]>([])
  const [activity, setActivity] = useState<ActivityEntry[]>([])
  const [contentCount, setContentCount] = useState<number | null>(null)
  const [party, setParty] = useState<ActorDto[]>([])
  const [inviteCode, setInviteCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    const [campaignsResult, activityResult, contentResult] = await Promise.allSettled([
      apiFetch<CampaignSummary[]>('/api/v1/campaigns'),
      apiFetch<ActivityEntry[]>('/api/v1/activity?limit=8'),
      apiFetch<ContentItemDto[]>('/api/v1/content/items'),
    ])
    if (campaignsResult.status === 'fulfilled') {
      setCampaigns(campaignsResult.value)
    } else {
      setError(
        campaignsResult.reason instanceof Error
          ? campaignsResult.reason.message
          : t.inicio.loadError,
      )
      setCampaigns([])
    }
    setActivity(activityResult.status === 'fulfilled' ? activityResult.value : [])
    setContentCount(contentResult.status === 'fulfilled' ? contentResult.value.length : null)
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  // Retratos del grupo de la campaña más reciente: arte del contenido del
  // usuario, no retratos fijos. Un fallo no interrumpe el Inicio.
  useEffect(() => {
    // La campaña con actividad más reciente es la que el usuario tiene en
    // mente; no la primera que devuelva la API.
    const first = [...campaigns]
      .filter((campaign) => !campaign.archivedAt)
      .sort((a, b) =>
        new Date(b.lastActivityAt ?? b.createdAt).getTime() -
        new Date(a.lastActivityAt ?? a.createdAt).getTime())[0]
    if (!first) {
      setParty([])
      return
    }
    let cancelled = false
    void apiFetch<ActorDto[]>(`/api/v1/campaigns/${first.id}/actors`)
      .then((actors) => {
        if (!cancelled) setParty(Array.isArray(actors) ? actors.slice(0, 6) : [])
      })
      .catch(() => { if (!cancelled) setParty([]) })
    return () => { cancelled = true }
  }, [campaigns])

  const recientes = campaigns
    .filter((campaign) => !campaign.archivedAt)
    .sort((a, b) =>
      new Date(b.lastActivityAt ?? b.createdAt).getTime() -
      new Date(a.lastActivityAt ?? a.createdAt).getTime())

  async function open(campaign: CampaignSummary) {
    try {
      onOpenCampaign(await apiFetch<CampaignDetail>(`/api/v1/campaigns/${campaign.id}`))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.inicio.openError)
    }
  }

  async function join() {
    setBusy(true)
    setError(null)
    try {
      await apiFetch(`/api/v1/invites/${inviteCode.trim()}/redeem`, { method: 'POST' })
      setInviteCode('')
      toast.show({ title: t.inicio.joinedToast, tone: 'success' })
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.inicio.joinError)
    } finally {
      setBusy(false)
    }
  }

  const scroll = (direction: -1 | 1) => {
    carouselRef.current?.scrollBy({ left: direction * 260, behavior: 'smooth' })
  }

  return (
    <main className="inicio-editorial">
      <div className="ie-label">{t.inicio.featured}</div>
      <section className="ie-hero" aria-label={t.inicio.featured}>
        <div className="ie-hero__art" aria-hidden="true">
          {COLLAGE.map((art) => (
            <img key={art.file} src={`/art/fantasy/${art.file}`} alt=""
              style={{
                left: `${art.x}%`, top: `${art.y}%`,
                width: art.size, height: art.size,
                transform: `rotate(${art.rotate}deg)`,
                filter: `blur(${art.blur}px)`, opacity: art.opacity,
              }}
            />
          ))}
          <div className="ie-hero__vignette" />
        </div>
        <div className="ie-hero__content">
          <span className="ie-hero__kicker">{t.inicio.heroKicker}</span>
          <h1>{t.inicio.heroTitle}</h1>
          <div className="ie-hero__rule" />
          <p>{contentCount !== null ? t.inicio.heroText(contentCount) : t.inicio.subtitle}</p>
          <button type="button" className="ie-hero__cta" onClick={onOpenLibrary}>
            {t.inicio.exploreLibrary}
          </button>
        </div>
      </section>

      {error ? <p className="error">{error}</p> : null}

      <div className="ie-row-head">
        <h2>{t.inicio.continueBlock}</h2>
        <div className="ie-arrows">
          <button type="button" aria-label={t.inicio.previous} onClick={() => scroll(-1)}>‹</button>
          <button type="button" aria-label={t.inicio.next} onClick={() => scroll(1)}>›</button>
          <Button variant="ghost" onClick={onOpenAll}>{t.inicio.seeAll}</Button>
        </div>
      </div>

      {campaigns.length === 0 ? (
        <EmptyState
          icon={<Icon name="house" />}
          title={t.inicio.noCampaigns}
          text={t.inicio.noCampaignsText}
          action={<Button variant="primary" onClick={onOpenAll}>{t.inicio.createCampaign}</Button>}
        />
      ) : (
        <div className="ie-cards" ref={carouselRef}>
          {recientes.map((campaign) => (
            <CampaignCard key={campaign.id} campaign={campaign} onOpen={(next) => void open(next)}
              onReload={() => void load()}
            />
          ))}
        </div>
      )}

      {party.length > 0 ? (
        <section className="ie-party" aria-label={t.inicio.party}>
          <h2>{t.inicio.party}</h2>
          <div className="ie-party__row">
            {party.map((actor) => (
              <div className="ie-party__member" key={actor.id}>
                <PartyPortrait actor={actor} />
                <strong>{actor.name}</strong>
                <small>{t.inicio.levelShort(actor.level)}</small>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      <div className="ie-columns">
        <section className="ie-block">
          <h2>{t.inicio.activityBlock}</h2>
          {activity.length === 0 ? (
            <EmptyState
              icon={<Icon name="calendar" />}
              title={t.inicio.noActivity}
              text={t.inicio.noActivityText}
            />
          ) : (
            <List>
              {activity.map((entry, index) => (
                <ListRow
                  key={`${entry.campaignId}-${index}`}
                  title={entry.summary}
                  subtitle={`${entry.campaignName} · ${entry.userName}`}
                  meta={<span className="pv-row__meta-time">{relativeTime(entry.createdAt)}</span>}
                />
              ))}
            </List>
          )}
        </section>

        <section className="ie-block ie-block--side">
          <h2>{t.inicio.invitesBlock}</h2>
          <div className="ie-invite">
            <input
              className="pv-input"
              placeholder={t.inicio.invitePlaceholder}
              value={inviteCode}
              maxLength={64}
              aria-label={t.inicio.invitePlaceholder}
              onChange={(event) => setInviteCode(event.target.value)}
            />
            <Button variant="primary" disabled={busy || inviteCode.trim().length === 0}
              onClick={() => void join()}>
              {t.inicio.join}
            </Button>
          </div>
          <p className="muted">{t.inicio.inviteHint}</p>
        </section>
      </div>
    </main>
  )
}
