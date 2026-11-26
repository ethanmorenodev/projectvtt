import { useState } from 'react'
import type { ReactNode } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import './hub.css'

/**
 * Prototipo del Character Hub (PMB-3.6) — solo desarrollo
 * (`#prototipo-hub[-vacio|-extremo]`), montado en el shell real.
 * Ventana tipo menú de RPG (CHARACTERS.md §3): Perfil / Equipo /
 * Relaciones / Historia. El Hub real será una **ventana** del sistema de
 * ventanas (el jugador no abandona la mesa) sincronizada con la ficha
 * clásica; la privacidad del DM se aplica en servidor (aquí se muestra la
 * sección marcada «Solo DM» para revisión del comportamiento).
 */

type Variant = 'normal' | 'vacio' | 'extremo'
type Tab = 'perfil' | 'equipo' | 'relaciones' | 'historia'

const SLOTS = [
  { id: 'main', label: 'Arma principal', item: 'Rapier' },
  { id: 'off', label: 'Secundaria', item: 'Shortbow' },
  { id: 'armor', label: 'Armadura', item: 'Leather armor' },
  { id: 'helm', label: 'Casco', item: null },
  { id: 'gloves', label: 'Guantes', item: null },
  { id: 'boots', label: 'Botas', item: 'Boots of elvenkind' },
  { id: 'acc1', label: 'Accesorio 1', item: null },
  { id: 'acc2', label: 'Accesorio 2', item: 'Ring of protection' },
]

const INVENTORY = [
  { name: 'Thieves’ tools', qty: 1 },
  { name: 'Potion of healing', qty: 2 },
  { name: 'Torch', qty: 5 },
  { name: 'Rope, silk (50 ft)', qty: 1 },
]

const RELATIONS = [
  { from: 'Elenar', to: 'Brom (compañero)', kind: 'ally' },
  { from: 'Elenar', to: 'Gremio de ladrones', kind: 'faction' },
  { from: 'Elenar', to: 'Capitán K.I.W.I.', kind: 'rival' },
  { from: 'Elenar', to: 'Syl (hermana)', kind: 'family' },
]

const TIMELINE = [
  { when: 'Sesión 12', what: 'Combate en el puerto de Scaldar; Elenar cayó a 3 PG.' },
  { when: 'Sesión 11', what: 'Acuerdo con el gremio de ladrones a cambio del mapa.' },
  { when: 'Sesión 9', what: 'Descubrimiento de la entrada a las cloacas.' },
]

export function CharacterHubPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const [tab, setTab] = useState<Tab>('perfil')
  const [dmLore, setDmLore] = useState(true)
  const empty = variant === 'vacio'
  const long = variant === 'extremo'
  const name = long
    ? 'Guardia de élite de la ciudadela de Scaldar con armadura ceremonial'
    : 'Elenar'

  return (
    <PrototypeShell active="mesa">
      <div className="hub-overlay">
        <section className="hub" aria-label="Character Hub">
          <header className="hub__head">
            <div className="hub__portrait" aria-hidden="true">Retrato</div>
            <div className="hub__ident">
              <h1 className="hub__name">{name}</h1>
              <span className="hub__meta">Nivel 1 · Rogue (Thief) · Enano de montaña</span>
            </div>
            <nav className="hub__tabs" aria-label="Secciones del Hub">
              {(['perfil', 'equipo', 'relaciones', 'historia'] as Tab[]).map((entry) => (
                <button
                  key={entry}
                  type="button"
                  className={`hub__tab${tab === entry ? ' hub__tab--on' : ''}`}
                  aria-current={tab === entry ? 'page' : undefined}
                  onClick={() => setTab(entry)}
                >
                  {entry === 'perfil' ? 'Perfil' : entry === 'equipo' ? 'Equipo' : entry === 'relaciones' ? 'Relaciones' : 'Historia'}
                </button>
              ))}
            </nav>
          </header>

          <div className="hub__body">
            {tab === 'perfil' ? (
              <div className="hub-cols">
                <Panel title="Atributos">
                  <div className="hub-attrs">
                    {(['Fue', 'Des', 'Con', 'Int', 'Sab', 'Car'] as const).map((ability, index) => (
                      <div key={ability} className="hub-attr">
                        <strong>{empty ? 8 : [17, 14, 16, 10, 13, 8][index]}</strong>
                        <span>{ability}</span>
                        <span className="hub-attr__mod">
                          {empty ? '−1' : ['+3', '+2', '+3', '+0', '+1', '−1'][index]}
                        </span>
                      </div>
                    ))}
                  </div>
                </Panel>
                <Panel title="Estado">
                  {empty ? (
                    <Empty text="Sin recursos registrados." />
                  ) : (
                    <ul className="hub-list">
                      <li>Puntos de golpe: 9/9</li>
                      <li>Dado de golpe: 1d8</li>
                      <li>CA: 14 · Iniciativa: +2 · Velocidad: 25 ft</li>
                      <li>Condiciones: ninguna</li>
                    </ul>
                  )}
                </Panel>
                <Panel title="Personalidad">
                  <p className="hub-text">
                    {empty
                      ? 'Sin descripción todavía.'
                      : 'Desconfiado con los extraños, leal hasta la muerte con su gremio. Mide las palabras y los bolsillos ajenos.'}
                  </p>
                </Panel>
              </div>
            ) : null}

            {tab === 'equipo' ? (
              <div className="hub-equip">
                <div className="hub-silhouette" aria-hidden="true">
                  <span>Silueta del personaje</span>
                </div>
                <div className="hub-slots">
                  {SLOTS.map((slot) => (
                    <div key={slot.id} className="hub-slot">
                      <span className="hub-slot__label">{slot.label}</span>
                      <span className={`hub-slot__item${!empty && slot.item ? ' hub-slot__item--on' : ''}`}>
                        {empty || !slot.item ? '—' : slot.item}
                      </span>
                    </div>
                  ))}
                </div>
                <Panel title="Mochila">
                  {empty ? (
                    <Empty text="La mochila está vacía." />
                  ) : (
                    <ul className="hub-list">
                      {INVENTORY.map((entry) => (
                        <li key={entry.name}>{entry.name} ×{entry.qty}</li>
                      ))}
                    </ul>
                  )}
                </Panel>
              </div>
            ) : null}

            {tab === 'relaciones' ? (
              empty ? (
                <Empty text="Sin relaciones registradas. Las relaciones nacen del contenido y de la historia compartida." />
              ) : (
                <div className="hub-graph">
                  {RELATIONS.map((relation) => (
                    <div key={relation.to} className={`hub-edge hub-edge--${relation.kind}`}>
                      <strong>{relation.from}</strong>
                      <span className="hub-edge__line" aria-hidden="true" />
                      <span>{long ? `${relation.to} — conexión larga para probar desbordes de texto en el grafo` : relation.to}</span>
                    </div>
                  ))}
                  <p className="hub-hint">
                    El grafo real se deriva de <code>ContentRelation</code> (ADR-031 §8).
                  </p>
                </div>
              )
            ) : null}

            {tab === 'historia' ? (
              <div className="hub-history">
                <Panel title="Cronología">
                  {empty ? (
                    <Empty text="Aún no hay acontecimientos." />
                  ) : (
                    <ul className="hub-timeline">
                      {TIMELINE.map((entry) => (
                        <li key={entry.when}>
                          <strong>{entry.when}</strong>
                          <span>{entry.what}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </Panel>
                <Panel
                  title={
                    <>
                      Lore privado del DM <span className="hub-badge">Solo DM</span>
                    </>
                  }
                >
                  <p className="hub-text">
                    {!dmLore
                      ? 'Oculta: el jugador no la vería (el servidor no la serializa hacia él).'
                      : empty
                        ? 'Sin notas privadas.'
                        : 'Elenar es buscado por la guardia por el robo del sello del puerto. Su hermana Syl negocia en secreto con el gremio.'}
                  </p>
                  <label className="hub-check">
                    <input
                      type="checkbox"
                      checked={dmLore}
                      onChange={(event) => setDmLore(event.target.checked)}
                    />
                    Vista previa como DM (en el Hub real esta sección no llega nunca al jugador: se filtra en servidor)
                  </label>
                </Panel>
              </div>
            ) : null}
          </div>

          <footer className="hub__foot">
            <span className="hub-hint">Ficha clásica y Hub comparten estado y eventos (una sola verdad).</span>
            <span className="hub__foot-actions">
              <button type="button" className="pv-btn pv-btn--ghost">Cerrar</button>
              <button type="button" className="pv-btn pv-btn--primary">Tirar d20</button>
            </span>
          </footer>
        </section>
      </div>
    </PrototypeShell>
  )
}

function Panel({ title, children }: { title: ReactNode; children: ReactNode }) {
  return (
    <div className="hub-panel">
      <h2 className="hub-panel__title">{title}</h2>
      {children}
    </div>
  )
}

function Empty({ text }: { text: string }) {
  return <p className="hub-empty">{text}</p>
}
