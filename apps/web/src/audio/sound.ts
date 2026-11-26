/**
 * Audio opcional (ADR-049): tonos breves generados con WebAudio en el cliente.
 * Sin samples, sin descargas y sin assets atribuibles. El audio es una señal
 * adicional: la información de los eventos sigue siendo visual.
 */

export type AudioKind = 'roll' | 'token' | 'message' | 'confirm'

/** Volumen en porcentaje (0-100), el mismo rango que el control de ajustes. */
export interface AudioSettings {
  enabled: boolean
  volume: number
}

const STORAGE_KEY = 'pv-audio-settings'
const DEFAULT_SETTINGS: AudioSettings = { enabled: false, volume: 60 }

/** Tono (frecuencia inicial → final) y duración en segundos de cada efecto. */
const TONES: Record<AudioKind, { from: number; to: number; duration: number }> = {
  roll: { from: 380, to: 620, duration: 0.14 },
  token: { from: 300, to: 340, duration: 0.08 },
  message: { from: 660, to: 880, duration: 0.12 },
  confirm: { from: 520, to: 780, duration: 0.14 },
}

type AudioParamLike = {
  setValueAtTime(value: number, time: number): void
  linearRampToValueAtTime(value: number, time: number): void
}

type AudioContextLike = {
  state: string
  currentTime: number
  destination: unknown
  resume(): Promise<void>
  createOscillator(): {
    type: OscillatorType
    frequency: AudioParamLike
    connect(node: unknown): void
    start(time: number): void
    stop(time: number): void
  }
  createGain(): {
    gain: AudioParamLike
    connect(node: unknown): void
  }
}

type AudioContextCtor = new () => AudioContextLike

let settings: AudioSettings = readSettings()
let context: AudioContextLike | null = null
let unlocked = false
let unavailable = false

function normalize(value: Partial<AudioSettings> | null): AudioSettings {
  const enabled = typeof value?.enabled === 'boolean' ? value.enabled : DEFAULT_SETTINGS.enabled
  const rawVolume =
    typeof value?.volume === 'number' && Number.isFinite(value.volume)
      ? value.volume
      : DEFAULT_SETTINGS.volume
  return {
    enabled,
    volume: Math.max(0, Math.min(100, Math.round(rawVolume))),
  }
}

function readSettings(): AudioSettings {
  if (typeof window === 'undefined') {
    return { ...DEFAULT_SETTINGS }
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    return raw ? normalize(JSON.parse(raw) as Partial<AudioSettings>) : { ...DEFAULT_SETTINGS }
  } catch {
    return { ...DEFAULT_SETTINGS }
  }
}

function contextCtor(): AudioContextCtor | undefined {
  if (typeof window === 'undefined') {
    return undefined
  }
  const scope = window as typeof window & { webkitAudioContext?: AudioContextCtor }
  return window.AudioContext ?? scope.webkitAudioContext
}

function ensureContext(): AudioContextLike | null {
  if (context) {
    return context
  }
  if (unavailable) {
    return null
  }
  const Ctor = contextCtor()
  if (!Ctor) {
    unavailable = true
    return null
  }
  try {
    context = new Ctor()
    return context
  } catch {
    unavailable = true
    return null
  }
}

/** Devuelve una copia de las preferencias vigentes. */
export function getAudioSettings(): AudioSettings {
  return { ...settings }
}

export function isEnabled(): boolean {
  return settings.enabled
}

/** Guarda las preferencias y las aplica a los siguientes efectos. */
export function setAudioSettings(next: Partial<AudioSettings>): void {
  const parsed = normalize({ ...settings, ...next })
  settings = parsed
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(parsed))
  } catch {
    // Almacenamiento bloqueado: las preferencias siguen vigentes en memoria.
  }
}

/**
 * Registra el primer gesto del usuario para crear/reanudar el AudioContext.
 * Se instala una sola vez desde la app; devuelve la función de limpieza.
 */
export function installAudioUnlock(): () => void {
  if (typeof window === 'undefined') {
    return () => undefined
  }

  const unlock = () => {
    const ctx = ensureContext()
    if (!ctx) {
      return
    }
    unlocked = true
    if (ctx.state === 'suspended') {
      try {
        void ctx.resume().catch(() => undefined)
      } catch {
        unavailable = true
      }
    }
  }

  window.addEventListener('click', unlock, true)
  window.addEventListener('keydown', unlock, true)
  return () => {
    window.removeEventListener('click', unlock, true)
    window.removeEventListener('keydown', unlock, true)
  }
}

/**
 * Reproduce un tono breve si el audio está activo y ya hubo un gesto.
 * Cualquier fallo de WebAudio degrada a silencio sin afectar a la UI.
 */
export function play(kind: AudioKind): void {
  if (!settings.enabled || !unlocked || unavailable) {
    return
  }
  const ctx = context
  if (!ctx) {
    return
  }
  try {
    if (ctx.state === 'suspended') {
      void ctx.resume().catch(() => undefined)
    }
    const { from, to, duration } = TONES[kind]
    const now = ctx.currentTime
    const oscillator = ctx.createOscillator()
    const gain = ctx.createGain()
    const level = 0.12 * (settings.volume / 100)

    oscillator.type = 'sine'
    oscillator.frequency.setValueAtTime(from, now)
    oscillator.frequency.linearRampToValueAtTime(to, now + duration)
    gain.gain.setValueAtTime(0.0001, now)
    gain.gain.linearRampToValueAtTime(level, now + 0.01)
    gain.gain.linearRampToValueAtTime(0.0001, now + duration)

    oscillator.connect(gain)
    gain.connect(ctx.destination)
    oscillator.start(now)
    oscillator.stop(now + duration)
  } catch {
    // Navegador sin WebAudio o contexto bloqueado: silencio, sin excepciones.
    unavailable = true
  }
}
