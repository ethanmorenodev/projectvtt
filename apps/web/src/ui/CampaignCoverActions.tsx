import { useState, type ChangeEvent } from 'react'
import { createPortal } from 'react-dom'
import { useToast } from '../ui'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import type { CampaignSummary } from '../api/types'
import { deleteCover, uploadCover } from '../api/covers'
import { t } from '../i18n'
import './campaignCoverActions.css'

/**
 * Controles de portada de campaña, hermanos del botón de apertura de la
 * tarjeta (nunca anidados en él). La visibilidad refleja la regla del
 * servidor: solo el propietario puede cambiar o quitar la portada.
 */
export function CampaignCoverActions({ campaign, onChanged }: {
  campaign: Pick<CampaignSummary, 'id' | 'name' | 'role' | 'hasCover'>
  onChanged: () => void
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const toast = useToast()

  if (campaign.role !== 'Owner') {
    return null
  }

  async function changeCover(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) {
      return
    }
    setBusy(true)
    setError(null)
    try {
      await uploadCover(campaign.id, file)
      toast.show({ title: t.campanas.manage.coverUpdatedToast, tone: 'success' })
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.coverError)
    } finally {
      setBusy(false)
    }
  }

  async function removeCover() {
    setBusy(true)
    setError(null)
    try {
      await deleteCover(campaign.id)
      toast.show({ title: t.campanas.manage.coverRemovedToast, tone: 'success' })
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.coverRemoveError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <span className="campaign-cover-actions" aria-busy={busy}>
        <label className="campaign-cover-actions__change" title={t.campanas.coverChange}>
          <span aria-hidden="true">{busy ? '…' : '✎'}</span>
          <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml"
            aria-label={`${t.campanas.coverChange}: ${campaign.name}`}
            disabled={busy}
            onChange={(event) => void changeCover(event)} />
        </label>
        {campaign.hasCover ? (
          <button type="button" className="campaign-cover-actions__remove"
            title={t.campanas.coverRemove}
            aria-label={`${t.campanas.coverRemove}: ${campaign.name}`}
            disabled={busy}
            onClick={() => setConfirmOpen(true)}>×</button>
        ) : null}
        {error ? (
          <span className="campaign-cover-actions__error" role="alert">{error}</span>
        ) : null}
      </span>
      {createPortal(
        <ConfirmDialog
          open={confirmOpen}
          title={t.campanas.coverRemoveConfirm.title}
          text={t.campanas.coverRemoveConfirm.text}
          confirmLabel={t.campanas.coverRemoveConfirm.confirm}
          cancelLabel={t.campanas.coverRemoveConfirm.cancel}
          destructive
          onConfirm={() => {
            setConfirmOpen(false)
            void removeCover()
          }}
          onCancel={() => setConfirmOpen(false)}
        />,
        document.body,
      )}
    </>
  )
}
