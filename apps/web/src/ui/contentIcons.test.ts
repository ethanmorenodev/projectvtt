import { describe, expect, it } from 'vitest'
import { contentCover, contentIcon, coverWithImage, portraitForName } from './contentIcons'

describe('Iconografía de contenido por tipo y nombre (PMB-8.6d)', () => {
  it('usa el nombre para diferenciar ítems del mismo tipo', () => {
    expect(contentIcon('race', 'Dragonborn')).toBe('/icons/dragon-head.svg')
    expect(contentIcon('race', 'Dwarf')).toBe('/icons/dwarf-face.svg')
    expect(contentIcon('race', 'High Elf')).toBe('/icons/woman-elf-face.svg')
    expect(contentIcon('race', 'Half-Orc')).toBe('/icons/orc-head.svg')
  })

  it('distingue clases por nombre', () => {
    expect(contentIcon('class', 'Barbarian')).toBe('/icons/barbarian.svg')
    expect(contentIcon('class', 'Wizard')).toBe('/icons/wizard-face.svg')
    expect(contentIcon('class', 'Ranger')).toBe('/icons/bow-arrow.svg')
    expect(contentIcon('class', 'Paladin')).toBe('/icons/shining-sword.svg')
  })

  it('cae al icono del tipo cuando el nombre no tiene palabra clave', () => {
    expect(contentIcon('race', 'Raza sin nombre conocido')).toBe('/icons/woman-elf-face.svg')
    expect(contentIcon('location', 'Lugar X')).toBe('/icons/castle.svg')
    expect(contentIcon('ability', 'Habilidad Y')).toBe('/icons/attack-gauge.svg')
  })

  it('resuelve hechizos con ilustraciones CC0 por efecto o escuela', () => {
    expect(contentIcon('spell', 'Bola de fuego')).toBe('/art/fantasy/icon_fireball.png')
    expect(contentIcon('spell', 'Rayo de hielo')).toBe('/art/fantasy/icon_frostball.png')
    expect(contentIcon('spell', 'Curar heridas')).toBe('/art/fantasy/icon_spellheal.png')
    expect(contentIcon('spell', 'Hechizo desconocido')).toMatch(/^\/art\/fantasy\/icon_.*\.png$/)
  })

  it('aplica ilustración de hechizo a abilities etiquetadas como spell', () => {
    expect(contentIcon('ability', 'Bola de fuego', ['spell', 'srd']))
      .toBe('/art/fantasy/icon_fireball.png')
    expect(contentIcon('ability', 'Bendición', ['spell']))
      .toMatch(/^\/art\/fantasy\/icon_.*\.png$/)
    // La selección es estable: el mismo nombre siempre da la misma imagen.
    expect(contentIcon('ability', 'Bendición', ['spell']))
      .toBe(contentIcon('ability', 'Bendición', ['spell']))
  })

  it('devuelve null si no hay tipo', () => {
    expect(contentIcon(undefined, 'Cualquiera')).toBeNull()
  })
})

describe('Portadas con sentido (no aleatorias)', () => {
  it('usa retratos semánticos para razas y clases', () => {
    expect(contentCover('race', 'Dwarf')).toEqual({
      src: '/icons/dwarf-face.svg', kind: 'icon',
    })
    expect(contentCover('class', 'Wizard')).toEqual({
      src: '/icons/wizard-face.svg', kind: 'icon',
    })
  })

  it('usa ilustraciones por palabra clave en objetos y criaturas', () => {
    expect(contentCover('item', 'Longsword')).toEqual({
      src: '/art/fantasy/icon_sword.png', kind: 'cover',
    })
    expect(contentCover('creature', 'Adult Red Dragon')).toEqual({
      src: '/art/fantasy/icon_dragon.png', kind: 'cover',
    })
  })

  it('prefiere la imagen resuelta y cae a la portada derivada', () => {
    expect(coverWithImage('blob:img', 'race', 'Dwarf')).toEqual({
      src: 'blob:img', kind: 'cover',
    })
    expect(coverWithImage(null, 'race', 'Dwarf')).toEqual({
      src: '/icons/dwarf-face.svg', kind: 'icon',
    })
  })

  it('respeta el arte de hechizos y es estable', () => {
    const first = contentCover('ability', 'Fireball', ['spell', 'srd'])
    expect(first.kind).toBe('cover')
    expect(first.src).toMatch(/^\/art\/fantasy\//)
    expect(contentCover('ability', 'Fireball', ['spell', 'srd'])).toEqual(first)
  })

  it('da retratos distintos y estables a personajes distintos', () => {
    expect(portraitForName('Vex')).not.toBe(portraitForName('Grom'))
    expect(portraitForName('Vex')).toBe(portraitForName('Vex'))
    expect(portraitForName('Vex')).toMatch(/^\/icons\/.+\.svg$/)
  })
})
