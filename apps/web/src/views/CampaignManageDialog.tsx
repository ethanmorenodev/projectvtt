import { useEffect, useState } from 'react'
import { apiFetch } from '../api/client'
import { deleteCover, uploadCover, useCampaignCover } from '../api/covers'
import type {
  CampaignDetail,
  CampaignRole,
  CampaignSummary,
  InviteInfo,
  RulesetInfo,
} from '../api/types'
import type { CampaignMember } from '../api/types'
import {
  Badge,
  Button,
  ConfirmDialog,
  Dialog,
  EmptyState,
  Field,
  Input,
  List,
  ListRow,
  useToast,
} from '../ui'
import { t } from '../i18n'
import { relativeTime } from './presentation'

/**
 * Gestión de campaña (PMB-2.4): las siete capacidades de API del informe
 * de auditoría §2.3 — renombrar, descripción, portada opcional, archivar,
 * crear/revocar invitaciones, cambiar rol y expulsar miembros, más el
 * listado de rulesets (GET /rulesets). Permisos y validación en servidor.
 * Cadenas en el catálogo i18n (ADR-038).
 */

interface Props {
  /** Solo usa id/name/role/description/hasCover (acepta Summary o Detail). */
  campaign: Pick<CampaignSummary, 'id' | 'name' | 'role' | 'description' | 'hasCover'>
  open: boolean
  labels: { title: string; close: string }
  onClose: () => void
  onChanged: () => void
}

const ROLES: Array<{ value: CampaignRole; label: string }> = [
  { value: 'Owner', label: 'Propietario' },
  { value: 'GM', label: 'Director de juego' },
  { value: 'Player', label: 'Jugador' },
]

export function CampaignManageDialog({ campaign, open, labels, onClose, onChanged }: Props) {
  const toast = useToast()
  const [name, setName] = useState(campaign.name)
  const [description, setDescription] = useState(campaign.description ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [invites, setInvites] = useState<InviteInfo[] | null>(null)
  const [members, setMembers] = useState<CampaignMember[] | null>(null)
  const [rulesets, setRulesets] = useState<RulesetInfo[]>([])
  const [confirm, setConfirm] = useState<'archive' | `revoke:${string}` | `remove:${string}` | null>(null)
  const coverUrl = useCampaignCover(campaign.id, campaign.hasCover)
  const manage = t.campanas.manage

  const canManage = campaign.role === 'Owner'

  useEffect(() => {
    if (!open) {
      return
    }
    setName(campaign.name)
    setDescription(campaign.description ?? '')
    setError(null)
    setConfirm(null)
    void apiFetch<InviteInfo[]>(`/api/v1/campaigns/${campaign.id}/invites`)
      .then(setInvites)
      .catch(() => setInvites([]))
    void apiFetch<CampaignMember[]>(`/api/v1/campaigns/${campaign.id}/members`)
      .then(setMembers)
      .catch(() => setMembers([]))
    void apiFetch<RulesetInfo[]>('/api/v1/rulesets')
      .then(setRulesets)
      .catch(() => setRulesets([]))
  }, [open, campaign])

  async function saveDetails() {
    setBusy(true)
    setError(null)
    try {
      await apiFetch<CampaignDetail>(`/api/v1/campaigns/${campaign.id}`, {
        method: 'PATCH',
        body: { name, description },
      })
      toast.show({ title: manage.savedToast, tone: 'success' })
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.saveError)
    } finally {
      setBusy(false)
    }
  }

  async function onCoverSelected(file: File | null) {
    if (!file) {
      return
    }
    setBusy(true)
    setError(null)
    try {
      await uploadCover(campaign.id, file)
      toast.show({ title: manage.coverUpdatedToast, tone: 'success' })
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.coverError)
    } finally {
      setBusy(false)
    }
  }

  async function removeCover() {
    setBusy(true)
    setError(null)
    try {
      await deleteCover(campaign.id)
      toast.show({ title: manage.coverRemovedToast, tone: 'success' })
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campanas.coverRemoveError)
    } finally {
      setBusy(false)
    }
  }

  async function createInvite() {
    setBusy(true)
    setError(null)
    try {
      const invite = await apiFetch<InviteInfo>(`/api/v1/campaigns/${campaign.id}/invites`, {
        method: 'POST',
        body: { role: 'Player', expiresInDays: 7 },
      })
      setInvites((current) => [invite, ...(current ?? [])])
      toast.show({ title: manage.inviteCreatedToast(invite.code), tone: 'success' })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.inviteError)
    } finally {
      setBusy(false)
    }
  }

  async function revokeInvite(inviteId: string) {
    setBusy(true)
    try {
      await apiFetch(`/api/v1/campaigns/${campaign.id}/invites/${inviteId}`, { method: 'DELETE' })
      setInvites((current) => (current ?? []).map((i) => (i.id === inviteId ? { ...i, revokedAt: new Date().toISOString() } : i)))
      toast.show({ title: manage.inviteRevokedToast, tone: 'success' })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.revokeError)
    } finally {
      setBusy(false)
    }
  }

  async function updateRole(userId: string, role: CampaignRole) {
    setBusy(true)
    setError(null)
    try {
      await apiFetch(`/api/v1/campaigns/${campaign.id}/members/${userId}`, {
        method: 'PATCH',
        body: { role },
      })
      setMembers((current) => (current ?? []).map((m) => (m.userId === userId ? { ...m, role } : m)))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.roleError)
    } finally {
      setBusy(false)
    }
  }

  async function removeMember(userId: string) {
    setBusy(true)
    try {
      await apiFetch(`/api/v1/campaigns/${campaign.id}/members/${userId}`, { method: 'DELETE' })
      setMembers((current) => (current ?? []).filter((m) => m.userId !== userId))
      toast.show({ title: manage.memberRemovedToast, tone: 'success' })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.removeError)
    } finally {
      setBusy(false)
    }
  }

  async function archive() {
    setBusy(true)
    try {
      await apiFetch(`/api/v1/campaigns/${campaign.id}/archive`, { method: 'POST' })
      toast.show({ title: manage.archivedToast, tone: 'success' })
      onChanged()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : manage.archiveError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog
      open={open}
      title={labels.title}
      closeLabel={labels.close}
      onClose={onClose}
      actions={<Button variant="ghost" onClick={onClose}>{labels.close}</Button>}
    >
      {error ? <p className="error">{error}</p> : null}

      <section className="pv-dialog__section">
        <h3 className="pv-dialog__section-title">{manage.general}</h3>
        <Field label={manage.nameLabel}>
          {({ id, ...aria }) => (
            <Input id={id} value={name} maxLength={100} disabled={!canManage}
              onChange={(event) => setName(event.target.value)} {...aria} />
          )}
        </Field>
        <Field label={manage.descriptionLabel} helper={manage.descriptionHelper}>
          {({ id, ...aria }) => (
            <Input id={id} value={description} maxLength={500} disabled={!canManage}
              placeholder="De qué va la campaña"
              onChange={(event) => setDescription(event.target.value)} {...aria} />
          )}
        </Field>
        <div className="pv-dialog__section">
          <span className="pv-field__label">{manage.cover}</span>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
            <div className="pv-cover" style={{ width: 64, height: 64 }}>
              {coverUrl ? <img src={coverUrl} alt="" /> : null}
            </div>
            {canManage ? (
              <>
                <label className="pv-btn pv-btn--secondary">
                  {campaign.hasCover ? manage.change : manage.upload}
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    style={{ display: 'none' }}
                    onChange={(event) => void onCoverSelected(event.target.files?.[0] ?? null)}
                  />
                </label>
                {campaign.hasCover ? (
                  <Button variant="ghost" onClick={() => void removeCover()}>{manage.remove}</Button>
                ) : null}
              </>
            ) : null}
          </div>
        </div>
        {canManage ? (
          <div style={{ display: 'flex', gap: 8 }}>
            <Button variant="primary" disabled={busy} onClick={() => void saveDetails()}>
              {manage.save}
            </Button>
            <Button variant="danger" onClick={() => setConfirm('archive')}>{manage.archive}</Button>
          </div>
        ) : null}
      </section>

      <section className="pv-dialog__section">
        <h3 className="pv-dialog__section-title">{manage.invites}</h3>
        {canManage ? (
          <div>
            <Button variant="secondary" disabled={busy} onClick={() => void createInvite()}>
              {manage.createInvite}
            </Button>
          </div>
        ) : null}
        {invites === null ? (
          <p className="muted">{manage.loadingInvites}</p>
        ) : invites.length === 0 ? (
          <EmptyState title={manage.noInvites} text={manage.noInvitesText} />
        ) : (
          <List>
            {invites.map((invite) => (
              <ListRow
                key={invite.id}
                title={invite.code}
                subtitle={manage.inviteMeta(
                  invite.role,
                  relativeTime(invite.expiresAt) || invite.expiresAt,
                )}
                badge={invite.revokedAt ? <Badge tone="neutral">{manage.inviteRevoked}</Badge> : <Badge tone="accent">{manage.inviteActive}</Badge>}
                actions={
                  !invite.revokedAt && canManage ? (
                    <Button variant="ghost" onClick={() => setConfirm(`revoke:${invite.id}`)}>{manage.revoke}</Button>
                  ) : null
                }
              />
            ))}
          </List>
        )}
      </section>

      <section className="pv-dialog__section">
        <h3 className="pv-dialog__section-title">{manage.members}</h3>
        {members === null ? (
          <p className="muted">{manage.loadingMembers}</p>
        ) : (
          <List>
            {members.map((member) => (
              <ListRow
                key={member.userId}
                title={member.userName}
                subtitle={manage.joinedAt(relativeTime(member.joinedAt))}
                actions={
                  canManage && member.role !== 'Owner' ? (
                    <>
                      <select
                        className="pv-input"
                        aria-label={manage.roleSelect(member.userName)}
                        value={member.role}
                        disabled={busy}
                        onChange={(event) =>
                          void updateRole(member.userId, event.target.value as CampaignRole)
                        }
                      >
                        {ROLES.map((role) => (
                          <option key={role.value} value={role.value}>{role.label}</option>
                        ))}
                      </select>
                      <Button variant="ghost" onClick={() => setConfirm(`remove:${member.userId}`)}>
                        {manage.removeMember}
                      </Button>
                    </>
                  ) : (
                    <Badge tone="neutral">{member.role}</Badge>
                  )
                }
              />
            ))}
          </List>
        )}
      </section>

      <section className="pv-dialog__section">
        <h3 className="pv-dialog__section-title">{manage.rules}</h3>
        <List>
          {rulesets.map((ruleset) => (
            <ListRow key={ruleset.key} title={ruleset.displayName} subtitle={ruleset.key} />
          ))}
        </List>
      </section>

      <ConfirmDialog
        open={confirm === 'archive'}
        title={manage.confirmArchive.title}
        text={manage.confirmArchive.text}
        confirmLabel={manage.confirmArchive.confirm}
        cancelLabel={manage.confirmArchive.cancel}
        destructive
        onConfirm={() => {
          setConfirm(null)
          void archive()
        }}
        onCancel={() => setConfirm(null)}
      />
      <ConfirmDialog
        open={confirm?.startsWith('revoke:') ?? false}
        title={manage.confirmRevoke.title}
        text={manage.confirmRevoke.text}
        confirmLabel={manage.confirmRevoke.confirm}
        cancelLabel={manage.confirmRevoke.cancel}
        destructive
        onConfirm={() => {
          const id = confirm?.slice('revoke:'.length) ?? ''
          setConfirm(null)
          void revokeInvite(id)
        }}
        onCancel={() => setConfirm(null)}
      />
      <ConfirmDialog
        open={confirm?.startsWith('remove:') ?? false}
        title={manage.confirmRemove.title}
        text={manage.confirmRemove.text}
        confirmLabel={manage.confirmRemove.confirm}
        cancelLabel={manage.confirmRemove.cancel}
        destructive
        onConfirm={() => {
          const id = confirm?.slice('remove:'.length) ?? ''
          setConfirm(null)
          void removeMember(id)
        }}
        onCancel={() => setConfirm(null)}
      />
    </Dialog>
  )
}
