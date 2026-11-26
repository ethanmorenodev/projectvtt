import type { ReactNode } from 'react'

/**
 * Contenido de las áreas del prototipo de navegación (PMB-1.3).
 * Datos de ejemplo estáticos: es un prototipo visual, sin lógica de
 * negocio ni datos reales (proceso visual obligatorio, docs/visual).
 */

function Thumb({ label }: { label: string }) {
  return <div className="proto-thumb" aria-hidden="true">{label}</div>
}

function SectionTitle({ children }: { children: ReactNode }) {
  return <h3 className="proto-section-title">{children}</h3>
}

export function InicioArea() {
  return (
    <div className="proto-area">
      <h2 className="proto-area__title">Inicio</h2>
      <SectionTitle>Continuar jugando</SectionTitle>
      <div className="proto-card-grid">
        {[
          ['La caída de Scaldar', 'GM · hace 2 h · 4 jugadores'],
          ['Crónicas del Mar Interior', 'Jugadora · ayer · 3 jugadores'],
          ['One-shot: la mina perdida', 'GM · hace 3 días'],
        ].map(([title, meta]) => (
          <div className="proto-card" key={title}>
            <Thumb label="🏰" />
            <div>
              <div className="proto-card__title">{title}</div>
              <div className="proto-card__meta">{meta}</div>
            </div>
          </div>
        ))}
      </div>
      <SectionTitle>Invitaciones</SectionTitle>
      <div className="proto-row-list">
        <div className="proto-row"><span>Invitación de Marcos a «El pacto de ceniza»</span><em>Aceptar</em></div>
      </div>
      <SectionTitle>Actividad reciente</SectionTitle>
      <div className="proto-row-list">
        <div className="proto-row"><span>Ana movió «Guardia del puerto» en Escena del puerto</span><em>hace 12 min</em></div>
        <div className="proto-row"><span>Luis publicó «Elfos del bosque (variante)» en la biblioteca</span><em>hace 1 h</em></div>
        <div className="proto-row"><span>Nueva versión de «Espada larga» disponible</span><em>ayer</em></div>
      </div>
    </div>
  )
}

export function CampanasArea() {
  return (
    <div className="proto-area">
      <h2 className="proto-area__title">Campañas</h2>
      <div className="proto-toolbar">
        <input className="pv-input proto-search" placeholder="Buscar campañas…" readOnly />
        <span className="proto-chip">Todas</span>
        <span className="proto-chip proto-chip--active">Propias</span>
        <span className="proto-chip">Compartidas</span>
        <span className="proto-chip">Archivadas</span>
      </div>
      <div className="proto-card-grid">
        {[
          ['La caída de Scaldar', 'Propietaria', '4 participantes', 'hace 2 h'],
          ['Crónicas del Mar Interior', 'Jugadora', '3 participantes', 'ayer'],
          ['One-shot: la mina perdida', 'Propietaria', '5 participantes', 'hace 3 días'],
          ['El pacto de ceniza', 'GM', '6 participantes', 'hace 1 semana'],
          ['Proyecto Ámbar (archivada)', 'Propietaria', '2 participantes', 'hace 2 meses'],
        ].map(([title, role, members, seen]) => (
          <div className="proto-card" key={title}>
            <Thumb label="🗺" />
            <div>
              <div className="proto-card__title">{title}</div>
              <div className="proto-card__meta">
                <span>{role}</span>
                <span>{members}</span>
                <span>{seen}</span>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

const campaignSections = [
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
]

export function CampanaArea() {
  return (
    <div className="proto-split">
      <nav className="proto-subnav">
        <div className="proto-subnav__title">La caída de Scaldar</div>
        {campaignSections.map((section, index) => (
          <div key={section} className={`proto-subnav__item${index === 0 ? ' proto-subnav__item--active' : ''}`}>
            {section}
          </div>
        ))}
      </nav>
      <div className="proto-area">
        <h2 className="proto-area__title">Resumen</h2>
        <div className="proto-stat-row">
          <div className="proto-stat"><strong>6</strong><span>escenas</span></div>
          <div className="proto-stat"><strong>12</strong><span>personajes</span></div>
          <div className="proto-stat"><strong>4</strong><span>jugadores</span></div>
          <div className="proto-stat"><strong>38</strong><span>elementos de biblioteca</span></div>
        </div>
        <SectionTitle>Última sesión</SectionTitle>
        <div className="proto-row-list">
          <div className="proto-row"><span>Combate en el puerto — ronda 3</span><em>continuar en la mesa</em></div>
          <div className="proto-row"><span>5 movimientos de tokens, 2 tiradas, 1 hechizo</span><em>ayer</em></div>
        </div>
        <SectionTitle>Participantes</SectionTitle>
        <div className="proto-row-list">
          <div className="proto-row"><span>Ana — Propietaria / GM</span><em>Gestionar</em></div>
          <div className="proto-row"><span>Luis — Jugador</span><em>Gestionar</em></div>
          <div className="proto-row"><span>Marcos — Jugador</span><em>Gestionar</em></div>
        </div>
      </div>
    </div>
  )
}

export function MesaArea({ children }: { children: ReactNode }) {
  return (
    <div className="proto-mesa">
      <div className="proto-mesa__hint">
        Mesa de juego — el tablero ocupa el fondo; las herramientas viven en el sistema de ventanas (prueba de PMB-1.5).
      </div>
      {children}
    </div>
  )
}

export function BibliotecaArea() {
  const items = [
    ['⚔', 'Espada larga'], ['🛡', 'Escudo de torre'], ['🧪', 'Poción de curación'],
    ['📜', 'Hechizo: Bola de fuego'], ['🧝', 'Alta elfa'], ['💀', 'Ghoul (variante)'],
    ['🏔', 'Montañas de Scaldar'], ['👁', 'Condición: cegado'],
  ] as const
  return (
    <div className="proto-split">
      <nav className="proto-subnav">
        <div className="proto-subnav__title">Tipos</div>
        {['Razas', 'Clases', 'Trasfondos', 'Hechizos', 'Objetos', 'Armas', 'Armaduras', 'Criaturas', 'Condiciones'].map(
          (type, index) => (
            <div key={type} className={`proto-subnav__item${index === 4 ? ' proto-subnav__item--active' : ''}`}>
              {type}
            </div>
          ),
        )}
      </nav>
      <div className="proto-area">
        <div className="proto-toolbar">
          <input className="pv-input proto-search" placeholder="Buscar en la biblioteca…" readOnly />
          <span className="proto-chip proto-chip--active">Miniaturas</span>
          <span className="proto-chip">Iconos</span>
          <span className="proto-chip">Ilustraciones</span>
          <span className="proto-chip">Licencia: todas</span>
        </div>
        <div className="proto-thumb-grid">
          {items.map(([icon, name]) => (
            <div className="proto-thumb-card" key={name}>
              <Thumb label={icon} />
              <span>{name}</span>
            </div>
          ))}
        </div>
      </div>
      <aside className="proto-inspector">
        <div className="proto-subnav__title">Inspector</div>
        <Thumb label="⚔" />
        <div className="proto-card__title">Espada larga</div>
        <div className="proto-card__meta"><span>Arma · Marcial</span></div>
        <div className="proto-card__meta"><span>Rareza: común</span><span>1d8 cortante</span></div>
        <div className="proto-note">Imagen: miniatura 128 · procedencia: paquete dnd5e-srd-5.1 · licencia CC BY 4.0</div>
      </aside>
    </div>
  )
}

export function GestionArea() {
  return (
    <div className="proto-area">
      <h2 className="proto-area__title">Gestión</h2>
      <div className="proto-row-list">
        <div className="proto-row"><span>Cuenta y perfil</span><em>Abrir</em></div>
        <div className="proto-row"><span>Preferencias de la aplicación (idioma, densidad, movimiento)</span><em>Abrir</em></div>
        <div className="proto-row"><span>Mis mundos</span><em>Abrir</em></div>
        <div className="proto-row"><span>Créditos y licencias</span><em>Abrir</em></div>
        <div className="proto-row"><span>Importar/exportar contenido</span><em>Abrir</em></div>
      </div>
    </div>
  )
}
