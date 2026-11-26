import { useState } from 'react'
import './experience.css'

/**
 * Prototipo interactivo EXP-3 (solo desarrollo): creador guiado de clases
 * homebrew para DM. Datos de MUESTRA. Sin programar ni editar JSON.
 * Pendiente de aprobación visual antes de integrar.
 */

type StepId = 'origen' | 'identidad' | 'atributos' | 'equipo' | 'habilidades' | 'progresion' | 'publicar'

const STEPS: Array<{ id: StepId; label: string; hint: string }> = [
  { id: 'origen', label: 'Origen', hint: 'Desde cero o duplicar' },
  { id: 'identidad', label: 'Identidad', hint: 'Nombre, imagen y concepto' },
  { id: 'atributos', label: 'Atributos y recursos', hint: 'Qué define a la clase' },
  { id: 'equipo', label: 'Equipo inicial', hint: 'Con qué empieza' },
  { id: 'habilidades', label: 'Habilidades', hint: 'Qué puede hacer' },
  { id: 'progresion', label: 'Progresión', hint: 'Niveles y especializaciones' },
  { id: 'publicar', label: 'Revisar y publicar', hint: 'Dónde estará disponible' },
]

const BASES = ['Guerrero', 'Mago', 'Explorador', 'Ninguna — desde cero']
const ABILITIES = [
  { name: 'Segundo viento', mode: 'Automático', icon: '/icons/attack-gauge.svg' },
  { name: 'Ojo de águila', mode: 'Resolución manual', icon: '/icons/gaze.svg' },
]
const ITEMS = ['Espada larga', 'Arco corto', 'Cota de malla']

export function HomebrewClassPrototype() {
  const [step, setStep] = useState<StepId>('origen')
  const [origin, setOrigin] = useState('Ninguna — desde cero')
  const [name, setName] = useState('Guardián del Umbral')
  const [abilities, setAbilities] = useState(ABILITIES)
  const [mode, setMode] = useState<'automático' | 'manual'>('automático')
  const [draftSaved, setDraftSaved] = useState(false)
  const [published, setPublished] = useState(false)
  const index = STEPS.findIndex((entry) => entry.id === step)

  return (
    <div className="exp">
      <header className="exp__top">
        <div className="exp__brand"><span className="exp__mark">✦</span> ProjectVTT</div>
        <div className="exp__breadcrumb"><strong>Crear clase · Solo DM</strong><span>Paso {index + 1} de {STEPS.length} · {STEPS[index].label}</span></div>
        <div className="exp__top-actions">
          <button type="button" className="pv-btn pv-btn--ghost" onClick={() => setDraftSaved(true)}>
            {draftSaved ? 'Borrador guardado ✓' : 'Guardar borrador'}
          </button>
          <button type="button" className="pv-btn pv-btn--ghost" onClick={() => setStep('publicar')}>Previsualizar</button>
          <button type="button" className="pv-btn pv-btn--primary" onClick={() => setStep(STEPS[Math.min(index + 1, STEPS.length - 1)].id)}>
            Continuar →
          </button>
        </div>
      </header>

      <div className="exp__creator">
        <nav className="exp__steps" aria-label="Pasos de la clase">
          {STEPS.map((entry, i) => (
            <button key={entry.id} type="button"
              className={`exp__step${entry.id === step ? ' is-active' : ''}${i < index ? ' is-done' : ''}`}
              onClick={() => setStep(entry.id)}>
              <span className="exp__step-num">{i < index ? '✓' : i + 1}</span>
              <span><strong>{entry.label}</strong><small>{entry.hint}</small></span>
            </button>
          ))}
          <div className="exp__summary">
            <span className="exp__label">TU CLASE</span>
            <div><span>Nombre</span><strong>{name || 'Sin nombre'}</strong></div>
            <div><span>Origen</span><strong>{origin.split(' — ')[0]}</strong></div>
            <div><span>Habilidades</span><strong>{abilities.length}</strong></div>
          </div>
        </nav>

        <section className="exp__content" aria-label="Creador de clase">
          <div className="exp__content-head">
            <h1>{STEPS[index].label}</h1>
            <p>
              {step === 'origen'
                ? 'Empieza de cero o toma una clase existente como base. Podrás cambiar todo después.'
                : step === 'publicar'
                  ? 'Revisa cómo se verá para los jugadores y elige dónde estará disponible.'
                  : STEPS[index].hint}
            </p>
          </div>

          {step === 'origen' ? (
            <div className="exp__choices">
              <div className="exp__panel" role="group" aria-label="Clase base">
                {BASES.map((base) => (
                  <button key={base} type="button"
                    className={`exp__base${origin === base ? ' is-selected' : ''}`}
                    onClick={() => setOrigin(base)}>
                    <img src={base === 'Mago' ? '/icons/wizard-face.svg' : base === 'Explorador' ? '/icons/bow-arrow.svg' : base === 'Guerrero' ? '/icons/broadsword.svg' : '/icons/dice.svg'} alt="" />
                    <span><strong>{base}</strong><small>{base === 'Ninguna — desde cero' ? 'Todo se define a mano' : 'Copia y ajusta sus valores'}</small></span>
                  </button>
                ))}
              </div>
            </div>
          ) : null}

          {step === 'identidad' ? (
            <div className="exp__panel exp__form">
              <label className="exp__field">Nombre<input value={name} onChange={(event) => setName(event.target.value)} /></label>
              <label className="exp__field">Concepto<textarea defaultValue="Un guardián que sella brechas entre mundos." /></label>
              <label className="exp__field">Estilo de juego<select defaultValue="Defensa y control"><option>Defensa y control</option><option>Daño directo</option><option>Apoyo al grupo</option></select></label>
              <label className="exp__field">Imagen de la clase<button type="button" className="pv-btn pv-btn--ghost">Elegir de la biblioteca</button></label>
            </div>
          ) : null}

          {step === 'atributos' ? (
            <div className="exp__panel exp__form">
              <label className="exp__field">Atributo principal<select defaultValue="Constitución"><option>Constitución</option><option>Fuerza</option><option>Sabiduría</option></select></label>
              <label className="exp__field">Dado de golpe<select defaultValue="d10"><option>d8</option><option>d10</option><option>d12</option></select></label>
              <label className="exp__field">Recurso propio<input defaultValue="Sellado · 3 usos por descanso" /></label>
            </div>
          ) : null}

          {step === 'equipo' ? (
            <div className="exp__panel">
              <p className="exp__hint">Arrastra desde la biblioteca o añade con el botón.</p>
              <div className="exp__chips">
                {ITEMS.map((item) => <span className="exp__chip" key={item}><img src="/icons/chest.svg" alt="" /> {item}</span>)}
              </div>
              <button type="button" className="exp__create-own">＋ Añadir objeto de la biblioteca</button>
            </div>
          ) : null}

          {step === 'habilidades' ? (
            <div className="exp__choices">
              {abilities.map((ability) => (
                <article className="exp__ability" key={ability.name}>
                  <img src={ability.icon} alt="" />
                  <div><strong>{ability.name}</strong><span className={`exp__mode ${ability.mode === 'Automático' ? 'is-auto' : 'is-manual'}`}>{ability.mode}</span></div>
                  <p>{ability.mode === 'Automático' ? 'El servidor aplica el efecto al resolver.' : 'El DM decide el resultado en la mesa.'}</p>
                </article>
              ))}
              <button type="button" className="exp__create-own"
                onClick={() => setAbilities((current) => [...current, {
                  name: mode === 'automático' ? 'Nueva habilidad automática' : 'Nueva habilidad manual',
                  mode: mode === 'automático' ? 'Automático' : 'Resolución manual',
                  icon: '/icons/attack-gauge.svg',
                }])}>
                ＋ Añadir habilidad de la biblioteca
              </button>
              <div className="exp__mode-switch" role="group" aria-label="Tipo de efecto">
                <button type="button" aria-pressed={mode === 'automático'} onClick={() => setMode('automático')}>Automático</button>
                <button type="button" aria-pressed={mode === 'manual'} onClick={() => setMode('manual')}>Resolución manual</button>
              </div>
            </div>
          ) : null}

          {step === 'progresion' ? (
            <div className="exp__panel exp__form">
              <label className="exp__field">Niveles<select defaultValue="1–20"><option>1–10</option><option>1–20</option></select></label>
              <label className="exp__field">Especializaciones<input defaultValue="Sellador, Vigilante" /></label>
              <label className="exp__field">Elección del jugador<select defaultValue="Al crear"><option>Al crear</option><option>Al subir de nivel</option><option>Ambos</option></select></label>
            </div>
          ) : null}

          {step === 'publicar' ? (
            <div className="exp__panel exp__publish">
              <h3>Así la verán los jugadores</h3>
              <div className="exp__preview-card">
                <img src="/icons/broadsword.svg" alt="" />
                <div><strong>{name || 'Nueva clase'}</strong><span>{origin.split(' — ')[0]} · {abilities.length} habilidades</span></div>
              </div>
              <h3>Dónde estará disponible</h3>
              <label className="exp__field"><input type="checkbox" defaultChecked /> Mundo: Los Reinos Olvidados</label>
              <label className="exp__field"><input type="checkbox" defaultChecked /> Campaña: La caída de Scaldar</label>
              <p className="exp__notice">Publicar no modifica a los personajes que ya usan la clase: cada jugador decide si actualiza.</p>
              <button type="button" className="pv-btn pv-btn--primary" onClick={() => setPublished(true)}>
                {published ? 'Clase publicada ✓' : 'Publicar clase'}
              </button>
            </div>
          ) : null}
        </section>

        <aside className="exp__portrait" aria-label="Vista previa de la clase">
          <span className="exp__label">VISTA PREVIA</span>
          <div className="exp__portrait-frame">
            <img src="/icons/broadsword.svg" alt="" className="exp__portrait-silhouette" />
            <span className="exp__portrait-caption">Imagen de la clase (de la biblioteca)</span>
          </div>
          <div className="exp__live">
            <span className="exp__label">RESUMEN</span>
            <p>{name || 'Sin nombre'}</p>
            <p>{origin.split(' — ')[0]}</p>
            <p>{abilities.length} habilidades · {draftSaved ? 'borrador guardado' : 'sin guardar'}</p>
          </div>
        </aside>
      </div>
    </div>
  )
}
