import { useState } from 'react'
import { Badge, Button, EmptyState, IconButton, List, ListRow } from '../../index'
import { PrototypeShell } from './AreasFase2'
import type { Variant } from './AreasFase2'
import './fase2.css'
import '../prototype.css'

/**
 * Prototipo del Espacio de campaña (PMB-2.6) — solo desarrollo, datos
 * estáticos. Doble nivel de navegación (raíl de app + subnavegación de
 * campaña) con las secciones del plan §3.1. Variantes de revisión:
 * `normal` | `vacio` | `extremo`. **No integrar hasta aprobación explícita.**
 */

const SECTIONS = [
  'Resumen',
  'Mesa de juego',
  'Escenas y mapas',
  'Personajes',
  'NPC',
  'Criaturas',
  'Biblioteca de campaña',
  'Contenido homebrew',
  'Historia y lore',
  'Facciones',
  'Localizaciones',
  'Participantes y permisos',
  'Configuración',
] as const

type Section = (typeof SECTIONS)[number]

interface Scene {
  name: string
  grid: string
  tokens: number
  cover?: string
}

interface Personaje {
  name: string
  player: string
  cover?: string
}

function Thumb({ label }: { label: string }) {
  return (
    <div className="f2-thumb" aria-hidden="true">
      {label}
    </div>
  )
}

function Estado({
  title,
  text,
  fase,
}: {
  title: string
  text: string
  fase: string
}) {
  return <EmptyState title={title} text={`${text} (se construye en ${fase}).`} />
}

export function EspacioCampanaPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const [section, setSection] = useState<Section>('Resumen')
  const vacio = variant === 'vacio'
  const extremo = variant === 'extremo'

  const escenas: Scene[] = extremo
    ? Array.from({ length: 12 }, (_, index) => ({
        name: index === 0
          ? 'La batalla final en las murallas de la ciudadela de Scaldar y el asedio de la guardia nocturna'
          : `Escena ${index + 1}`,
        grid: index % 2 ? 'hex 48px' : 'square 32px',
        tokens: index * 2,
        cover: index % 3 ? undefined : '🗺',
      }))
    : [
        { name: 'Puerto de Scaldar', grid: 'hex 48px', tokens: 6, cover: '🗺' },
        { name: 'La ciudadela', grid: 'square 32px', tokens: 2 },
        { name: 'Cloacas', grid: 'square 32px', tokens: 0 },
      ]

  const personajes: Personaje[] = extremo
    ? Array.from({ length: 9 }, (_, index) => ({
        name: index === 0 ? 'Elenar de la Casa Valdris, guardiana del pacto' : `Personaje ${index + 1}`,
        player: index % 3 === 0 ? 'ana' : index % 3 === 1 ? 'luis' : 'marcos',
        cover: index % 2 ? undefined : '🧝',
      }))
    : [
        { name: 'Elenar', player: 'ana', cover: '🧝' },
        { name: 'Brom', player: 'luis' },
        { name: 'Syl', player: 'marcos', cover: '🧙' },
      ]

  const vacioNote = vacio
    ? { title: 'Aún no hay nada aquí', text: 'Crea el primer elemento de esta sección' }
    : null

  return (
    <PrototypeShell active="campana">
      <div className="proto-split">
        <nav className="proto-subnav">
          <div className="proto-subnav__title">La caída de Scaldar</div>
          {SECTIONS.map((entry) => (
            <div
              key={entry}
              className={`proto-subnav__item${section === entry ? ' proto-subnav__item--active' : ''}`}
              onClick={() => setSection(entry)}
            >
              {entry}
            </div>
          ))}
        </nav>

        <main className="f2-area">
          <header className="f2-page-head">
            <h1 className="f2-page-title">{section}</h1>
            <div className="f2-page-actions">
              <Badge tone="accent">Activa</Badge>
              <Button variant="secondary">Gestionar</Button>
            </div>
          </header>

          {section === 'Resumen' && (
            <>
              <div className="f2-stats">
                <div className="proto-stat"><strong>{vacio ? 0 : escenas.length}</strong><span>escenas</span></div>
                <div className="proto-stat"><strong>{vacio ? 0 : personajes.length}</strong><span>personajes</span></div>
                <div className="proto-stat"><strong>{vacio ? 0 : 4}</strong><span>jugadores</span></div>
                <div className="proto-stat"><strong>{vacio ? 0 : 38}</strong><span>elementos de biblioteca</span></div>
              </div>
              {vacioNote ? (
                <EmptyState title="Campaña vacía" text="Crea una escena o un personaje para empezar." action={<Button variant="primary">Crear escena</Button>} />
              ) : (
                <List>
                  <ListRow title="Última sesión: combate en el puerto — ronda 3" meta={<span className="pv-row__meta-time">hace 2 h</span>} />
                  <ListRow title="5 movimientos de tokens, 2 tiradas, 1 hechizo" meta={<span className="pv-row__meta-time">ayer</span>} />
                </List>
              )}
            </>
          )}

          {section === 'Escenas y mapas' && (vacioNote ? (
            <EmptyState title={vacioNote.title} text={`${vacioNote.text} de escenas.`} action={<Button variant="primary">Crear escena</Button>} />
          ) : (
            <List>
              {escenas.map((scene) => (
                <ListRow
                  key={scene.name}
                  thumb={<Thumb label={scene.cover ?? '🗺'} />}
                  title={scene.name}
                  badge={<Badge tone="neutral">{scene.grid}</Badge>}
                  meta={<span className="pv-row__meta-time">{scene.tokens} tokens</span>}
                  actions={<IconButton icon={<span aria-hidden="true">→</span>} label={`Abrir ${scene.name}`} />}
                />
              ))}
            </List>
          ))}

          {section === 'Personajes' && (vacioNote ? (
            <EmptyState title={vacioNote.title} text={`${vacioNote.text} de personajes.`} action={<Button variant="primary">Crear personaje</Button>} />
          ) : (
            <div className="f2-thumb-grid">
              {personajes.map((personaje) => (
                <div className="proto-thumb-card" key={personaje.name}>
                  <Thumb label={personaje.cover ?? personaje.name.charAt(0)} />
                  <span>{personaje.name}</span>
                  <span className="pv-row__meta-time">{personaje.player}</span>
                </div>
              ))}
            </div>
          ))}

          {section === 'Participantes y permisos' && (vacioNote ? (
            <EmptyState title={vacioNote.title} text={`${vacioNote.text} de participantes.`} />
          ) : (
            <List>
              <ListRow title="ana" badge={<Badge tone="accent">Propietaria</Badge>} subtitle="GM de la campaña" />
              <ListRow title="luis" badge={<Badge tone="neutral">Jugador</Badge>} subtitle="Brom · 2 personajes" />
              <ListRow title="marcos" badge={<Badge tone="neutral">Jugador</Badge>} subtitle="Syl · 1 personaje" />
            </List>
          ))}

          {section === 'Biblioteca de campaña' && (vacioNote ? (
            <EmptyState title={vacioNote.title} text={`${vacioNote.text} de biblioteca.`} action={<Button variant="primary">Crear contenido</Button>} />
          ) : (
            <div className="f2-thumb-grid">
              {['Espada larga', 'Poción', 'Alta elfa', 'Ghoul', 'Bola de fuego', 'Cota de malla'].map((entry) => (
                <div className="proto-thumb-card" key={entry}>
                  <Thumb label={entry.charAt(0)} />
                  <span>{entry}</span>
                </div>
              ))}
            </div>
          ))}

          {!['Resumen', 'Escenas y mapas', 'Personajes', 'Participantes y permisos', 'Biblioteca de campaña'].includes(section) && (
            <Estado
              title={section}
              text="Esta sección está definida en la navegación y llegará con su fase"
              fase={
                section === 'Mesa de juego' ? 'la Fase 2 (rediseño de la mesa)'
                : section === 'Contenido homebrew' ? 'la Fase 5'
                : section === 'Configuración' ? 'la Fase 2'
                : 'las Fases 4–7'
              }
            />
          )}
        </main>
      </div>
    </PrototypeShell>
  )
}
