import { useEffect, useMemo, useState } from 'react'
import { apiFetch, apiFetchBlob } from '../api/client'
import { contentIcon } from '../ui/contentIcons'
import { t } from '../i18n'
import type {
  ActorDto,
  ActorInventoryDto,
  EquipmentSlotDto,
} from '../api/types'
import { useLocation } from '../routing/router'
import '../views/inventory.css'

/**
 * Inventario visual real (PMB-6.4): silueta con huecos del blueprint del
 * sistema (ADR-041), mochila e inspector. El servidor valida hueco, tipo y
 * unicidad; esta vista solo presenta y pide operaciones.
 */
export function InventoryView() {
  const { route } = useLocation()
  const actorId = route.kind === 'inventario' ? route.actorId : null
  const [actor, setActor] = useState<ActorDto | null>(null)
  const [portraitUrl, setPortraitUrl] = useState<string | null>(null)
  const portraitCrop = useMemo(() => {
    const build = actor?.build as Record<string, unknown> | null | undefined
    const value = build?.portraitCrop as { scale?: number; x?: number; y?: number } | undefined
    return {
      objectPosition: `${value?.x ?? 50}% ${value?.y ?? 50}%`,
      transform: `scale(${value?.scale ?? 1})`,
    }
  }, [actor?.build])
  const [comparing, setComparing] = useState(false)
  const [slots, setSlots] = useState<EquipmentSlotDto[]>([])
  const [inventory, setInventory] = useState<ActorInventoryDto | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [targetSlot, setTargetSlot] = useState('')
  const [notice, setNotice] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [transferActors, setTransferActors] = useState<ActorDto[]>([])
  const [transferToId, setTransferToId] = useState('')
  const [transferQuantity, setTransferQuantity] = useState('')

  const reload = async () => {
    if (!actorId) return
    try {
      const next = await apiFetch<ActorInventoryDto>(`/api/v1/actors/${actorId}/items`)
      setInventory(next)
      setError(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.inventario.error)
    }
  }

  useEffect(() => {
    if (!actorId) return
    void apiFetch<ActorDto>(`/api/v1/actors/${actorId}`).then(setActor).catch(() => undefined)
    void apiFetch<EquipmentSlotDto[]>('/api/v1/equipment-slots/dnd5e')
      .then((rows) => {
        setSlots(rows)
        if (rows.length > 0) setTargetSlot(rows[0].id)
      })
      .catch(() => setSlots([]))
    void reload()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actorId])

  useEffect(() => {
    if (!actor?.campaignId) return
    let cancelled = false
    void apiFetch<ActorDto[]>(`/api/v1/campaigns/${actor.campaignId}/actors`)
      .then((rows) => {
        if (cancelled) return
        const candidates = Array.isArray(rows)
          ? rows.filter((candidate) => candidate.id !== actor.id)
          : []
        setTransferActors(candidates)
      })
      .catch(() => { if (!cancelled) setTransferActors([]) })
    return () => { cancelled = true }
  }, [actor?.campaignId, actor?.id])

  useEffect(() => {
    let cancelled = false
    let objectUrl: string | null = null
    if (!actor?.portraitId) {
      setPortraitUrl(null)
      return
    }
    void apiFetchBlob(`/api/v1/images/${actor.portraitId}/file`)
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
  }, [actor?.portraitId])

  const items = inventory?.items ?? []
  const selected = items.find((item) => item.id === selectedId) ?? null
  const inSlot = (slotId: string) => items.find((item) => item.slotId === slotId) ?? null
  const backpack = useMemo(() => items.filter((item) => item.slotId === null), [items])

  const equip = async () => {
    if (!selected || busy) return
    if (inSlot(targetSlot)) {
      setNotice(t.inventario.occupied)
      return
    }
    setBusy(true)
    try {
      const next = await apiFetch<ActorInventoryDto>(
        `/api/v1/actors/${selected.actorId}/items/${selected.id}/equip`,
        { method: 'POST', body: { version: selected.version, slotId: targetSlot } })
      setInventory(next)
      const label = slots.find((slot) => slot.id === targetSlot)?.label ?? targetSlot
      setNotice(t.inventario.equipped(selected.name, label))
      setError(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.inventario.error)
    } finally {
      setBusy(false)
    }
  }

  const unequip = async () => {
    if (!selected || busy) return
    setBusy(true)
    try {
      const next = await apiFetch<ActorInventoryDto>(
        `/api/v1/actors/${selected.actorId}/items/${selected.id}/equip`,
        { method: 'DELETE' })
      setInventory(next)
      setNotice(t.inventario.unequipped(selected.name))
      setError(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.inventario.error)
    } finally {
      setBusy(false)
    }
  }

  const transfer = async () => {
    if (!selected || !transferToId || busy) return
    const rawQuantity = transferQuantity.trim()
    let quantity: number | undefined
    if (rawQuantity !== '') {
      quantity = Number(rawQuantity)
      if (!Number.isInteger(quantity) || quantity < 1) {
        setError(t.inventario.transferInvalidQuantity)
        return
      }
    }
    const targetName = transferActors.find((candidate) => candidate.id === transferToId)?.name
      ?? transferToId
    setBusy(true)
    try {
      const next = await apiFetch<ActorInventoryDto>(
        `/api/v1/actors/${selected.actorId}/items/${selected.id}/transfer`,
        {
          method: 'POST',
          body: {
            targetActorId: transferToId,
            ...(quantity === undefined ? {} : { quantity }),
            version: selected.version,
          },
        })
      setInventory(next)
      setNotice(t.inventario.transferDone(selected.name, targetName, quantity ?? selected.quantity))
      setError(null)
      setTransferQuantity('')
      if (!next.items.some((entry) => entry.id === selected.id)) setSelectedId(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.inventario.transferError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="inv">
      <header className="inv__head">
        <div>
          <h1 className="biblio__title">
            {t.inventario.title}{actor ? ` · ${actor.name}` : ''}
          </h1>
          <p className="biblio__subtitle">{t.inventario.subtitle}</p>
        </div>
      </header>
      {error && <p className="biblio-status biblio-status--error" role="alert">{error}</p>}
      {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}

      <div className="inv__body">
        <section className="inv__backpack" aria-label={t.inventario.backpack(backpack.length)}>
          <h2 className="biblio-zone__title">{t.inventario.backpack(backpack.length)}</h2>
          {backpack.length === 0 ? (
            <p className="biblio-inspector__empty">{t.inventario.backpackEmpty}</p>
          ) : (
            <ul className="inv__grid">
              {backpack.map((item) => (
                <li key={item.id}>
                  <button type="button"
                    className={`inv-card ${selectedId === item.id ? 'is-selected' : ''}`}
                    onClick={() => setSelectedId(item.id)}>
                    <span className="inv-card__thumb" aria-hidden="true">
                      <img src={contentIcon(undefined, item.name) ?? '/icons/chest.svg'} alt="" />
                    </span>
                    <span className="inv-card__name">{item.name}</span>
                    <span className="inv-card__meta">
                      {item.weight !== null ? `${item.weight} lb` : '—'}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="inv__silhouette" aria-label={t.inventario.silhouette}>
          <div className="inv-silhouette">
            <span className="inv-silhouette__figure" aria-hidden="true">
              {portraitUrl
                ? <img src={portraitUrl} alt="" className="inv-silhouette__portrait"
                    style={portraitCrop} />
                : <img src="/icons/rogue.svg" alt="" />}
            </span>
            {slots.map((slot, index) => {
              const item = inSlot(slot.id)
              return (
                <button key={slot.id} type="button"
                  className={`inv-slot inv-slot--${index}`}
                  aria-label={`${slot.label}${item ? `: ${item.name}` : ''}`}
                  onClick={() => item && setSelectedId(item.id)}>
                  {item ? (
                    <>
                      <span className="inv-slot__name">{item.name}</span>
                    </>
                  ) : (
                    <span className="inv-slot__empty">{slot.label}</span>
                  )}
                </button>
              )
            })}
          </div>
          <div className="inv-effects">
            <span className="creador__label">{t.inventario.effects}</span>
            <div className="inv-effects__row">
              <span>{t.inventario.slot}</span>
              <strong>{items.filter((item) => item.slotId !== null).length} / {slots.length}</strong>
            </div>
            <div className="inv-effects__row">
              <span>{t.inventario.weight}</span>
              <strong>
                {items.filter((item) => item.slotId !== null)
                  .reduce((total, item) => total + (item.weight ?? 0), 0)} lb
              </strong>
            </div>
            <p className="creador__notice">{t.inventario.portraitNote}</p>
          </div>
        </section>

        <aside className="img-biblio__inspector" aria-label={t.inventario.detail}>
          <h2 className="biblio-zone__title">{t.inventario.detail}</h2>
          {!selected ? (
            <p className="biblio-inspector__empty">{t.inventario.select}</p>
          ) : (
            <div className="biblio-inspector__body">
              <h3 className="biblio-inspector__name">{selected.name}</h3>
              <p className="biblio-inspector__summary">
                {t.inventario.weight}: {selected.weight !== null ? `${selected.weight} lb` : '—'}
              </p>
              <h4 className="biblio-inspector__section">{t.inventario.equip}</h4>
              <div className="inv-actions">
                <select aria-label={t.inventario.slot} value={targetSlot}
                  onChange={(event) => setTargetSlot(event.target.value)}>
                  {slots.map((slot) => (
                    <option key={slot.id} value={slot.id}>{slot.label}</option>
                  ))}
                </select>
                <button type="button" disabled={busy} onClick={() => void equip()}>
                  {t.inventario.equip}
                </button>
                {selected.slotId && (
                  <button type="button" disabled={busy} onClick={() => void unequip()}>
                    {t.inventario.unequip}
                  </button>
                )}
                <button type="button" onClick={() => setComparing((current) => !current)}>
                  {t.inventario.compare}
                </button>
              </div>
              <h4 className="biblio-inspector__section">{t.inventario.transfer}</h4>
              <div className="inv-actions">
                <select aria-label={t.inventario.transferTo} value={transferToId}
                  disabled={busy || transferActors.length === 0}
                  onChange={(event) => setTransferToId(event.target.value)}>
                  <option value="">{t.inventario.transferSelectTarget}</option>
                  {transferActors.map((candidate) => (
                    <option key={candidate.id} value={candidate.id}>{candidate.name}</option>
                  ))}
                </select>
                <input type="number" aria-label={t.inventario.transferQuantity}
                  placeholder={t.inventario.transferQuantityHint}
                  min={1} max={selected.quantity} step={1}
                  value={transferQuantity} disabled={busy}
                  onChange={(event) => setTransferQuantity(event.target.value)} />
                <button type="button" disabled={busy || !transferToId}
                  onClick={() => void transfer()}>
                  {t.inventario.transferAction}
                </button>
              </div>
              {transferActors.length === 0 && (
                <p className="creador__notice">{t.inventario.transferEmpty}</p>
              )}
              {comparing ? (
                <div className="inv-compare">
                  <span className="creador__label">{t.inventario.compare}</span>
                  <div>
                    <span>{t.inventario.compareCurrent}</span>
                    <strong>{inSlot(targetSlot)?.name ?? '—'}</strong>
                  </div>
                  <div>
                    <span>{t.inventario.compareSelected}</span>
                    <strong>{selected.name}</strong>
                  </div>
                  <p className="creador__notice">
                    {t.inventario.weight}: {inSlot(targetSlot)?.weight ?? '—'} lb → {selected.weight ?? '—'} lb
                  </p>
                </div>
              ) : null}
            </div>
          )}
        </aside>
      </div>
    </div>
  )
}
