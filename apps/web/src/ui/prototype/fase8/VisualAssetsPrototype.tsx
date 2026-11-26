import { PrototypeShell } from '../fase2/AreasFase2'
import elfIcon from './icons/woman-elf-face.svg'
import rogueIcon from './icons/rogue.svg'
import dragonIcon from './icons/dragon-head.svg'
import shieldIcon from './icons/riot-shield.svg'
import towerIcon from './icons/tarot-16-the-tower.svg'
import gearsIcon from './icons/gears.svg'
import houseIcon from './icons/house.svg'
import bookIcon from './icons/open-book.svg'
import gamepadIcon from './icons/gamepad.svg'
import cogIcon from './icons/cog.svg'
import chatIcon from './icons/chat-bubble.svg'
import familyIcon from './icons/family-tree.svg'
import markerIcon from './icons/position-marker.svg'
import calendarIcon from './icons/calendar.svg'
import combatIcon from './icons/attack-gauge.svg'
import walletIcon from './icons/wallet.svg'
import './visualAssets.css'

/**
 * Prototipo de mejora visual (PMB-8.6b, solo desarrollo): sustituye las
 * iniciales de las tarjetas de contenido por iconografía vectorial y lleva
 * iconos al raíl. Fuentes y licencias en `icons/LICENSES.md`.
 * NO está integrado en la app real: requiere aprobación visual.
 */

interface ContentCard {
  name: string
  type: string
  initial: string
  icon: string
}

const CARDS: ContentCard[] = [
  { name: 'Elfo', type: 'Raza', initial: 'E', icon: elfIcon },
  { name: 'Pícaro', type: 'Clase', initial: 'P', icon: rogueIcon },
  { name: 'Dragón rojo', type: 'Criatura', initial: 'D', icon: dragonIcon },
  { name: 'Guardia gris', type: 'Facción', initial: 'G', icon: shieldIcon },
  { name: 'Torre de Baldur', type: 'Localización', initial: 'T', icon: towerIcon },
  { name: 'El saqueo', type: 'Evento', initial: 'E', icon: calendarIcon },
]

const RAIL = [
  { label: 'Inicio', icon: houseIcon },
  { label: 'Campañas', icon: walletIcon },
  { label: 'Mesa de juego', icon: gamepadIcon },
  { label: 'Biblioteca', icon: bookIcon },
  { label: 'Gestión', icon: cogIcon },
]

const CATALOG = [
  ['Raza / elfo', elfIcon], ['Clase / pícaro', rogueIcon],
  ['Criatura / dragón', dragonIcon], ['Facción / escudo', shieldIcon],
  ['Localización / torre', towerIcon], ['Evento / calendario', calendarIcon],
  ['Ajustes', gearsIcon], ['Inicio', houseIcon], ['Biblioteca', bookIcon],
  ['Mesa', gamepadIcon], ['Chat', chatIcon], ['Relaciones', familyIcon],
  ['Mapa / anotación', markerIcon], ['Combate', combatIcon],
] as const

export function VisualAssetsPrototype() {
  return (
    <PrototypeShell active="biblioteca">
      <div className="visualproto">
        <header className="visualproto__head">
          <div>
            <h1 className="biblio__title">Mejora visual — iconografía</h1>
            <p className="biblio__subtitle">
              Iconos SVG de game-icons.net (CC BY 3.0, Lorc / Delapouite y colaboradores) · atribución en Créditos
            </p>
          </div>
        </header>

        <section className="visualproto__compare" aria-label="Comparación antes y después">
          <div className="visualproto__panel">
            <h2 className="biblio-zone__title">Antes — iniciales</h2>
            <div className="visualproto__grid">
              {CARDS.map((card) => (
                <article className="visualproto__card" key={`old-${card.name}`}>
                  <div className="visualproto__thumb visualproto__thumb--old" aria-hidden="true">
                    {card.initial}
                  </div>
                  <span className="visualproto__name">{card.name}</span>
                  <span className="visualproto__type">{card.type}</span>
                </article>
              ))}
            </div>
          </div>
          <div className="visualproto__panel visualproto__panel--new">
            <h2 className="biblio-zone__title">Después — iconos SVG</h2>
            <div className="visualproto__grid">
              {CARDS.map((card) => (
                <article className="visualproto__card" key={`new-${card.name}`}>
                  <div className="visualproto__thumb visualproto__thumb--new">
                    <img src={card.icon} alt="" />
                  </div>
                  <span className="visualproto__name">{card.name}</span>
                  <span className="visualproto__type">{card.type}</span>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="visualproto__rail-demo" aria-label="Raíl con iconos">
          <h2 className="biblio-zone__title">Raíl principal con iconos</h2>
          <div className="visualproto__rail">
            {RAIL.map((item) => (
              <span className="visualproto__rail-item" key={item.label}>
                <img src={item.icon} alt="" />
                {item.label}
              </span>
            ))}
          </div>
        </section>

        <section className="visualproto__catalog" aria-label="Catálogo de iconos">
          <h2 className="biblio-zone__title">Catálogo ({CATALOG.length})</h2>
          <div className="visualproto__catalog-grid">
            {CATALOG.map(([label, icon]) => (
              <div className="visualproto__catalog-item" key={label}>
                <img src={icon} alt="" />
                <span>{label}</span>
              </div>
            ))}
          </div>
        </section>
      </div>
    </PrototypeShell>
  )
}
