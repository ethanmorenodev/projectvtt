import { useState } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import {
  SAMPLE_GRANTS,
  SAMPLE_PROPOSALS,
  type Permission,
  type ProtoGrant,
  type ProtoProposal,
  type Workflow,
} from './dmFlowData'
import '../../../views/dmFlow.css'

/**
 * Prototipo del flujo del DM (PMB-4.8, solo desarrollo): propuestas del
 * autor, bandeja de aprobación del DM con diff, y permisos explícitos.
 * Datos de muestra; la app real no se toca en esta fase.
 */

const STATE_LABELS: Record<Workflow, string> = {
  borrador: 'Borrador',
  propuesta: 'Propuesta',
  publicado: 'Publicado',
}

export function DmFlowPrototype({ variant = 'normal' }: { variant?: 'normal' | 'vacio' }) {
  const [items, setItems] = useState<ProtoProposal[]>(
    () => (variant === 'vacio' ? [] : SAMPLE_PROPOSALS.map((item) => ({ ...item }))))
  const [view, setView] = useState<'autor' | 'dm'>('dm')
  const [openDiff, setOpenDiff] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [grants, setGrants] = useState<ProtoGrant[]>(() => [...SAMPLE_GRANTS])
  const [subject, setSubject] = useState('')
  const [kind, setKind] = useState<'user' | 'role'>('user')
  const [permission, setPermission] = useState<Permission>('view')

  const change = (id: string, state: Workflow, message: string) => {
    setItems((current) => current.map((item) => item.id === id
      ? {
          ...item,
          state,
          revision: state === 'publicado' ? item.revision + 1 : item.revision,
        }
      : item))
    setOpenDiff(null)
    setNotice(message)
  }

  const pending = items.filter((item) => item.state === 'propuesta')
  const own = items.filter((item) => item.author === 'Mira Colmillo')

  return (
    <PrototypeShell active="campana">
      <div className="dm-flow">
        <header className="dm-flow__head">
          <div>
            <h1 className="biblio__title">Flujo del DM</h1>
            <p className="biblio__subtitle">Propuestas, aprobación y permisos explícitos</p>
          </div>
          <div className="dm-flow__toggle" role="group" aria-label="Perspectiva">
            <button type="button" aria-pressed={view === 'autor'} onClick={() => setView('autor')}>
              Vista autor
            </button>
            <button type="button" aria-pressed={view === 'dm'} onClick={() => setView('dm')}>
              Vista DM
            </button>
          </div>
        </header>

        {notice && <p className="biblio-inspector__note" role="status">{notice}</p>}

        <section className="dm-flow__panel" aria-label={view === 'dm' ? 'Bandeja del DM' : 'Mis propuestas'}>
          <h2 className="biblio-zone__title">
            {view === 'dm' ? `Bandeja del DM · ${pending.length} pendiente(s)` : 'Mis contenidos'}
          </h2>
          {(view === 'dm' ? pending : own).length === 0 ? (
            <p className="biblio-inspector__empty">
              {variant === 'vacio' || (view === 'dm' && pending.length === 0)
                ? 'No hay propuestas pendientes de revisión.'
                : 'Todavía no has creado contenido.'}
            </p>
          ) : (
            <ul className="dm-list">
              {(view === 'dm' ? pending : own).map((item) => (
                <li key={item.id} className="dm-card">
                  <div className="dm-card__head">
                    <strong>{item.name}</strong>
                    <span className="biblio-tag">{STATE_LABELS[item.state]}</span>
                    <span className="biblio-inspector__note">{item.author} · rev. {item.revision}</span>
                  </div>
                  <button type="button" aria-expanded={openDiff === item.id}
                    onClick={() => setOpenDiff(openDiff === item.id ? null : item.id)}>
                    {openDiff === item.id ? 'Ocultar cambios' : 'Ver cambios'}
                  </button>
                  {openDiff === item.id && (
                    <ul className="dm-diff">
                      {item.changes.map((line) => <li key={line}>{line}</li>)}
                    </ul>
                  )}
                  <div className="dm-card__actions">
                    {view === 'autor' && item.state === 'borrador' && (
                      <button type="button" onClick={() => change(item.id, 'propuesta',
                        `«${item.name}» enviada a revisión del DM.`)}>
                        Proponer
                      </button>
                    )}
                    {view === 'dm' && (
                      <>
                        <button type="button" onClick={() => change(item.id, 'publicado',
                          `«${item.name}» aprobada: revisión ${item.revision + 1} publicada.`)}>
                          Aprobar y publicar
                        </button>
                        <button type="button" onClick={() => change(item.id, 'borrador',
                          `«${item.name}» devuelta a borrador.`)}>
                          Rechazar
                        </button>
                      </>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="dm-flow__panel" aria-label="Permisos del contenido">
          <h2 className="biblio-zone__title">Permisos explícitos</h2>
          <p className="biblio-inspector__note">
            Se suman al ámbito del item; el servidor decide la autoridad final.
          </p>
          <ul className="dm-list">
            {grants.map((grant) => (
              <li key={grant.id} className="dm-grant">
                <span className="biblio-tag">{grant.kind}</span>
                <strong>{grant.subject}</strong>
                <span className="biblio-tag">{grant.permission}</span>
                <button type="button" onClick={() => {
                  setGrants((current) => current.filter((entry) => entry.id !== grant.id))
                  setNotice(`Permiso de «${grant.subject}» eliminado.`)
                }}>
                  Quitar
                </button>
              </li>
            ))}
          </ul>
          <div className="dm-grant-form">
            <input aria-label="Destinatario" placeholder="Usuario o rol"
              value={subject} onChange={(event) => setSubject(event.target.value)} />
            <select aria-label="Tipo de destinatario" value={kind}
              onChange={(event) => setKind(event.target.value as 'user' | 'role')}>
              <option value="user">user</option>
              <option value="role">role</option>
            </select>
            <select aria-label="Permiso" value={permission}
              onChange={(event) => setPermission(event.target.value as Permission)}>
              <option value="view">view</option>
              <option value="edit">edit</option>
            </select>
            <button type="button" onClick={() => {
              if (!subject.trim()) return
              setGrants((current) => [...current, {
                id: `g-${current.length + 1}`, subject: subject.trim(), kind, permission,
              }])
              setSubject('')
              setNotice(`Permiso «${permission}» concedido a «${subject.trim()}».`)
            }}>
              Añadir permiso
            </button>
          </div>
        </section>
      </div>
    </PrototypeShell>
  )
}
