/**
 * Iconografía de contenido (aprobada en PMB-8.6b, ampliada en PMB-8.6d).
 * SVG de game-icons.net, CC BY 3.0 — atribución en Créditos y en
 * `/api/v1/credits/export`. La inicial del nombre se conserva como fallback
 * cuando no hay icono asignado.
 *
 * Se elige primero por **palabra clave del nombre** (para que «Enano»,
 * «Elfo» o «Pícaro» no compartan icono) y después por tipo de contenido.
 * Es una pista visual de categoría, no arte literal del ítem.
 */

/** Ilustraciones CC0 de OpenGameArt (Drummyfish) para hechizos. */
const SPELL_ART: Array<[string, string]> = [
  ['fireball', 'icon_fireball.png'],
  ['fuego', 'icon_fireball.png'],
  ['fire', 'icon_fireball.png'],
  ['frost', 'icon_frostball.png'],
  ['ice', 'icon_frostball.png'],
  ['hielo', 'icon_frostball.png'],
  ['heal', 'icon_spellheal.png'],
  ['curar', 'icon_spellheal.png'],
  ['tornado', 'icon_tornado.png'],
  ['viento', 'icon_tornado.png'],
  ['aurora', 'icon_aurared.png'],
  ['crystal', 'icon_crystalblue.png'],
  ['poison', 'icon_potionred.png'],
  ['veneno', 'icon_potionred.png'],
  ['ring', 'icon_ringmagic.png'],
]
const SPELL_ART_ALL = [
  'icon_spellcastgreen.png', 'icon_fireball.png', 'icon_frostball.png',
  'icon_spellheal.png', 'icon_tornado.png', 'icon_aurared.png',
  'icon_crystalblue.png', 'icon_ringmagic.png', 'icon_magicdagger.png',
  'icon_magicsword.png', 'icon_potionred.png',
]

/** Selección estable por nombre: cada hechizo conserva su ilustración. */
function spellArtForName(name: string): string {
  let hash = 0
  for (let i = 0; i < name.length; i += 1) {
    hash = (hash * 31 + name.charCodeAt(i)) | 0
  }
  const file = SPELL_ART_ALL[Math.abs(hash) % SPELL_ART_ALL.length]
  return `/art/fantasy/${file}`
}

const BY_TYPE: Record<string, string> = {
  race: 'woman-elf-face',
  class: 'rogue',
  npc: 'bandit',
  creature: 'dragon-head',
  item: 'chest',
  spell: 'ankh',
  condition: 'sight-disabled',
  subclass: 'family-tree',
  faction: 'riot-shield',
  location: 'castle',
  event: 'calendar',
  ability: 'attack-gauge',
  background: 'open-book',
}

/** Palabra clave (en minúsculas) → icono. Se comprueba en orden. */
const BY_NAME: Array<[string, string]> = [
  ['dragonborn', 'dragon-head'],
  ['dragon', 'dragon-head'],
  ['dwarf', 'dwarf-face'],
  ['enano', 'dwarf-face'],
  ['half-orc', 'orc-head'],
  ['orc', 'orc-head'],
  ['goblin', 'goblin-head'],
  ['ogre', 'ogre'],
  ['troll', 'ogre'],
  ['tiefling', 'warlock-hood'],
  ['half-elf', 'elf-ear'],
  ['elf', 'woman-elf-face'],
  ['elfo', 'woman-elf-face'],
  ['halfling', 'viking-head'],
  ['gnomo', 'cyclops'],
  ['gnome', 'cyclops'],
  ['human', 'viking-head'],
  ['humano', 'viking-head'],
  ['skeleton', 'skeleton'],
  ['esqueleto', 'skeleton'],
  ['wolf', 'wolf-head'],
  ['lobo', 'wolf-head'],
  ['barbarian', 'barbarian'],
  ['bárbaro', 'barbarian'],
  ['bard', 'cleopatra'],
  ['bardo', 'cleopatra'],
  ['cleric', 'nefertiti'],
  ['clérigo', 'nefertiti'],
  ['druid', 'wolf-head'],
  ['druida', 'wolf-head'],
  ['fighter', 'broadsword'],
  ['guerrero', 'broadsword'],
  ['monk', 'cyclops'],
  ['monje', 'cyclops'],
  ['paladin', 'shining-sword'],
  ['paladín', 'shining-sword'],
  ['ranger', 'bow-arrow'],
  ['explorador', 'bow-arrow'],
  ['rogue', 'rogue'],
  ['pícaro', 'rogue'],
  ['picaro', 'rogue'],
  ['sorcerer', 'wizard-face'],
  ['hechicero', 'wizard-face'],
  ['warlock', 'warlock-hood'],
  ['brujo', 'warlock-hood'],
  ['wizard', 'wizard-face'],
  ['mago', 'wizard-face'],
  ['sword', 'broadsword'],
  ['espada', 'broadsword'],
  ['axe', 'wood-axe'],
  ['hacha', 'wood-axe'],
  ['bow', 'bow-arrow'],
  ['arco', 'bow-arrow'],
  ['castle', 'castle'],
  ['torre', 'castle'],
  ['tower', 'castle'],
  ['chest', 'chest'],
  ['cofre', 'chest'],
  ['chakram', 'chakram'],
  ['fireball', 'fireball'],
  ['fuego', 'fireball'],
  ['fire', 'fireball'],
  ['ghost', 'ghost'],
  ['espectro', 'ghost'],
  ['soul', 'soul'],
  ['alma', 'soul'],
  ['all-seeing', 'all-seeing-eye'],
  ['visión', 'all-seeing-eye'],
  ['heal', 'ankh'],
  ['curar', 'ankh'],
  ['ice', 'ice-cube'],
  ['hielo', 'ice-cube'],
  ['frost', 'ice-cube'],
  ['poison', 'poison-bottle'],
  ['veneno', 'poison-bottle'],
  ['teleport', 'teleport'],
  ['portal', 'portal'],
  ['barrier', 'barrier'],
  ['barrera', 'barrier'],
  ['shield', 'barrier'],
  ['eye-target', 'eye-target'],
  ['scry', 'all-seeing-eye'],
]

/** Ilustración CC0 que encaja con el nombre de objetos y criaturas. */
const COVER_ART_BY_KEYWORD: Array<[RegExp, string]> = [
  [/dragon/i, 'icon_dragon.png'],
  [/goblin|orc|ogre|troll|kobold/i, 'icon_orc.png'],
  [/wolf|bear|owlbear|spider|beast|cat/i, 'icon_bigcatwhite.png'],
  [/greatsword|longsword|shortsword|rapier|scimitar|sword|blade|dagger/i, 'icon_sword.png'],
  [/greataxe|handaxe|axe|warhammer|hammer|mace|spear|quarterstaff/i, 'icon_axe.png'],
  [/crossbow|longbow|shortbow|bow|arrow/i, 'icon_crossbow.png'],
  [/plate|chain mail|armor|armour|leather/i, 'icon_armor.png'],
  [/shield/i, 'icon_shield.png'],
  [/book|tome|scroll/i, 'icon_book.png'],
  [/potion|elixir|healing/i, 'icon_potionred.png'],
  [/gold|coin|treasure|chest/i, 'icon_chest.png'],
  [/ring/i, 'icon_ringmagic.png'],
  [/crystal|gem|orb/i, 'icon_crystalblue.png'],
  [/fire|flame|burn/i, 'icon_fireball.png'],
  [/frost|ice|cold/i, 'icon_frostball.png'],
  [/poison|venom/i, 'icon_potionred.png'],
  [/tornado|storm|wind/i, 'icon_tornado.png'],
]

/** Retratos de personaje (iconos CC BY ya licenciados) elegidos de forma estable por
 *  nombre, para que Vex, Grom y Kira no compartan el mismo retrato. */
const PORTRAITS = [
  'woman-elf-face', 'dwarf-face', 'orc-head', 'wizard-face', 'barbarian',
  'rogue', 'ranger', 'paladin', 'warlock-hood', 'nefertiti', 'cleopatra', 'bandit',
] as const

export function portraitForName(name: string): string {
  let hash = 0
  for (let i = 0; i < name.length; i += 1) {
    hash = (hash * 31 + name.charCodeAt(i)) | 0
  }
  return `/icons/${PORTRAITS[Math.abs(hash) % PORTRAITS.length]}.svg`
}

/** Portada con sentido: hechizo → arte propio; objeto/criatura → ilustración
 *  por palabra clave; raza/clase/condición → retrato o icono semántico SVG.
 *  Nunca aleatoria por hash. El kind `cover` comparte recorte, esquinas y
 *  degradado con la imagen asignada por el autor. */
export function contentCover(
  typeKey: string | undefined, name?: string, tags?: readonly string[] | null,
): { src: string; kind: 'cover' | 'icon' } {
  const normalized = (name ?? '').toLowerCase()
  if ((tags ?? []).some((tag) => tag.toLowerCase() === 'spell')) {
    return { src: spellArtForName(normalized), kind: 'cover' }
  }

  const base = typeKey?.split(':')[0] ?? ''
  if (base === 'item' || base === 'creature') {
    const match = COVER_ART_BY_KEYWORD.find(([pattern]) => pattern.test(normalized))
    if (match) {
      return { src: `/art/fantasy/${match[1]}`, kind: 'cover' }
    }
  }

  const icon = contentIcon(typeKey, name, tags)
  if (icon) {
    return { src: icon, kind: 'icon' }
  }
  // Último recurso para tipos sin icono asignado: ilustración estable.
  return { src: '/art/fantasy/icon_book.png', kind: 'cover' }
}

/** Helper compartido por la rejilla y el editor: la imagen cargada del
 *  contenido gana; si no hay `imageId` o falla la carga se usa la portada
 *  derivada. La URL ya resuelta la entrega `useImageUrl`. */
export function coverWithImage(
  imageUrl: string | null,
  typeKey: string | undefined,
  name?: string,
  tags?: readonly string[] | null,
): { src: string; kind: 'cover' | 'icon' } {
  return imageUrl ? { src: imageUrl, kind: 'cover' } : contentCover(typeKey, name, tags)
}

export function contentIcon(
  typeKey: string | undefined, name?: string, tags?: readonly string[] | null,
): string | null {
  const base = typeKey?.split(':')[0]
  if (!base) return null

  const normalized = (name ?? '').toLowerCase()
  const isSpell = base === 'spell'
    || (tags ?? []).some((tag) => tag.toLowerCase() === 'spell')
  if (isSpell) {
    const art = SPELL_ART.find(([keyword]) => normalized.includes(keyword))?.[1]
    return art ? `/art/fantasy/${art}` : spellArtForName(normalized)
  }
  const named = BY_NAME.find(([keyword]) => normalized.includes(keyword))?.[1]
  const icon = named ?? BY_TYPE[base]
  return icon ? `/icons/${icon}.svg` : null
}
