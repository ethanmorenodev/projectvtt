import { useState, type MouseEvent } from 'react'
import { PrototypeShell } from '../fase2/AreasFase2'
import {
  FEET_PER_SQUARE, GRID_COLUMNS, GRID_ROWS,
  SAMPLE_ANNOTATIONS, SAMPLE_LAYERS,
  type ProtoAnnotation, type ProtoLayer,
} from './mapToolsData'
import '../../../views/mapTools.css'

/**
 * Prototipo de herramientas de mapa (PMB-7.5, solo desarrollo): medición
 * en cuadrados de 5 ft, dibujo libre, anotaciones, capas y visibilidad.
 * Referencia visual: `docs/visual/PROJECT_VISUAL_GUIDELINES.md`.
 */

type Tool = 'select' | 'measure' | 'draw' | 'annotate'

interface Point { x: number; y: number }

export function MapToolsPrototype({ variant = 'normal' }: { variant?: 'normal' | 'vacio' }) {
  const [tool, setTool] = useState<Tool>('measure')
  const [layers, setLayers] = useState<ProtoLayer[]>(() => SAMPLE_LAYERS.map((l) => ({ ...l })))
  const [annotations, setAnnotations] = useState<ProtoAnnotation[]>(
    () => (variant === 'vacio' ? [] : SAMPLE_ANNOTATIONS.map((a) => ({ ...a }))))
  const [measure, setMeasure] = useState<Point[]>([])
  const [drawing, setDrawing] = useState<Point[]>([])
  const [note, setNote] = useState('Nota del DM')
  const [notice, setNotice] = useState<string | null>(null)

  const squares = measure.length === 2
    ? Math.round(Math.hypot(
        measure[1].x - measure[0].x, measure[1].y - measure[0].y))
    : 0

  const clickMap = (event: MouseEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect()
    const point = {
      x: Math.floor((event.clientX - rect.left) / rect.width * GRID_COLUMNS) + 0.5,
      y: Math.floor((event.clientY - rect.top) / rect.height * GRID_ROWS) + 0.5,
    }
    if (tool === 'measure') {
      const next = [...measure, point].slice(-2)
      setMeasure(next)
      if (next.length === 2) {
        const distance = Math.round(Math.hypot(
          next[1].x - next[0].x, next[1].y - next[0].y)) * FEET_PER_SQUARE
        setNotice(`Distancia: ${distance} pies (${distance / FEET_PER_SQUARE} cuadrados).`)
      }
    } else if (tool === 'draw') {
      setDrawing((current) => [...current, point])
      setNotice(`Trazo: ${drawing.length + 1} punto(s).`)
    } else if (tool === 'annotate') {
      setAnnotations((current) => [...current, {
        id: `a-${current.length + 1}`, x: point.x, y: point.y,
        text: note.trim() || 'Nota',
      }])
      setNotice(`Anotación «${note}» añadida.`)
    } else {
      setNotice('Herramienta Seleccionar: nada que medir.')
    }
  }

  const toggleLayer = (id: string) => {
    setLayers((current) => current.map((layer) =>
      layer.id === id ? { ...layer, visible: !layer.visible } : layer))
    const layer = layers.find((entry) => entry.id === id)
    setNotice(`Capa «${layer?.name}» ${layer?.visible ? 'oculta' : 'visible'}.`)
  }

  const visible = (id: string) => layers.find((layer) => layer.id === id)?.visible ?? false

  return (
    <PrototypeShell active="mesa">
      <div className="maptools">
        <header className="maptools__head">
          <div>
            <h1 className="biblio__title">Herramientas de mapa</h1>
            <p className="biblio__subtitle">
              Medición · dibujo · anotaciones · capas y visibilidad · 1 cuadrado = {FEET_PER_SQUARE} ft
            </p>
          </div>
        </header>

        <div className="maptools__body">
          <nav className="maptools__rail" aria-label="Herramientas">
            {([
              ['select', 'Seleccionar'], ['measure', 'Medir'],
              ['draw', 'Dibujar'], ['annotate', 'Anotar'],
            ] as [Tool, string][]).map(([id, label]) => (
              <button key={id} type="button" aria-pressed={tool === id}
                onClick={() => { setTool(id); setNotice(`Herramienta: ${label}.`) }}>
                {label}
              </button>
            ))}
            <label className="maptools__note">
              Texto de anotación
              <input value={note} onChange={(event) => setNote(event.target.value)} />
            </label>
          </nav>

          <section className="maptools__stage" aria-label="Mapa">
            <div className="maptools__canvas" onClick={clickMap}
              style={{ backgroundSize: `${100 / GRID_COLUMNS}% ${100 / GRID_ROWS}%` }}>
              <svg viewBox={`0 0 ${GRID_COLUMNS} ${GRID_ROWS}`} preserveAspectRatio="none"
                className="maptools__overlay" aria-hidden="true">
                {visible('mapa') && (
                  <rect x="0" y="0" width={GRID_COLUMNS} height={GRID_ROWS}
                    fill="rgba(60, 90, 120, 0.25)" />
                )}
                {measure.length === 2 && (
                  <line x1={measure[0].x} y1={measure[0].y}
                    x2={measure[1].x} y2={measure[1].y}
                    stroke="#ffd166" strokeWidth="0.12" strokeDasharray="0.3 0.2" />
                )}
                {drawing.length > 1 && (
                  <polyline points={drawing.map((p) => `${p.x},${p.y}`).join(' ')}
                    fill="none" stroke="#8ecae6" strokeWidth="0.12" />
                )}
              </svg>
              {visible('anotaciones') && annotations.map((annotation) => (
                <span key={annotation.id} className="maptools__pin"
                  style={{ left: `${annotation.x / GRID_COLUMNS * 100}%`,
                    top: `${annotation.y / GRID_ROWS * 100}%` }}
                  title={annotation.text}>
                  <img src="/icons/position-marker.svg" alt="" />
                </span>
              ))}
              {visible('niebla') && (
                <span className="maptools__fog" aria-hidden="true">
                  <img src="/icons/fog.svg" alt="" /> Niebla activa
                </span>
              )}
            </div>
            <p className="biblio-inspector__note" role="status">
              {notice ?? `Herramienta ${tool} · ${squares} cuadrados · ${annotations.length} anotación(es)`}
            </p>
          </section>

          <aside className="img-biblio__inspector" aria-label="Capas">
            <h2 className="biblio-zone__title">Capas</h2>
            {layers.map((layer) => (
              <div className="maptools__layer" key={layer.id}>
                <button type="button" onClick={() => toggleLayer(layer.id)}
                  aria-label={`${layer.visible ? 'Ocultar' : 'Mostrar'} ${layer.name}`}>
                  <img
                    src={layer.visible ? '/icons/gaze.svg' : '/icons/sight-disabled.svg'}
                    alt=""
                    aria-hidden="true"
                  />
                </button>
                <span>{layer.name}</span>
                <span className="biblio-tag">{layer.kind}</span>
                <input type="range" min={0} max={100} value={Math.round(layer.opacity * 100)}
                  aria-label={`Opacidad de ${layer.name}`}
                  onChange={(event) => setLayers((current) => current.map((entry) =>
                    entry.id === layer.id
                      ? { ...entry, opacity: Number(event.target.value) / 100 }
                      : entry))} />
              </div>
            ))}
            <h4 className="biblio-inspector__section">Medición</h4>
            <p className="biblio-inspector__note">
              {squares} cuadrados · {squares * FEET_PER_SQUARE} ft
            </p>
          </aside>
        </div>
      </div>
    </PrototypeShell>
  )
}
