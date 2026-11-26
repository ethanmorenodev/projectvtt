import { useState, type MouseEvent } from 'react'
import { fetchAnnotations, setAnnotations } from '../api/annotations'
import type { SceneAnnotationsState, SetSceneAnnotationRequest } from '../api/types'
import { replace, useSceneAnnotations } from '../realtime/annotationStore'
import { t } from '../i18n'
import '../views/mapTools.css'

/**
 * Herramientas de mapa en la mesa (PMB-7.5/ADR-043): panel de ventana
 * (ADR-033). La medición sigue siendo ayuda local; dibujo y notas se
 * comparten mediante el store y el comando idempotente `SetSceneAnnotation`.
 * La niebla y el grid siguen siendo propiedad del servidor (ADR-020/023).
 * El mapa base y la niebla se renderizan en Pixi (SceneStage) y siguen
 * controlados por el inspector de escena: este panel solo los etiqueta como
 * tales y no altera su render. Las anotaciones SVG sí se dibujan aquí, así
 * que su visibilidad/opacidad son reales.
 */

type Tool = 'select' | 'measure' | 'draw' | 'annotate'
interface Point { x: number; y: number }
interface Layer { id: string; name: string; visible: boolean; opacity: number }

export interface MapToolsViewProps {
  /** Estado compartido de la escena; si falta, se lee del store por sceneId. */
  annotations?: SceneAnnotationsState
  /** Solo la UI: el servidor vuelve a validar el rol (GM/Owner). */
  canWrite?: boolean
}

const LAYERS: Layer[] = [
  { id: 'mapa', name: 'Mapa base', visible: true, opacity: 1 },
  { id: 'niebla', name: 'Niebla', visible: true, opacity: 0.65 },
  { id: 'anotaciones', name: 'Anotaciones', visible: true, opacity: 1 },
]
const COLUMNS = 10
const ROWS = 6
const FEET = 5

export function MapToolsView({ annotations, canWrite = false }: MapToolsViewProps) {
  const [tool, setTool] = useState<Tool>('measure')
  const [layers, setLayers] = useState<Layer[]>(() => LAYERS.map((layer) => ({ ...layer })))
  const [measure, setMeasure] = useState<Point[]>([])
  const [drawing, setDrawing] = useState<Point[]>([])
  const [notes, setNotes] = useState<Point[]>([])
  const [noteText, setNoteText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const labels = t.mesa.tools.mapPanel
  const live = useSceneAnnotations(annotations?.sceneId ?? '')
  const shared = annotations ?? live
  const sceneId = shared.sceneId
  const noteCount = shared.items.filter((entry) => entry.kind === 'note').length + notes.length

  // Solo las anotaciones de este SVG responden a los controles de capa; el
  // mapa y la niebla pertenecen al inspector de escena (Pixi/SceneStage).
  const overlayLayer = layers.find((layer) => layer.id === 'anotaciones') ?? LAYERS[2]
  const overlayStyle = {
    display: overlayLayer.visible ? undefined : 'none',
    opacity: overlayLayer.opacity,
  }
  const layerName = (layer: Layer) =>
    layer.id === 'anotaciones' ? layer.name : `${layer.name} · ${t.mesa.inspector}`

  const squares = measure.length === 2
    ? Math.round(Math.hypot(measure[1].x - measure[0].x, measure[1].y - measure[0].y))
    : 0

  const click = (event: MouseEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect()
    const point = {
      x: Math.floor((event.clientX - rect.left) / rect.width * COLUMNS) + 0.5,
      y: Math.floor((event.clientY - rect.top) / rect.height * ROWS) + 0.5,
    }
    if (tool === 'measure') setMeasure((current) => [...current, point].slice(-2))
    else if (tool === 'draw') setDrawing((current) => [...current, point])
    else if (tool === 'annotate' && noteText.trim()) {
      setNotes((current) => [...current, point])
    }
  }

  const resetPending = () => {
    setDrawing([])
    setNotes([])
    setNoteText('')
  }

  const share = async (kind: 'draw' | 'note') => {
    const points = kind === 'draw' ? drawing : notes
    if (!canWrite || !sceneId || points.length === 0) return
    const request: SetSceneAnnotationRequest = {
      commandId: crypto.randomUUID(),
      expectedVersion: shared.version,
      kind,
      points,
      text: kind === 'note' ? noteText.trim() : null,
      clear: false,
    }
    try {
      replace(await setAnnotations(sceneId, request))
      resetPending()
      setError(null)
    } catch (cause) {
      // Un 409 deja la versión local obsoleta: se recarga antes de reintentar.
      try {
        replace(await fetchAnnotations(sceneId))
      } catch {
        // Se conserva el mensaje del fallo original.
      }
      setError(cause instanceof Error ? cause.message : labels.error)
    }
  }

  const clearAll = async () => {
    if (!canWrite || !sceneId) return
    try {
      replace(await setAnnotations(sceneId, {
        commandId: crypto.randomUUID(),
        expectedVersion: shared.version,
        kind: 'draw',
        clear: true,
      }))
      resetPending()
      setError(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : labels.error)
    }
  }

  return (
    <div className="maptools maptools--panel">
      <div className="maptools__rail" aria-label={t.mesa.tools.mapTools}>
        {([
          ['select', labels.select], ['measure', labels.measure],
          ['draw', labels.draw], ['annotate', labels.annotate],
        ] as [Tool, string][]).map(([id, label]) => (
          <button key={id} type="button" aria-pressed={tool === id}
            onClick={() => setTool(id)}>{label}</button>
        ))}
      </div>

      <div className="maptools__stage" aria-label={t.mesa.tools.mapTools}>
        <div className="maptools__canvas" onClick={click}
          style={{ backgroundSize: `${100 / COLUMNS}% ${100 / ROWS}%` }}>
          <svg viewBox={`0 0 ${COLUMNS} ${ROWS}`} preserveAspectRatio="none"
            className="maptools__overlay" role="img" aria-label={labels.shared}
            style={overlayStyle}>
            {measure.length === 2 && (
              <line x1={measure[0].x} y1={measure[0].y}
                x2={measure[1].x} y2={measure[1].y}
                stroke="#ffd166" strokeWidth="0.12" strokeDasharray="0.3 0.2" />
            )}
            {drawing.length > 1 && (
              <polyline points={drawing.map((p) => `${p.x},${p.y}`).join(' ')}
                fill="none" stroke="#8ecae6" strokeWidth="0.12" />
            )}
            {shared.items.filter((entry) => entry.kind === 'draw').map((entry) => (
              <polyline key={entry.id} className="maptools__annotation maptools__annotation--draw"
                points={entry.points.map((p) => `${p.x},${p.y}`).join(' ')}
                fill="none" stroke="#8ecae6" strokeWidth="0.12" />
            ))}
            {shared.items.filter((entry) => entry.kind === 'note').map((entry) => (
              <text key={entry.id} className="maptools__annotation maptools__annotation--note"
                x={entry.points[0]?.x ?? 0} y={entry.points[0]?.y ?? 0}
                fill="#ffd166" fontSize="0.35">{entry.text ?? ''}</text>
            ))}
          </svg>
          {notes.map((point, index) => (
            <span key={index} className="maptools__pin" aria-hidden="true"
              style={{ left: `${point.x / COLUMNS * 100}%`,
                top: `${point.y / ROWS * 100}%`, ...overlayStyle }}>
              <img src="/icons/position-marker.svg" alt="" />
            </span>
          ))}
        </div>
        <p className="biblio-inspector__note" role="status">
          {labels.squares(squares)} · {labels.feet(squares * FEET)} · {noteCount} {labels.note}
        </p>
        {tool === 'annotate' && (
          <div className="maptools__note">
            <input value={noteText} aria-label={labels.note}
              placeholder={labels.note}
              onChange={(event) => setNoteText(event.target.value)} />
          </div>
        )}
        {canWrite && sceneId && (
          <div className="maptools__actions" role="group" aria-label={labels.shared}>
            <button type="button" disabled={drawing.length === 0}
              onClick={() => void share('draw')}>{labels.shareDraw}</button>
            <button type="button" disabled={notes.length === 0 || !noteText.trim()}
              onClick={() => void share('note')}>{labels.shareNote}</button>
            <button type="button" disabled={shared.items.length === 0}
              onClick={() => void clearAll()}>{labels.clearAnnotations}</button>
          </div>
        )}
        {error && <p className="maptools__error" role="alert">{error}</p>}
      </div>

      <aside className="img-biblio__inspector" aria-label={labels.layers}>
        <h4 className="biblio-zone__title">{labels.layers}</h4>
        {layers.map((layer) => (
          <div className="maptools__layer" key={layer.id}>
            <button type="button"
              aria-label={`${layer.visible ? labels.hide : labels.show} ${layerName(layer)}`}
              title={layer.id === 'anotaciones' ? undefined : t.mesa.inspector}
              onClick={() => setLayers((current) => current.map((entry) =>
                entry.id === layer.id ? { ...entry, visible: !entry.visible } : entry))}>
              <img
                src={layer.visible ? '/icons/gaze.svg' : '/icons/sight-disabled.svg'}
                alt=""
                aria-hidden="true"
              />
            </button>
            <span>{layerName(layer)}</span>
            <input type="range" min={0} max={100}
              value={Math.round(layer.opacity * 100)}
              aria-label={`${labels.opacity} ${layerName(layer)}`}
              onChange={(event) => setLayers((current) => current.map((entry) =>
                entry.id === layer.id
                  ? { ...entry, opacity: Number(event.target.value) / 100 }
                  : entry))} />
          </div>
        ))}
      </aside>
    </div>
  )
}
