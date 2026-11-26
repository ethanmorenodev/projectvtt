import { useState } from 'react'
import { getAudioSettings, play, setAudioSettings } from '../audio/sound'
import { t } from '../i18n'
import './herramientas.css'

/**
 * Ajustes de sonido (ADR-049): controles explícitos para activar los efectos,
 * elegir el volumen y probarlos. El sonido es opcional y complementario: los
 * eventos siguen mostrándose visualmente en la interfaz.
 */
export function AjustesView() {
  const [settings, setSettings] = useState(getAudioSettings)
  const copy = t.ajustes

  function change(next: Partial<typeof settings>) {
    const applied = { ...settings, ...next }
    setSettings(applied)
    setAudioSettings(applied)
  }

  return (
    <main className="ajustes">
      <header className="ajustes__head">
        <h1 className="biblio__title">{copy.title}</h1>
        <p className="biblio__subtitle">{copy.subtitle}</p>
      </header>

      <section className="ajustes__panel" aria-label={copy.title}>
        <div className="ajustes__row">
          <label htmlFor="pv-audio-enabled">{copy.activar}</label>
          <input
            id="pv-audio-enabled"
            type="checkbox"
            role="switch"
            aria-checked={settings.enabled}
            checked={settings.enabled}
            onChange={(event) => change({ enabled: event.target.checked })}
          />
        </div>

        <div className="ajustes__row">
          <label htmlFor="pv-audio-volume">{copy.volumen}</label>
          <input
            id="pv-audio-volume"
            type="range"
            min={0}
            max={100}
            step={5}
            value={settings.volume}
            onChange={(event) => change({ volume: Number(event.target.value) })}
          />
          <output htmlFor="pv-audio-volume">{settings.volume}%</output>
        </div>

        <div className="ajustes__row ajustes__row--actions">
          <button type="button" className="pv-btn pv-btn--primary" onClick={() => play('confirm')}>
            {copy.probar}
          </button>
        </div>

        <p className="ajustes__note">{copy.descripcion}</p>
        <p className="ajustes__note">{copy.avisoNavegador}</p>
      </section>
    </main>
  )
}
