import { useState } from 'react'
import './experience.css'

/**
 * Prototipo interactivo EXP-1 (solo desarrollo): creación de personajes con
 * la imagen aportada por el usuario siempre visible a la derecha y elecciones
 * en tarjetas comprensibles. Datos de MUESTRA. Pendiente de aprobación visual.
 * La imagen se representa con un marcador honesto: el usuario aportará la suya.
 */

type StepId = 'identidad' | 'raza' | 'clase' | 'atributos' | 'presentacion'

const STEPS: Array<{ id: StepId; label: string; hint: string }> = [
  { id: 'identidad', label: 'Identidad', hint: 'Cómo se llama tu personaje' },
  { id: 'raza', label: 'Raza', hint: 'De dónde viene y qué aporta' },
  { id: 'clase', label: 'Clase', hint: 'Qué sabe hacer' },
  { id: 'atributos', label: 'Atributos', hint: 'Sus puntos fuertes' },
  { id: 'presentacion', label: 'Presentación', hint: 'Así queda tu personaje' },
]

interface Choice {
  id: string
  name: string
  icon: string
  tagline: string
  detail: string
  recommended?: string
}

const RACES: Choice[] = [
  {
    id: 'elfo', name: 'Elfo', icon: '/icons/woman-elf-face.svg',
    tagline: 'Ágil, perceptivo y longevo.',
    detail: 'Bonificación a Destreza. +5 pies de velocidad. Visión oscura y ventaja contra encantamientos.',
    recommended: 'Recomendado si quieres explorar, esquivar y fallar menos al percibir.',
  },
  {
    id: 'enano', name: 'Enano', icon: '/icons/dwarf-face.svg',
    tagline: 'Resistente y tenaz.',
    detail: 'Bonificación a Constitución. Resistencia al veneno y ventaja en salvaciones de sabiduría.',
  },
  {
    id: 'tiefling', name: 'Tiefling', icon: '/icons/warlock-hood.svg',
    tagline: 'Carismático y con un don oscuro.',
    detail: 'Bonificación a Carisma. Resistencia al fuego y un hechizo inherente.',
  },
]

const CLASSES: Choice[] = [
  {
    id: 'guerrero', name: 'Guerrero', icon: '/icons/broadsword.svg',
    tagline: 'Pelea cuerpo a cuerpo y aguanta golpes.',
    detail: 'Dado de golpe d10, competencia con armaduras y armas simples y marciales. Segundo viento.',
    recommended: 'Buena primera clase: sencilla, resistente y Perdona errores.',
  },
  {
    id: 'mago', name: 'Mago', icon: '/icons/wizard-face.svg',
    tagline: 'Lanza hechizos y controla el campo.',
    detail: 'Dado d6, conjuros de mago y elección de escuela. Requiere planificar recursos.',
  },
  {
    id: 'explorador', name: 'Explorador', icon: '/icons/bow-arrow.svg',
    tagline: 'Disparos, sigilo y supervivencia.',
    detail: 'Dado d10, arcos y espadas ligeras. Combate favorito y rastreo.',
  },
]

export function CreatorVisualPrototype() {
  const [step, setStep] = useState<StepId>('raza')
  const [race, setRace] = useState<Choice | null>(null)
  const [characterClass, setCharacterClass] = useState<Choice | null>(null)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [frameMode, setFrameMode] = useState(false)
  const [imageNotice, setImageNotice] = useState<string | null>(null)

  const index = STEPS.findIndex((entry) => entry.id === step)
  const choices = step === 'raza' ? RACES : step === 'clase' ? CLASSES : []
  const selected = step === 'raza' ? race : characterClass
  const pick = (choice: Choice) => {
    if (step === 'raza') setRace(choice)
    else setCharacterClass(choice)
  }

  return (
    <div className="exp">
      <header className="exp__top">
        <div className="exp__brand"><span className="exp__mark">✦</span> ProjectVTT</div>
        <div className="exp__breadcrumb">
          <strong>Crear personaje</strong>
          <span>Paso {index + 1} de {STEPS.length} · {STEPS[index].label}</span>
        </div>
        <div className="exp__top-actions">
          <button type="button" className="pv-btn pv-btn--ghost">Guardar borrador</button>
          <button type="button" className="pv-btn pv-btn--primary" onClick={() => setStep(STEPS[Math.min(index + 1, STEPS.length - 1)].id)}>
            Continuar →
          </button>
        </div>
      </header>

      <div className="exp__creator">
        <nav className="exp__steps" aria-label="Pasos de creación">
          {STEPS.map((entry, i) => (
            <button key={entry.id} type="button"
              className={`exp__step${entry.id === step ? ' is-active' : ''}${i < index ? ' is-done' : ''}`}
              onClick={() => setStep(entry.id)}>
              <span className="exp__step-num">{i < index ? '✓' : i + 1}</span>
              <span><strong>{entry.label}</strong><small>{entry.hint}</small></span>
            </button>
          ))}
          <div className="exp__summary">
            <span className="exp__label">TU PERSONAJE</span>
            <div><span>Raza</span><strong>{race?.name ?? 'Sin elegir'}</strong></div>
            <div><span>Clase</span><strong>{characterClass?.name ?? 'Sin elegir'}</strong></div>
            <div><span>Atributos</span><strong>Por repartir</strong></div>
          </div>
        </nav>

        <section className="exp__content" aria-label="Elecciones del personaje">
          <div className="exp__content-head">
            <h1>{step === 'raza' ? '¿De dónde viene tu personaje?' : step === 'clase' ? '¿Qué sabe hacer?' : STEPS[index].label}</h1>
            <p>
              {step === 'raza'
                ? 'Elige la que mejor encaje con la historia que imaginas. Puedes cambiarla y volver atrás cuando quieras.'
                : step === 'clase'
                  ? 'Piensa en qué te gustaría hacer en la partida: pelear, lanzar conjuros, explorar…'
                  : STEPS[index].hint}
            </p>
          </div>

          {choices.length > 0 ? (
            <div className="exp__choices">
              {choices.map((choice) => (
                <article key={choice.id}
                  className={`exp__choice${selected?.id === choice.id ? ' is-selected' : ''}`}>
                  <button type="button" className="exp__choice-main" onClick={() => pick(choice)}>
                    <span className="exp__choice-art"><img src={choice.icon} alt="" /></span>
                    <span className="exp__choice-copy">
                      <strong>{choice.name}</strong>
                      <span>{choice.tagline}</span>
                    </span>
                    {choice.recommended ? <span className="exp__recommended">Recomendado</span> : null}
                  </button>
                  <div className="exp__choice-foot">
                    <button type="button" onClick={() => setExpanded(expanded === choice.id ? null : choice.id)}>
                      {expanded === choice.id ? 'Ver menos' : '¿Qué aporta?'}
                    </button>
                    {selected?.id === choice.id ? <span className="exp__picked">Elegida ✓</span> : null}
                  </div>
                  {expanded === choice.id ? (
                    <div className="exp__choice-detail">
                      <p>{choice.detail}</p>
                      {choice.recommended ? <p className="exp__why">{choice.recommended}</p> : null}
                      <div className="exp__numbers">Velocidad 30 ft · Idiomas comunes · Rasgos de raza</div>
                    </div>
                  ) : null}
                </article>
              ))}
              <button type="button" className="exp__create-own">＋ Crear una opción propia (DM)</button>
            </div>
          ) : (
            <div className="exp__intro">
              <div className="exp__intro-card"><img src="/icons/dice.svg" alt="" /><strong>Tus decisiones, a tu ritmo</strong><span>Pocas opciones a la vez, con explicaciones sencillas.</span></div>
              <div className="exp__intro-card"><img src="/icons/position-marker.svg" alt="" /><strong>Tu imagen siempre cerca</strong><span>La foto que aportes te acompaña durante todo el recorrido.</span></div>
              <button type="button" className="pv-btn pv-btn--primary" onClick={() => setStep('raza')}>Empezar por la raza →</button>
            </div>
          )}
        </section>

        <aside className="exp__portrait" aria-label="Imagen del personaje">
          <span className="exp__label">TU PERSONAJE</span>
          <div className={`exp__portrait-frame${frameMode ? ' is-framing' : ''}`}>
            <img src="/icons/rogue.svg" alt="" className="exp__portrait-silhouette" />
            <span className="exp__portrait-caption">Imagen de muestra · el jugador aporta la suya</span>
            {frameMode ? <div className="exp__crop-grid" aria-hidden="true" /> : null}
          </div>
          <div className="exp__portrait-actions">
            <button type="button" className="pv-btn pv-btn--primary" onClick={() => setImageNotice('Subir imagen (demo)')}>Subir imagen</button>
            <button type="button" className="pv-btn pv-btn--ghost" onClick={() => setImageNotice('Imagen reemplazada (demo)')}>Reemplazar</button>
            <button type="button" className="pv-btn pv-btn--ghost" onClick={() => setFrameMode((current) => !current)}>
              {frameMode ? 'Listo' : 'Ajustar encuadre'}
            </button>
          </div>
          <p className="exp__notice" role="status">{imageNotice ?? 'Puedes cambiar la imagen sin perder lo que ya has elegido.'}</p>
          <div className="exp__live">
            <span className="exp__label">RESUMEN EN VIVO</span>
            <p>{race ? `${race.name} · ${race.tagline}` : 'Elige una raza para ver su aportación.'}</p>
            <p>{characterClass ? characterClass.tagline : 'Aún sin clase.'}</p>
          </div>
        </aside>
      </div>
    </div>
  )
}
