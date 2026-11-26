import { Badge, Button, Card, EmptyState, FilterChip, IconButton, List, ListRow } from '../../index'
import { AppShell } from '../../../shell/AppShell'
import type { ReactNode } from 'react'
import './fase2.css'

/**
 * Prototipos de contenido de la Fase 2 (PMB-2.1 Inicio, PMB-2.3
 * Campañas) — solo desarrollo, datos estáticos. Se montan dentro del
 * shell real (AppShell) para que la revisión sea representativa.
 *
 * Correcciones aprobadas (2026-11-09) aplicadas:
 *  1. Filas de dos líneas fijas (nunca crecen por envoltura), con orden
 *     de sacrificio documentado (COMPONENT_RULES §4).
 *  2. Acción rápida como icono-flecha discreto y siempre visible en
 *     listas densas (área 28 px, tooltip y aria-label); texto solo en
 *     elementos destacados.
 *  3. Sin CTA duplicados cuando representan la misma acción.
 *  4. Iniciales de portada alternativa con protagonismo reducido.
 *  5. Metadatos temporales normalizados («hace X») y alineados a la
 *     derecha en columna fija.
 *  6. Badge y FilterChip como componentes de la biblioteca.
 *
 * Variantes de revisión: `normal` | `vacio` | `extremo` | `sin-resultados`.
 * **No integrar hasta aprobación explícita.**
 */

export type Variant = 'normal' | 'vacio' | 'extremo' | 'sin-resultados'

/** Portada (simula arte del usuario) o alternativa discreta con inicial. */
function Thumb({ name, cover }: { name: string; cover?: string }) {
  return cover ? (
    <div className="f2-thumb f2-thumb--cover" aria-hidden="true">
      {cover}
    </div>
  ) : (
    <div className="f2-thumb f2-thumb--fallback" aria-hidden="true">
      {name.trim().charAt(0).toUpperCase()}
    </div>
  )
}

function stateTone(state: string): 'accent' | 'warning' | 'neutral' {
  return state === 'Activa' ? 'accent' : state === 'En pausa' ? 'warning' : 'neutral'
}

/** Acción rápida de lista densa: icono-flecha accesible (decisión 2). */
function RowAction({ label }: { label: string }) {
  return <IconButton icon={<span aria-hidden="true">→</span>} label={label} />
}

/** Envoltorio de revisión: los prototipos se ven dentro del shell real. */
export function PrototypeShell({
  active,
  children,
}: {
  active: 'inicio' | 'campanas' | 'campana' | 'mesa' | 'biblioteca'
  children: ReactNode
}) {
  return (
    <AppShell
      active={active}
      userName="Prototipo"
      onNavigate={() => undefined}
      onSignOut={() => undefined}
      palette={{ placeholder: 'Buscar (Ctrl+K): campañas, escenas, acciones…', emptyText: 'Sin resultados.', groups: [] }}
    >
      {children}
    </AppShell>
  )
}

function Block({
  title,
  action,
  children,
}: {
  title: string
  action?: ReactNode
  children: ReactNode
}) {
  return (
    <section className="f2-block">
      <header className="f2-block__head">
        <h2 className="f2-block__title">{title}</h2>
        {action}
      </header>
      {children}
    </section>
  )
}

interface Campana {
  name: string
  cover?: string
  description: string
  state: string
  role: string
  members: string
  seen: string
}

const RECENTES: Campana[] = [
  { name: 'La caída de Scaldar', cover: '🏰', description: '', state: 'Activa', role: 'GM', members: '4 jugadores', seen: 'hace 2 h' },
  { name: 'Crónicas del Mar Interior', cover: '⛵', description: '', state: 'Activa', role: 'Jugadora', members: '3 jugadores', seen: 'hace 1 día' },
  { name: 'One-shot: la mina perdida', cover: '⛏', description: '', state: 'Activa', role: 'GM', members: '5 jugadores', seen: 'hace 3 días' },
]

const RECENTES_EXTREMO: Campana[] = [
  { name: 'La épica campaña de las montañas de Scaldar y el asedio interminable de la ciudadela', description: '', state: 'Activa', role: 'GM', members: '4 jugadores', seen: 'hace 2 h' },
  { name: 'Crónicas del Mar Interior', cover: '⛵', description: '', state: 'Activa', role: 'Jugadora', members: '3 jugadores', seen: 'hace 1 día' },
  { name: 'One-shot: la mina perdida', cover: '⛏', description: '', state: 'Activa', role: 'GM', members: '5 jugadores', seen: 'hace 3 días' },
]

const ACTIVIDAD: Array<{ text: string; time: string }> = [
  { text: 'Ana movió «Guardia del puerto» en Escena del puerto', time: 'hace 12 min' },
  { text: 'Luis publicó «Elfos del bosque (variante)» en la biblioteca', time: 'hace 1 h' },
  { text: 'Marcos tiró percepción: 18 (d20 11 + 7)', time: 'hace 1 h' },
  { text: 'Nueva versión de «Espada larga» disponible para 2 personajes', time: 'hace 1 día' },
  { text: 'Se importó el mapa «Puerto de Scaldar»', time: 'hace 1 día' },
]

const INVITACIONES: Array<{ name: string; from: string }> = [
  { name: 'El pacto de ceniza', from: 'Marcos te invita como jugador' },
]

const ACCESO: Array<{ label: string; hint: string }> = [
  { label: 'Mis mundos', hint: '2 mundos' },
  { label: 'Biblioteca', hint: 'contenido reutilizable' },
]

/** PMB-2.1 — Inicio: volver a jugar, estar al día, crear. */
export function InicioPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const recientes = variant === 'extremo' ? RECENTES_EXTREMO : RECENTES
  const vacio = variant === 'vacio'

  return (
    <main className="f2-area">
      <header className="f2-page-head">
        <h1 className="f2-page-title">Inicio</h1>
        <div className="f2-page-actions">
          <Button variant="secondary">Unirse con código</Button>
          <Button variant="primary">Crear campaña</Button>
        </div>
      </header>

      <Block
        title="Continuar jugando"
        action={vacio ? undefined : <Button variant="ghost">Ver todas</Button>}
      >
        {vacio ? (
          // Sin CTA duplicado: la acción «Crear campaña» ya está en la
          // cabecera (decisión 3). El texto guía hacia las dos vías.
          <EmptyState
            title="Aún no hay campañas"
            text="Crea tu primera campaña con «Crear campaña» o únete a una con un código de invitación."
          />
        ) : (
          <div className="f2-card-grid">
            {recientes.slice(0, 3).map((entry) => (
              <Card
                key={entry.name}
                thumb={<Thumb name={entry.name} cover={entry.cover} />}
                title={entry.name}
                meta={
                  <>
                    <span>{entry.role}</span>
                    <span>{entry.members}</span>
                    <span>{entry.seen}</span>
                  </>
                }
                actions={<Button variant="ghost">Continuar →</Button>}
              />
            ))}
          </div>
        )}
      </Block>

      <div className="f2-columns">
        <Block title="Actividad reciente">
          {vacio ? (
            <EmptyState
              title="Sin actividad reciente"
              text="Los movimientos, tiradas y cambios de tus campañas aparecerán aquí."
            />
          ) : (
            <List>
              {ACTIVIDAD.map((entry) => (
                <ListRow
                  key={entry.text}
                  title={entry.text}
                  meta={<span className="pv-row__meta-time">{entry.time}</span>}
                />
              ))}
            </List>
          )}
        </Block>

        <div className="f2-side">
          <Block title="Invitaciones">
            {vacio ? (
              <EmptyState title="Sin invitaciones" text="Las invitaciones que recibas aparecerán aquí." />
            ) : (
              <List>
                {INVITACIONES.map((entry) => (
                  <ListRow
                    key={entry.name}
                    title={entry.name}
                    subtitle={entry.from}
                    actions={
                      <>
                        <Button variant="primary">Aceptar</Button>
                        <Button variant="ghost">Rechazar</Button>
                      </>
                    }
                  />
                ))}
              </List>
            )}
          </Block>

          <Block title="Acceso rápido">
            <List>
              {ACCESO.map((entry) => (
                <ListRow
                  key={entry.label}
                  title={entry.label}
                  subtitle={entry.hint}
                  actions={<RowAction label={`Abrir ${entry.label}`} />}
                />
              ))}
            </List>
          </Block>
        </div>
      </div>
    </main>
  )
}

const CAMPANAS: Campana[] = [
  { name: 'La caída de Scaldar', cover: '🏰', description: 'Campaña principal: la ciudad portuaria bajo asedio.', state: 'Activa', role: 'Propietaria', members: '4 participantes', seen: 'hace 2 h' },
  { name: 'Crónicas del Mar Interior', description: 'Piratas, ruinas sumergidas y una tripulación rival.', state: 'Activa', role: 'Jugadora', members: '3 participantes', seen: 'hace 1 día' },
  { name: 'One-shot: la mina perdida', cover: '⛏', description: 'Aventura corta de intriga en las profundidades.', state: 'Activa', role: 'Propietaria', members: '5 participantes', seen: 'hace 3 días' },
  { name: 'El pacto de ceniza', description: 'Sesión zero: creación de personajes del sistema 2024.', state: 'En pausa', role: 'GM', members: '6 participantes', seen: 'hace 1 semana' },
  { name: 'Proyecto Ámbar', cover: '🧪', description: 'Pruebas de sistema y contenido homebrew.', state: 'Archivada', role: 'Propietaria', members: '2 participantes', seen: 'hace 2 meses' },
]

const CAMPANAS_EXTREMO: Campana[] = [
  { name: 'La épica campaña de las montañas de Scaldar y el asedio interminable de la ciudadela', description: 'Una historia larguísima que abarca tres reinos, dos inviernos y un dragón particularmente terco con la burocracia.', state: 'Activa', role: 'Propietaria', members: '6 participantes', seen: 'hace 10 min' },
  { name: 'Crónicas del Mar Interior', cover: '⛵', description: 'Piratas, ruinas sumergidas y una tripulación rival.', state: 'Activa', role: 'Jugadora', members: '3 participantes', seen: 'hace 1 día' },
  { name: 'One-shot: la mina perdida', cover: '⛏', description: 'Aventura corta de intriga en las profundidades.', state: 'Activa', role: 'Propietaria', members: '5 participantes', seen: 'hace 3 días' },
  { name: 'El pacto de ceniza', description: 'Sesión zero: creación de personajes del sistema 2024.', state: 'En pausa', role: 'GM', members: '6 participantes', seen: 'hace 1 semana' },
  { name: 'Proyecto Ámbar', cover: '🧪', description: 'Pruebas de sistema y contenido homebrew.', state: 'Archivada', role: 'Propietaria', members: '2 participantes', seen: 'hace 2 meses' },
  { name: 'La rebelión de los constructores de puentes', description: 'Sindicalismo fantástico en el valle del río Lento.', state: 'Activa', role: 'Jugadora', members: '4 participantes', seen: 'hace 4 h' },
  { name: 'Noche de radio: horrores de la frecuencia 13', cover: '📻', description: 'Terror contemporáneo en una emisora rural.', state: 'Activa', role: 'GM', members: '3 participantes', seen: 'hace 1 día' },
  { name: 'Academia de invocadores (curso 2024)', description: 'Campus, exámenes y demasiados demonios sueltos.', state: 'En pausa', role: 'Jugadora', members: '7 participantes', seen: 'hace 2 semanas' },
  { name: 'El tren de las 4:44', cover: '🚂', description: 'Viaje interminable entre estaciones imposibles.', state: 'Activa', role: 'Propietaria', members: '5 participantes', seen: 'hace 5 h' },
  { name: 'Cazadores del sol negro', description: 'Ciencia ficción nórdica y mutaciones solares.', state: 'Activa', role: 'GM', members: '4 participantes', seen: 'hace 6 días' },
  { name: 'Crónica del archivo perdido', description: 'Investigación, papel mohoso y secretos mejor guardados.', state: 'Archivada', role: 'Propietaria', members: '2 participantes', seen: 'hace 4 meses' },
  { name: 'Mercaderes de la última frontera', cover: '🐫', description: 'Caravanas, precios y política en el desierto.', state: 'Activa', role: 'Jugadora', members: '6 participantes', seen: 'hace 1 h' },
  { name: 'Bajo la ciudad de cristal', description: 'Intriga corta de dos sesiones en las cloacas iluminadas.', state: 'Activa', role: 'Propietaria', members: '3 participantes', seen: 'hace 3 semanas' },
  { name: 'La guerra de los pasteleros', cover: '🥧', description: 'Comedia absurda de dominio territorial culinario.', state: 'Archivada', role: 'GM', members: '5 participantes', seen: 'hace 5 meses' },
]

const FILTROS = ['Todas', 'Propias', 'Compartidas', 'Archivadas'] as const

/** PMB-2.3 — Campañas: localizar rápido entre muchas. */
export function CampanasPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const campanas = variant === 'extremo' ? CAMPANAS_EXTREMO : CAMPANAS
  const sinResultados = variant === 'sin-resultados'

  return (
    <main className="f2-area">
      <header className="f2-page-head">
        <h1 className="f2-page-title">Campañas</h1>
        <div className="f2-page-actions">
          <Button variant="secondary">Unirse con código</Button>
          <Button variant="primary">Crear campaña</Button>
        </div>
      </header>

      <div className="f2-toolbar">
        <input
          className="pv-input f2-search"
          placeholder="Buscar por nombre o descripción…"
          readOnly
          value={sinResultados ? 'mina' : undefined}
        />
        {FILTROS.map((filtro, index) => (
          <FilterChip key={filtro} label={filtro} selected={index === 0} />
        ))}
      </div>

      {variant === 'vacio' ? (
        // Sin CTA duplicado: la acción ya está en la cabecera (decisión 3).
        <EmptyState
          title="Aún no hay campañas"
          text="Crea tu primera campaña con «Crear campaña» o únete a una con un código de invitación."
        />
      ) : sinResultados ? (
        <EmptyState
          title="Sin resultados para «mina»"
          text="Prueba con otro término o revisa los filtros activos."
          action={<Button variant="secondary">Limpiar búsqueda</Button>}
        />
      ) : (
        <List>
          {campanas.map((entry) => (
            <ListRow
              key={entry.name}
              thumb={<Thumb name={entry.name} cover={entry.cover} />}
              title={entry.name}
              badge={<Badge tone={stateTone(entry.state)}>{entry.state}</Badge>}
              subtitle={entry.description}
              meta={
                <>
                  <span className="pv-row__meta-item pv-row__meta-item--drop-4">{entry.role}</span>
                  <span className="pv-row__meta-item pv-row__meta-item--drop-2">{entry.members}</span>
                  <span className="pv-row__meta-time">{entry.seen}</span>
                </>
              }
              actions={
                <RowAction
                  label={`${entry.state === 'Archivada' ? 'Ver' : 'Continuar'} ${entry.name}`}
                />
              }
            />
          ))}
        </List>
      )}
    </main>
  )
}
