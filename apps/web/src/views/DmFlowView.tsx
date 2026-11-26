import { useEffect, useState } from 'react'
import { apiFetch } from '../api/client'
import { t } from '../i18n'
import type { ContentGrantDto, ContentProposalDto } from '../api/types'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Button } from '../ui'
import { useNavigate } from '../routing/router'
import '../views/dmFlow.css'

/** Flujo del DM real (PMB-4.8): propuestas con diff y permisos explícitos. */
export function DmFlowView() {
  const go = useNavigate()
  const [proposals, setProposals] = useState<ContentProposalDto[] | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [grants, setGrants] = useState<ContentGrantDto[]>([])
  const [openDiff, setOpenDiff] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<
    { action: 'approve' | 'reject'; proposal: ContentProposalDto } | null>(null)
  const [subject, setSubject] = useState('')
  const [subjectKind, setSubjectKind] = useState('user')
  const [permission, setPermission] = useState('view')

  const reload = async () => {
    try {
      setProposals(await apiFetch<ContentProposalDto[]>('/api/v1/content/proposals'))
      setError(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.dm.error)
    }
  }

  const reloadGrants = async (id: string) => {
    try {
      setGrants(await apiFetch<ContentGrantDto[]>(`/api/v1/content/items/${id}/grants`))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.dm.error)
    }
  }

  useEffect(() => {
    void reload()
  }, [])

  useEffect(() => {
    setGrants([])
    if (selectedId) void reloadGrants(selectedId)
  }, [selectedId])

  const decide = async () => {
    if (!confirm) return
    const { action, proposal } = confirm
    setConfirm(null)
    try {
      const dto = await apiFetch<{ workflowState: string; currentRevision: number }>(
        `/api/v1/content/items/${proposal.id}/${action}`,
        { method: 'POST', body: action === 'reject' ? { reason: null } : {} })
      setNotice(action === 'approve'
        ? t.dm.approved(proposal.name, dto.currentRevision)
        : t.dm.rejected(proposal.name))
      if (selectedId === proposal.id) setSelectedId(null)
      await reload()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.dm.error)
    }
  }

  const addGrant = async () => {
    if (!selectedId || !subject.trim()) return
    try {
      await apiFetch<ContentGrantDto>(`/api/v1/content/items/${selectedId}/grants`, {
        method: 'POST',
        body: { subjectKind, subjectId: subject.trim(), permission },
      })
      setNotice(t.dm.added(permission, subject.trim()))
      setSubject('')
      await reloadGrants(selectedId)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.dm.error)
    }
  }

  const removeGrant = async (grant: ContentGrantDto) => {
    if (!selectedId) return
    try {
      await apiFetch<void>(`/api/v1/content/items/${selectedId}/grants/${grant.id}`,
        { method: 'DELETE' })
      setNotice(t.dm.removed(grant.subjectId))
      await reloadGrants(selectedId)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.dm.error)
    }
  }

  return (
    <div className="dm-flow">
      <header className="dm-flow__head">
        <div>
          <h1 className="biblio__title">{t.dm.title}</h1>
          <p className="biblio__subtitle">{t.dm.subtitle}</p>
        </div>
      </header>
      {error && <p className="biblio-status biblio-status--error" role="alert">{error}</p>}
      {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}

      <section className="dm-flow__panel" aria-label="Bandeja del DM">
        <h2 className="biblio-zone__title">{t.dm.inbox((proposals ?? []).length)}</h2>
        {proposals === null ? (
          <p className="biblio-inspector__empty">{t.biblioteca.loading}</p>
        ) : proposals.length === 0 ? (
          <div className="first-time" role="note">
            <img src="/icons/riot-shield.svg" alt="" aria-hidden="true" />
            <div>
              <h3>{t.dm.firstTitle}</h3>
              <p>{t.dm.firstText}</p>
              <Button variant="primary" onClick={() => go({ kind: 'biblioteca' })}>
                {t.dm.firstAction}
              </Button>
            </div>
          </div>
        ) : (
          <ul className="dm-list">
            {proposals.map((proposal) => (
              <li key={proposal.id} className="dm-card">
                <div className="dm-card__head">
                  <button type="button" className="biblio-rel__link"
                    onClick={() => setSelectedId(proposal.id)}>
                    <strong>{proposal.name}</strong>
                  </button>
                  <span className="biblio-tag">{proposal.authorName}</span>
                  <span className="biblio-inspector__note">
                    rev. {proposal.currentRevision}
                  </span>
                </div>
                <button type="button" aria-expanded={openDiff === proposal.id}
                  onClick={() => setOpenDiff(openDiff === proposal.id ? null : proposal.id)}>
                  {openDiff === proposal.id ? t.dm.hideChanges : t.dm.changes}
                </button>
                {openDiff === proposal.id && (
                  <ul className="dm-diff">
                    {proposal.changes.map((line) => <li key={line}>{line}</li>)}
                  </ul>
                )}
                <div className="dm-card__actions">
                  <button type="button" onClick={() => setConfirm({ action: 'approve', proposal })}>
                    {t.dm.approve}
                  </button>
                  <button type="button" onClick={() => setConfirm({ action: 'reject', proposal })}>
                    {t.dm.reject}
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="dm-flow__panel" aria-label={t.dm.grants}>
        <h2 className="biblio-zone__title">{t.dm.grants}</h2>
        <p className="biblio-inspector__note">{t.dm.grantsHint}</p>
        {selectedId === null ? (
          <p className="biblio-inspector__empty">
            Selecciona una propuesta para ver sus permisos.
          </p>
        ) : (
          <>
            <ul className="dm-list">
              {grants.map((grant) => (
                <li key={grant.id} className="dm-grant">
                  <span className="biblio-tag">{grant.subjectKind}</span>
                  <strong>{grant.subjectId}</strong>
                  <span className="biblio-tag">{grant.permission}</span>
                  <button type="button" onClick={() => void removeGrant(grant)}>
                    {t.dm.remove}
                  </button>
                </li>
              ))}
            </ul>
            <div className="dm-grant-form">
              <input aria-label={t.dm.subject} placeholder={t.dm.subject}
                value={subject} onChange={(event) => setSubject(event.target.value)} />
              <select aria-label={t.dm.subjectKind} value={subjectKind}
                onChange={(event) => setSubjectKind(event.target.value)}>
                <option value="user">user</option>
                <option value="role">role</option>
              </select>
              <select aria-label={t.dm.permission} value={permission}
                onChange={(event) => setPermission(event.target.value)}>
                <option value="view">view</option>
                <option value="edit">edit</option>
              </select>
              <button type="button" onClick={() => void addGrant()}>{t.dm.addGrant}</button>
            </div>
          </>
        )}
      </section>

      {confirm && (
        <ConfirmDialog
          open
          title={confirm.action === 'approve' ? t.dm.approve : t.dm.reject}
          text={confirm.proposal.name}
          confirmLabel={confirm.action === 'approve' ? t.dm.approve : t.dm.reject}
          cancelLabel={t.editor.actions.cancel}
          onConfirm={() => void decide()}
          onCancel={() => setConfirm(null)}
        />
      )}
    </div>
  )
}
