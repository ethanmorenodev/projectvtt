import { describe, expect, it } from 'vitest'
import { navigate, parseRoute, routePath } from './router'

describe('parseRoute', () => {
  it('reconoce las rutas del área de trabajo', () => {
    expect(parseRoute('/')).toEqual({ kind: 'home' })
    expect(parseRoute('/campaigns')).toEqual({ kind: 'campaigns' })
    expect(parseRoute('/campaigns/abc-123')).toEqual({ kind: 'campaign', campaignId: 'abc-123' })
    expect(parseRoute('/campaigns/abc/scenes/def')).toEqual({
      kind: 'scene',
      campaignId: 'abc',
      sceneId: 'def',
    })
    expect(parseRoute('/campaigns/abc/mesa')).toEqual({ kind: 'table', campaignId: 'abc' })
    expect(parseRoute('/inicio')).toEqual({ kind: 'inicio' })
    expect(parseRoute('/biblioteca')).toEqual({ kind: 'biblioteca' })
    expect(parseRoute('/herramientas')).toEqual({ kind: 'herramientas' })
    expect(parseRoute('/gestion')).toEqual({ kind: 'gestion' })
    expect(parseRoute('/imagenes')).toEqual({ kind: 'imagenes' })
    expect(parseRoute('/creditos')).toEqual({ kind: 'creditos' })
    expect(parseRoute('/login')).toEqual({ kind: 'login' })
  })

  it('normaliza barras finales y rechaza rutas inválidas', () => {
    expect(parseRoute('/campaigns/')).toEqual({ kind: 'campaigns' })
    expect(parseRoute('/campaigns/../etc')).toEqual({ kind: 'not-found' })
    expect(parseRoute('/campaigns/abc/scenes')).toEqual({ kind: 'not-found' })
    expect(parseRoute('/otra-cosa')).toEqual({ kind: 'not-found' })
  })

  it('routePath es la inversa de parseRoute', () => {
    const scene = parseRoute('/campaigns/abc/scenes/def')
    expect(routePath(scene)).toBe('/campaigns/abc/scenes/def')
    expect(parseRoute(routePath(scene))).toEqual(scene)
    expect(routePath({ kind: 'herramientas' })).toBe('/herramientas')
    expect(parseRoute(routePath({ kind: 'herramientas' }))).toEqual({ kind: 'herramientas' })
    expect(routePath({ kind: 'table', campaignId: 'abc' })).toBe('/campaigns/abc/mesa')
    expect(parseRoute(routePath({ kind: 'table', campaignId: 'abc' })))
      .toEqual({ kind: 'table', campaignId: 'abc' })
  })
})

describe('navigate', () => {
  it('actualiza la URL con pushState y replaceState', () => {
    navigate('/campaigns/abc')
    expect(window.location.pathname).toBe('/campaigns/abc')
    navigate('/campaigns/def', true)
    expect(window.location.pathname).toBe('/campaigns/def')
  })
})
