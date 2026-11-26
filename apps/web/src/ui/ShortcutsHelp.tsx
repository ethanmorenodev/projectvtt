import { useEffect } from 'react'
import { t } from '../i18n'
import { MESA_KEYS } from './shortcuts'
import './shortcutsHelp.css'

/** Ayuda de atajos de teclado (accesibilidad): diálogo ligero y cerrable. */
export function ShortcutsHelp({ open, onClose, showMesa = false }: {
  open: boolean
  onClose: () => void
  showMesa?: boolean
}) {
  useEffect(() => {
    if (!open) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null

  const rows: Array<{ keys: string[]; label: string }> = [
    { keys: ['Ctrl', 'K'], label: t.shell.shortcutSearch },
    { keys: ['Esc'], label: t.shell.shortcutEscape },
    { keys: ['Tab'], label: t.shell.shortcutFocus },
    { keys: ['↑', '↓'], label: t.shell.shortcutNav },
    { keys: ['↵'], label: t.shell.shortcutEnter },
    { keys: ['?'], label: t.shell.shortcutsTitle },
  ]

  return (
    <div className="pv-shortcuts__overlay" onClick={onClose}>
      <div className="pv-shortcuts" role="dialog" aria-modal="true"
        aria-label={t.shell.shortcutsTitle} onClick={(event) => event.stopPropagation()}>
        <header className="pv-shortcuts__head">
          <h2>{t.shell.shortcutsTitle}</h2>
          <button type="button" onClick={onClose} aria-label={t.shell.shortcutsClose}>×</button>
        </header>
        <section>
          <h3>{t.shell.shortcutsGeneral}</h3>
          <dl>
            {rows.map((row) => (
              <div key={row.label} className="pv-shortcuts__row">
                <dt>{row.keys.map((key) => <kbd key={key}>{key}</kbd>)}</dt>
                <dd>{row.label}</dd>
              </div>
            ))}
          </dl>
        </section>
        {showMesa && (
          <section>
            <h3>{t.shell.shortcutsTable}</h3>
            <dl>
              {([
                [MESA_KEYS.roll, t.mesa.shortcuts.roll],
                [MESA_KEYS.annotate, t.mesa.shortcuts.annotate],
                [MESA_KEYS.measure, t.mesa.shortcuts.measure],
                [MESA_KEYS.fog, t.mesa.shortcuts.fog],
              ] as const).map(([key, label]) => (
                <div key={key} className="pv-shortcuts__row">
                  <dt><kbd>{key.toUpperCase()}</kbd></dt>
                  <dd>{label}</dd>
                </div>
              ))}
            </dl>
          </section>
        )}
        <p className="pv-shortcuts__hint">{t.shell.shortcutHint}</p>
      </div>
    </div>
  )
}
