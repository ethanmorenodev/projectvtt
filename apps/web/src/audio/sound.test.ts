import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

type Sound = typeof import('./sound')

interface MockContextOptions {
  blocked?: boolean
  suspended?: boolean
}

interface MockContext {
  state: string
  currentTime: number
  destination: object
  resume: ReturnType<typeof vi.fn>
}

interface MockGain {
  gain: {
    setValueAtTime: ReturnType<typeof vi.fn>
    linearRampToValueAtTime: ReturnType<typeof vi.fn>
  }
  connect: ReturnType<typeof vi.fn>
}

interface MockOscillator {
  type: string
  frequency: {
    setValueAtTime: ReturnType<typeof vi.fn>
    linearRampToValueAtTime: ReturnType<typeof vi.fn>
  }
  connect: ReturnType<typeof vi.fn>
  start: ReturnType<typeof vi.fn>
  stop: ReturnType<typeof vi.fn>
}

const STORAGE_KEY = 'pv-audio-settings'

function audioMock(options: MockContextOptions = {}) {
  const contexts: MockContext[] = []
  const oscillators: MockOscillator[] = []
  const gains: MockGain[] = []

  class MockAudioContext {
    state = options.suspended ? 'suspended' : 'running'
    currentTime = 0
    destination = {}
    resume = vi.fn(async () => {
      this.state = 'running'
    })

    constructor() {
      if (options.blocked) {
        throw new Error('AudioContext bloqueado')
      }
      contexts.push(this)
    }

    createOscillator(): MockOscillator {
      const oscillator: MockOscillator = {
        type: 'sine',
        frequency: {
          setValueAtTime: vi.fn(),
          linearRampToValueAtTime: vi.fn(),
        },
        connect: vi.fn(),
        start: vi.fn(),
        stop: vi.fn(),
      }
      oscillators.push(oscillator)
      return oscillator
    }

    createGain(): MockGain {
      const gain: MockGain = {
        gain: {
          setValueAtTime: vi.fn(),
          linearRampToValueAtTime: vi.fn(),
        },
        connect: vi.fn(),
      }
      gains.push(gain)
      return gain
    }
  }

  return { MockAudioContext, contexts, oscillators, gains }
}

describe('Gestor de audio opcional (ADR-049)', () => {
  let sound: Sound
  let cleanup: (() => void) | undefined

  async function load(options: MockContextOptions = {}) {
    const mock = audioMock(options)
    vi.stubGlobal('AudioContext', mock.MockAudioContext)
    vi.resetModules()
    sound = await import('./sound')
    cleanup = sound.installAudioUnlock()
    return mock
  }

  beforeEach(() => {
    window.localStorage.clear()
  })

  afterEach(() => {
    cleanup?.()
    cleanup = undefined
    vi.unstubAllGlobals()
    vi.clearAllMocks()
  })

  it('permanece silencioso hasta que hay gesto y activación explícita', async () => {
    const { oscillators } = await load()

    sound.play('roll')
    expect(oscillators).toHaveLength(0)

    sound.setAudioSettings({ enabled: true })
    expect(sound.isEnabled()).toBe(true)
    sound.play('roll')
    expect(oscillators).toHaveLength(0)

    window.dispatchEvent(new MouseEvent('click'))
    sound.play('roll')
    expect(oscillators).toHaveLength(1)
    expect(oscillators[0].start).toHaveBeenCalledTimes(1)
  })

  it('usa un único AudioContext y lo reanuda tras el primer gesto', async () => {
    const { contexts, oscillators } = await load({ suspended: true })
    sound.setAudioSettings({ enabled: true })

    window.dispatchEvent(new MouseEvent('click'))
    window.dispatchEvent(new KeyboardEvent('keydown'))
    sound.play('confirm')

    expect(contexts).toHaveLength(1)
    expect(contexts[0].resume).toHaveBeenCalledTimes(1)
    expect(contexts[0].state).toBe('running')
    expect(oscillators).toHaveLength(1)
    expect(oscillators[0].start).toHaveBeenCalledTimes(1)
  })

  it('aplica el volumen al nivel del oscilador', async () => {
    const { gains, oscillators } = await load()
    sound.setAudioSettings({ enabled: true, volume: 100 })
    window.dispatchEvent(new MouseEvent('click'))
    sound.play('message')

    expect(oscillators).toHaveLength(1)
    expect(gains).toHaveLength(1)
    const levels = gains[0].gain.linearRampToValueAtTime.mock.calls.map(
      (call) => call[0] as number,
    )
    expect(Math.max(...levels)).toBeCloseTo(0.12)

    sound.setAudioSettings({ volume: 0 })
    sound.play('message')
    const allLevels = gains[1].gain.linearRampToValueAtTime.mock.calls.map(
      (call) => call[0] as number,
    )
    expect(Math.max(...allLevels)).toBe(0.0001)
  })

  it('guarda y recupera preferencias en localStorage', async () => {
    await load()
    sound.setAudioSettings({ enabled: true, volume: 35 })

    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '{}')).toEqual({
      enabled: true,
      volume: 35,
    })

    vi.resetModules()
    const restored: Sound = await import('./sound')
    expect(restored.getAudioSettings()).toEqual({ enabled: true, volume: 35 })
    expect(restored.isEnabled()).toBe(true)
  })

  it('degrada a silencio sin WebAudio o si el contexto está bloqueado', async () => {
    vi.stubGlobal('AudioContext', undefined)
    vi.resetModules()
    sound = await import('./sound')
    cleanup = sound.installAudioUnlock()

    expect(() => {
      sound.setAudioSettings({ enabled: true })
      window.dispatchEvent(new MouseEvent('click'))
      sound.play('token')
    }).not.toThrow()

    cleanup?.()
    const blocked = await load({ blocked: true })
    expect(() => {
      sound.setAudioSettings({ enabled: true })
      window.dispatchEvent(new MouseEvent('click'))
      sound.play('token')
    }).not.toThrow()
    expect(blocked.oscillators).toHaveLength(0)
  })

  it('localStorage no disponible no rompe la lectura ni la escritura', async () => {
    await load()
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado')
    })
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('bloqueado')
    })

    expect(() => sound.setAudioSettings({ enabled: true })).not.toThrow()
    expect(sound.isEnabled()).toBe(true)
    setItem.mockRestore()
    getItem.mockRestore()
  })
})
