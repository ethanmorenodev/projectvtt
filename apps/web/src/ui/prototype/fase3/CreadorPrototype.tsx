import { useState } from 'react'
import type { ReactNode } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import './creador.css'

/**
 * Prototipo del creador de personajes (PMB-3.4) — solo desarrollo
 * (`#prototipo-creador[-vacio|-extremo]`), montado en el shell real.
 * Sigue el **mapa de pasos aprobado** (docs/visual/screens/pmb-3.1-
 * creador-flujo.md): asistente genérico por pasos con efectos visibles,
 * atributos manual + compra de puntos (decisión 3) y equipo inicial
 * (decisión 4). Datos de muestra del SRD: el creador real consumirá el
 * contenido publicado en la campaña y validará en servidor
 * (`POST /characters/build/preview`).
 */

type Variant = 'normal' | 'vacio' | 'extremo'

interface Race {
  key: string
  name: string
  bonuses: string
  speed: number
  languages: string
  traits: string[]
  variants: Array<{ key: string; name: string; effect: string }>
}

interface ClassInfo {
  key: string
  name: string
  hitDie: number
  saves: string
  skills: number
  summary: string
}

interface Background {
  key: string
  name: string
  skills: string
  feature: string
}

const RACES: Race[] = [
  {
    key: 'dwarf',
    name: 'Dwarf',
    bonuses: 'Con +2',
    speed: 25,
    languages: 'Common, Dwarvish',
    traits: ['Darkvision (60 ft)', 'Dwarven Resilience', 'Stonecunning'],
    variants: [
      { key: 'hill', name: 'Hill Dwarf', effect: 'Wis +1 · Dwarven Toughness' },
      { key: 'mountain', name: 'Mountain Dwarf', effect: 'Str +2 · Dwarven Armor Training' },
    ],
  },
  {
    key: 'elf',
    name: 'Elf',
    bonuses: 'Dex +2',
    speed: 30,
    languages: 'Common, Elvish',
    traits: ['Darkvision (60 ft)', 'Fey Ancestry', 'Trance'],
    variants: [
      { key: 'high', name: 'High Elf', effect: 'Int +1 · Cantrip' },
      { key: 'wood', name: 'Wood Elf', effect: 'Wis +1 · Speed 35 ft' },
      { key: 'drow', name: 'Drow', effect: 'Cha +1 · Superior Darkvision (120 ft)' },
    ],
  },
  {
    key: 'halfling',
    name: 'Halfling',
    bonuses: 'Dex +2',
    speed: 25,
    languages: 'Common, Halfling',
    traits: ['Lucky', 'Brave', 'Halfling Nimbleness'],
    variants: [
      { key: 'lightfoot', name: 'Lightfoot', effect: 'Cha +1 · Naturally Stealthy' },
      { key: 'stout', name: 'Stout', effect: 'Con +1 · Stout Resilience' },
    ],
  },
  {
    key: 'human',
    name: 'Human',
    bonuses: 'All abilities +1',
    speed: 30,
    languages: 'Common + 1 extra',
    traits: [],
    variants: [],
  },
  {
    key: 'tiefling',
    name: 'Tiefling',
    bonuses: 'Cha +2, Int +1',
    speed: 30,
    languages: 'Common, Infernal',
    traits: ['Darkvision (60 ft)', 'Hellish Resistance', 'Infernal Legacy'],
    variants: [],
  },
]

const CLASSES: ClassInfo[] = [
  { key: 'rogue', name: 'Rogue', hitDie: 8, saves: 'Dex, Int', skills: 4, summary: 'Stealth, trickery and skill.' },
  { key: 'fighter', name: 'Fighter', hitDie: 10, saves: 'Str, Con', skills: 2, summary: 'Master of martial combat.' },
  { key: 'wizard', name: 'Wizard', hitDie: 6, saves: 'Int, Wis', skills: 2, summary: 'Scholarly magic-user.' },
  { key: 'bard', name: 'Bard', hitDie: 8, saves: 'Dex, Cha', skills: 3, summary: 'Voice that weaves magic.' },
  { key: 'cleric', name: 'Cleric', hitDie: 8, saves: 'Wis, Cha', skills: 2, summary: 'Priestly champion.' },
]

const BACKGROUNDS: Background[] = [
  { key: 'acolyte', name: 'Acolyte', skills: 'Insight, Religion', feature: 'Shelter of the Faithful' },
  { key: 'criminal', name: 'Criminal', skills: 'Deception, Stealth', feature: 'Criminal Contact' },
  { key: 'folk-hero', name: 'Folk Hero', skills: 'Animal Handling, Survival', feature: 'Rustic Hospitality' },
  { key: 'sage', name: 'Sage', skills: 'Arcana, History', feature: 'Researcher' },
]

const SKILLS = [
  'Acrobatics', 'Animal Handling', 'Arcana', 'Athletics', 'Deception', 'History',
  'Insight', 'Intimidation', 'Investigation', 'Medicine', 'Nature', 'Perception',
  'Performance', 'Persuasion', 'Religion', 'Sleight of Hand', 'Stealth', 'Survival',
]

const ABILITIES = ['Strength', 'Dexterity', 'Constitution', 'Intelligence', 'Wisdom', 'Charisma']
const POINT_BUY_COST: Record<number, number> = {
  8: 0, 9: 1, 10: 2, 11: 3, 12: 4, 13: 5, 14: 7, 15: 9,
}

const STEPS = [
  'Sistema', 'Identidad', 'Raza y variante', 'Clase', 'Trasfondo',
  'Atributos', 'Habilidades y rasgos', 'Equipo inicial', 'Historia e imagen', 'Revisión',
]

function modifier(score: number): number {
  return Math.floor((score - 10) / 2)
}

function modText(score: number): string {
  const mod = modifier(score)
  return mod >= 0 ? `+${mod}` : `${mod}`
}

export function CreadorPrototype({ variant = 'normal' }: { variant?: Variant }) {
  const [step, setStep] = useState(0)
  const [name, setName] = useState(variant === 'vacio' ? '' : 'Elenar')
  const [raceKey, setRaceKey] = useState<string | null>(variant === 'vacio' ? null : 'dwarf')
  const [variantKey, setVariantKey] = useState<string | null>(variant === 'vacio' ? null : 'mountain')
  const [classKey, setClassKey] = useState<string | null>(variant === 'vacio' ? null : 'rogue')
  const [backgroundKey, setBackgroundKey] = useState<string | null>(variant === 'vacio' ? null : 'criminal')
  const [method, setMethod] = useState<'pointBuy' | 'manual'>('pointBuy')
  const [scores, setScores] = useState<Record<string, number>>(
    Object.fromEntries(ABILITIES.map((a) => [a, 8])),
  )
  const [skills, setSkills] = useState<string[]>(
    variant === 'vacio' ? [] : ['Stealth', 'Perception', 'Athletics', 'Deception'],
  )

  const race = RACES.find((r) => r.key === raceKey) ?? null
  const chosenVariant = race?.variants.find((v) => v.key === variantKey) ?? null
  const classInfo = CLASSES.find((c) => c.key === classKey) ?? null
  const background = BACKGROUNDS.find((b) => b.key === backgroundKey) ?? null
  const long = variant === 'extremo'

  const spent = ABILITIES.reduce((total, a) => total + (POINT_BUY_COST[scores[a]] ?? 0), 0)
  const nameShown = long
    ? 'Guardia de élite de la ciudadela de Scaldar con armadura ceremonial'
    : name

  function bump(ability: string, delta: number) {
    setScores((current) => {
      const next = Math.min(15, Math.max(8, current[ability] + delta))
      return { ...current, [ability]: next }
    })
  }

  function toggleSkill(skill: string) {
    setSkills((current) =>
      current.includes(skill)
        ? current.filter((s) => s !== skill)
        : [...current, skill],
    )
  }

  const stepBody = (() => {
    switch (step) {
      case 0:
        return (
          <Section title="Elige el sistema de juego">
            <ChoiceCard selected title="D&D 5e (SRD 5.1)" meta="Único sistema de la campaña" />
            <Hint>Si la campaña tuviera varios sistemas, aparecerían aquí.</Hint>
          </Section>
        )
      case 1:
        return (
          <Section title="Identidad">
            <label className="creador-field">
              Nombre del personaje
              <input
                className="pv-input"
                value={name}
                maxLength={100}
                placeholder="Nombre"
                onChange={(event) => setName(event.target.value)}
              />
            </label>
            <div className="creador-portrait" aria-hidden="true">Retrato</div>
            <label className="creador-field">
              Descripción
              <textarea className="pv-input" rows={3} placeholder="Cómo es…" />
            </label>
            <label className="creador-field">
              Personalidad
              <textarea className="pv-input" rows={3} placeholder="Cómo actúa…" />
            </label>
          </Section>
        )
      case 2:
        return (
          <Section title="Raza y variante">
            <div className="creador-grid">
              {RACES.map((entry) => (
                <ChoiceCard
                  key={entry.key}
                  selected={raceKey === entry.key}
                  title={long ? `${entry.name} — linaje extendido con rasgos heredados de antepasados legendarios` : entry.name}
                  meta={`${entry.bonuses} · ${entry.speed} ft · ${entry.languages}`}
                  onClick={() => {
                    setRaceKey(entry.key)
                    setVariantKey(entry.variants[0]?.key ?? null)
                  }}
                />
              ))}
            </div>
            {race && race.variants.length > 0 ? (
              <div className="creador-chips">
                <span className="creador-chips__label">Variante:</span>
                {race.variants.map((entry) => (
                  <button
                    key={entry.key}
                    type="button"
                    className={`creador-chip${variantKey === entry.key ? ' creador-chip--on' : ''}`}
                    onClick={() => setVariantKey(entry.key)}
                  >
                    {entry.name}
                  </button>
                ))}
              </div>
            ) : null}
            {race ? (
              <Effects title="Efectos de la elección">
                <li>{race.bonuses} {chosenVariant ? `· ${chosenVariant.effect}` : ''}</li>
                <li>Velocidad {race.speed} ft · Tamaño Medium</li>
                <li>Idiomas: {race.languages}</li>
                {race.traits.map((trait) => <li key={trait}>{trait}</li>)}
              </Effects>
            ) : (
              <Empty text="Elige una raza para ver sus efectos." />
            )}
          </Section>
        )
      case 3:
        return (
          <Section title="Clase">
            <div className="creador-grid">
              {CLASSES.map((entry) => (
                <ChoiceCard
                  key={entry.key}
                  selected={classKey === entry.key}
                  title={entry.name}
                  meta={`d${entry.hitDie} · Salvaciones: ${entry.saves} · ${entry.skills} habilidades`}
                  text={entry.summary}
                  onClick={() => setClassKey(entry.key)}
                />
              ))}
            </div>
            {classInfo ? (
              <Effects title="Efectos de la elección">
                <li>Dado de golpe d{classInfo.hitDie}</li>
                <li>Tiradas de salvación: {classInfo.saves}</li>
                <li>Elige {classInfo.skills} habilidades (paso «Habilidades y rasgos»)</li>
              </Effects>
            ) : (
              <Empty text="Elige una clase para ver sus efectos." />
            )}
          </Section>
        )
      case 4:
        return (
          <Section title="Trasfondo">
            <List>
              {BACKGROUNDS.map((entry) => (
                <ListRow
                  key={entry.key}
                  selected={backgroundKey === entry.key}
                  title={entry.name}
                  meta={`${entry.skills} · ${entry.feature}`}
                  onClick={() => setBackgroundKey(entry.key)}
                />
              ))}
            </List>
            {background ? (
              <Effects title="Efectos de la elección">
                <li>Competencias: {background.skills}</li>
                <li>Rasgo: {background.feature}</li>
              </Effects>
            ) : (
              <Empty text="Elige un trasfondo para ver sus efectos." />
            )}
          </Section>
        )
      case 5:
        return (
          <Section title="Atributos">
            <div className="creador-segmented" role="group" aria-label="Método">
              <button
                type="button"
                className={`creador-chip${method === 'pointBuy' ? ' creador-chip--on' : ''}`}
                onClick={() => setMethod('pointBuy')}
              >
                Compra de puntos
              </button>
              <button
                type="button"
                className={`creador-chip${method === 'manual' ? ' creador-chip--on' : ''}`}
                onClick={() => setMethod('manual')}
              >
                Manual
              </button>
            </div>
            {method === 'pointBuy' ? (
              <>
                <p className="creador-budget">
                  Presupuesto: <strong>{spent}</strong>/27 puntos
                  {spent > 27 ? ' — superado' : ''}
                </p>
                <div className="creador-abilities">
                  {ABILITIES.map((ability) => (
                    <div key={ability} className="creador-ability">
                      <span>{ability}</span>
                      <button type="button" aria-label={`Restar ${ability}`} onClick={() => bump(ability, -1)}>−</button>
                      <strong>{scores[ability]}</strong>
                      <button type="button" aria-label={`Sumar ${ability}`} onClick={() => bump(ability, 1)}>+</button>
                      <span className="creador-ability__cost">{POINT_BUY_COST[scores[ability]]} pts</span>
                      <span className="creador-ability__mod">{modText(scores[ability])}</span>
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <div className="creador-abilities">
                {ABILITIES.map((ability) => (
                  <div key={ability} className="creador-ability">
                    <span>{ability}</span>
                    <input
                      className="pv-input"
                      type="number"
                      min={3}
                      max={20}
                      value={scores[ability]}
                      aria-label={ability}
                      onChange={(event) =>
                        setScores((current) => ({
                          ...current,
                          [ability]: Math.min(20, Math.max(3, Number(event.target.value) || 3)),
                        }))
                      }
                    />
                    <span className="creador-ability__mod">{modText(scores[ability])}</span>
                  </div>
                ))}
              </div>
            )}
            <Hint>El servidor valida rangos y presupuesto (reglas solo en servidor).</Hint>
          </Section>
        )
      case 6:
        return (
          <Section title={`Habilidades y rasgos — elige ${classInfo?.skills ?? 0}`}>
            <div className="creador-chips">
              {SKILLS.map((skill) => (
                <button
                  key={skill}
                  type="button"
                  className={`creador-chip${skills.includes(skill) ? ' creador-chip--on' : ''}`}
                  onClick={() => toggleSkill(skill)}
                >
                  {skill}
                </button>
              ))}
            </div>
            <Hint>
              Elegidas {skills.length} de {classInfo?.skills ?? 0}: {skills.join(', ') || 'ninguna'}.
            </Hint>
          </Section>
        )
      case 7:
        return (
          <Section title="Equipo inicial">
            <List>
              <ListRow title="Leather armor" meta="Según clase" />
              <ListRow title="Two daggers" meta="Según clase" />
              <ListRow title="Thieves' tools" meta="Según clase (Rogue)" />
            </List>
            <Hint>Las reglas de equipo por sistema llegan con el contenido de objetos (P.4).</Hint>
          </Section>
        )
      case 8:
        return (
          <Section title="Historia e imagen">
            <label className="creador-field">
              Historia
              <textarea className="pv-input" rows={5} placeholder="De dónde viene…" />
            </label>
            <div className="creador-portrait" aria-hidden="true">Imagen</div>
            <Hint>Paso saltable: puedes completarlo después desde la ficha.</Hint>
          </Section>
        )
      default:
        return (
          <Section title="Revisión — resumen de decisiones">
            <div className="creador-review">
              <Effects title="Identidad">
                <li>{nameShown || 'Sin nombre'}</li>
                <li>Nivel 1</li>
              </Effects>
              <Effects title="Raza">
                <li>{race ? `${race.name}${chosenVariant ? ` (${chosenVariant.name})` : ''}` : 'Sin elegir'}</li>
                {race ? <li>{race.bonuses} {chosenVariant ? `· ${chosenVariant.effect}` : ''}</li> : null}
                {race ? <li>Velocidad {race.speed} ft · {race.languages}</li> : null}
              </Effects>
              <Effects title="Clase y trasfondo">
                <li>{classInfo ? `${classInfo.name} (d${classInfo.hitDie})` : 'Sin clase'}</li>
                <li>{background ? `${background.name} — ${background.feature}` : 'Sin trasfondo'}</li>
                <li>Habilidades: {skills.join(', ') || 'ninguna'}</li>
              </Effects>
              <Effects title="Atributos">
                {ABILITIES.map((ability) => (
                  <li key={ability}>{ability} {scores[ability]} ({modText(scores[ability])})</li>
                ))}
              </Effects>
            </div>
            <div className="creador-actions">
              <button type="button" className="pv-btn pv-btn--primary">Crear personaje</button>
            </div>
          </Section>
        )
    }
  })()

  return (
    <PrototypeShell active="campana">
      <main className="creador">
        <header className="creador__head">
          <h1 className="creador__title">Crear personaje — La caída de Scaldar</h1>
          <span className="creador__draft">Borrador guardado</span>
          <span className="creador__step">Paso {step + 1} de {STEPS.length}</span>
        </header>
        <div className="creador__body">
          <nav className="proto-subnav" aria-label="Pasos del creador">
            {STEPS.map((label, index) => (
              <button
                key={label}
                type="button"
                className={`proto-subnav__item${index === step ? ' proto-subnav__item--active' : ''}`}
                onClick={() => setStep(index)}
              >
                {index < step ? '✓ ' : ''}{label}
              </button>
            ))}
          </nav>
          <div className="creador__content">
            {stepBody}
            <div className="creador__nav">
              <button
                type="button"
                className="pv-btn pv-btn--secondary"
                disabled={step === 0}
                onClick={() => setStep((current) => Math.max(0, current - 1))}
              >
                Atrás
              </button>
              <button type="button" className="pv-btn pv-btn--ghost">Guardar y salir</button>
              <button
                type="button"
                className="pv-btn pv-btn--primary"
                disabled={step === STEPS.length - 1}
                onClick={() => setStep((current) => Math.min(STEPS.length - 1, current + 1))}
              >
                Siguiente
              </button>
            </div>
          </div>
        </div>
      </main>
    </PrototypeShell>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="creador-section">
      <h2 className="creador-section__title">{title}</h2>
      {children}
    </section>
  )
}

function ChoiceCard({
  selected,
  title,
  meta,
  text,
  onClick,
}: {
  selected?: boolean
  title: string
  meta?: string
  text?: string
  onClick?: () => void
}) {
  return (
    <button
      type="button"
      className={`creador-card${selected ? ' creador-card--on' : ''}`}
      aria-pressed={Boolean(selected)}
      onClick={onClick}
    >
      <strong>{title}</strong>
      {meta ? <span>{meta}</span> : null}
      {text ? <span>{text}</span> : null}
    </button>
  )
}

function Effects({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="creador-effects">
      <h3 className="creador-effects__title">{title}</h3>
      <ul>{children}</ul>
    </div>
  )
}

function List({ children }: { children: ReactNode }) {
  return <ul className="creador-list">{children}</ul>
}

function ListRow({
  selected,
  title,
  meta,
  onClick,
}: {
  selected?: boolean
  title: string
  meta?: string
  onClick?: () => void
}) {
  return (
    <li>
      <button
        type="button"
        className={`creador-list__row${selected ? ' creador-list__row--on' : ''}`}
        aria-pressed={Boolean(selected)}
        onClick={onClick}
      >
        <strong>{title}</strong>
        {meta ? <span>{meta}</span> : null}
      </button>
    </li>
  )
}

function Empty({ text }: { text: string }) {
  return <p className="creador-empty">{text}</p>
}

function Hint({ children }: { children: ReactNode }) {
  return <p className="creador-hint">{children}</p>
}
