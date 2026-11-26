/**
 * Genera `backend/Api/Content/Seed/dnd5e-srd-5.2-content.json` a partir de
 * https://www.dnd5eapi.co/api/2024 (SRD 5.2.1, CC BY 4.0; datos de
 * github.com/5e-bits/5e-database). Sin texto inventado: las descripciones
 * son las que devuelve la API, literalmente.
 *
 * Uso:
 *   node tools/seed/transform-srd-52.mjs
 *   LIMIT_SPELLS=40 LIMIT_MONSTERS=30 LIMIT_EQUIPMENT=30 node tools/seed/transform-srd-52.mjs
 *
 * Determinista: se ordena el índice por `index`, se aplica el límite, se
 * recuperan los detalles y se ordenan los ítems por `itemKey`. No entran
 * campos volátiles de la API (p. ej. `updated_at`). Misma entrada → mismo
 * JSON. `pack.key` es `dnd5e-srd-5.2`: `ContentSeedLoader` asocia cada
 * archivo al pack por esa clave, no por el nombre del archivo.
 */
import { writeFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const out = join(here, '../../backend/Api/Content/Seed/dnd5e-srd-5.2-content.json')
const API = 'https://www.dnd5eapi.co/api/2024'

const limitFor = (name, fallback) => {
  const raw = process.env[name]
  if (raw === undefined || raw === '') return fallback
  const value = Number(raw)
  if (!Number.isInteger(value) || value < 0) {
    throw new Error(`${name} debe ser un entero >= 0 (recibido: ${raw})`)
  }
  return value
}

const LIMITS = {
  spells: limitFor('LIMIT_SPELLS', 40),
  monsters: limitFor('LIMIT_MONSTERS', 30),
  equipment: limitFor('LIMIT_EQUIPMENT', 30),
}

const compare = (a, b) => (a < b ? -1 : a > b ? 1 : 0)
const asText = (value) =>
  Array.isArray(value) ? value.join('\n\n') : (typeof value === 'string' ? value : '')
// En SRD 5.2 el campo se llama `description` (string o lista); en 5.1 era
// `desc`. Se acepta ambos para que el mapeo sea explícito y sin inventar texto.
const descriptionOf = (data) => asText(data.description ?? data.desc)
const summaryOf = (data) =>
  descriptionOf(data).split(/\n+/).map((line) => line.trim()).find(Boolean) || data.name

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

const getJson = async (path, attempt = 0) => {
  const response = await fetch(`${API}/${path}`)
  if (response.ok) return response.json()
  // La API limita la tasa de peticiones: reintento con backoff (el orden de
  // llegada no afecta al JSON final, que se ordena por `itemKey`).
  if ((response.status === 429 || response.status >= 500) && attempt < 6) {
    await sleep(500 * 2 ** attempt)
    return getJson(path, attempt + 1)
  }
  throw new Error(`GET ${API}/${path} → HTTP ${response.status}`)
}

const mapLimited = async (values, fn, concurrency = 3) => {
  const results = new Array(values.length)
  let cursor = 0
  await Promise.all(Array.from({ length: Math.min(concurrency, values.length) }, async () => {
    while (cursor < values.length) {
      const position = cursor++
      results[position] = await fn(values[position])
    }
  }))
  return results
}

const fetchType = async (type) => {
  const index = await getJson(type)
  const ids = index.results.map((entry) => entry.index).sort(compare).slice(0, LIMITS[type])
  return mapLimited(ids, (id) => getJson(`${type}/${id}`))
}

const transformSpell = (data) => {
  const effects = []
  if (data.damage?.damage_at_slot_level) {
    const dice = Object.values(data.damage.damage_at_slot_level)[0]
    effects.push({ key: `daño ${dice}`, kind: 'custom', value: 0 })
  }
  if (effects.length === 0) {
    effects.push({ key: summaryOf(data).slice(0, 60), kind: 'custom', value: 0 })
  }
  const casting = (data.casting_time || '').toLowerCase()
  // `casting_time: "Action"` de la API 2024 → `activation: "action"`.
  const activation = casting.includes('bonus') ? 'bonus'
    : casting.includes('reaction') ? 'reaction'
    : casting.includes('action') ? 'action'
    : 'free'
  const rawDuration = (data.duration || '').toLowerCase()
  const duration = rawDuration.includes('concentration') ? 'concentration'
    : rawDuration.includes('round') ? `rounds:${parseInt(rawDuration, 10) || 1}`
    : 'instant'
  return {
    typeKey: 'ability',
    itemKey: `ability/${data.index}`,
    name: data.name,
    summary: summaryOf(data),
    description: descriptionOf(data),
    tags: ['spell', 'srd', '5.2.1'],
    properties: {
      activation,
      duration,
      resolution: 'manual',
      effects,
      costs: data.level > 0 ? [{ key: 'spellSlot', amount: data.level }] : undefined,
    },
  }
}

const transformEquipment = (data) => {
  const effects = []
  if (data.damage?.damage_dice) {
    effects.push({ key: `daño ${data.damage.damage_dice}`, kind: 'custom', value: 0 })
  }
  if (data.armor_class?.base) {
    effects.push({ key: `CA ${data.armor_class.base}`, kind: 'custom', value: data.armor_class.base })
  }
  return {
    typeKey: 'item',
    itemKey: `item/${data.index}`,
    name: data.name,
    summary: summaryOf(data),
    description: descriptionOf(data),
    tags: ['item', 'srd', '5.2.1'],
    properties: {
      weight: data.weight ?? 0,
      value: data.cost?.quantity ?? 0,
      rarity: 'common',
      effects: effects.length ? effects : [{ key: 'equipo de aventura', kind: 'custom', value: 0 }],
    },
  }
}

const transformCreature = (data) => {
  const entries = [
    ...(Array.isArray(data.special_abilities) ? data.special_abilities : []),
    ...(Array.isArray(data.actions) ? data.actions : []),
  ]
  // Texto literal de la API (nombre + descripción de cada rasgo/acción).
  const traits = entries.slice(0, 6).map((entry) => `${entry.name} — ${asText(entry.desc)}`)
  const description = traits.join('\n\n')
  return {
    typeKey: 'creature',
    itemKey: `creature/${data.index}`,
    name: data.name,
    summary: summaryOf({ description, name: data.name }),
    description,
    tags: ['creature', 'srd', '5.2.1'],
    properties: {
      armorClass: data.armor_class?.[0]?.value ?? 10,
      hitPoints: data.hit_points ?? 10,
      speed: parseFloat(data.speed?.walk) || 30,
      challengeRating: data.challenge_rating ?? 0,
      traits,
    },
  }
}

const [spells, monsters, equipment] = await Promise.all([
  fetchType('spells'),
  fetchType('monsters'),
  fetchType('equipment'),
])

const items = [
  ...spells.map(transformSpell),
  ...monsters.map(transformCreature),
  ...equipment.map(transformEquipment),
].sort((a, b) => compare(a.itemKey, b.itemKey))

const manifest = {
  pack: {
    key: 'dnd5e-srd-5.2',
    name: 'D&D 5e — System Reference Document 5.2.1',
    version: '5.2.1',
    system: 'dnd5e',
    sourceUrl: API,
    license: 'CC BY 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
    attribution: [
      'Wizards of the Coast, System Reference Document 5.2.1, licensed under Creative Commons Attribution 4.0 International (CC BY 4.0).',
      `Contenido extraído de ${API} (5e-database, CC BY 4.0).`,
    ],
  },
  types: [
    {
      key: 'ability',
      displayName: 'Ability',
      schema: {
        fields: [
          { key: 'activation', type: 'string' },
          { key: 'effects', type: 'list' },
          { key: 'duration', type: 'string' },
          { key: 'resolution', type: 'string' },
          { key: 'uses', type: 'object', required: false },
          { key: 'costs', type: 'list', required: false },
          { key: 'requirements', type: 'list', required: false },
          { key: 'choiceGroups', type: 'list', required: false },
        ],
      },
    },
    {
      key: 'item',
      displayName: 'Item',
      schema: {
        fields: [
          { key: 'weight', type: 'number', required: false },
          { key: 'volume', type: 'number', required: false },
          { key: 'value', type: 'number', required: false },
          { key: 'rarity', type: 'string', required: false },
          { key: 'effects', type: 'list', required: false },
          { key: 'container', type: 'object', required: false },
        ],
      },
    },
    {
      key: 'creature',
      displayName: 'Creature',
      schema: {
        fields: [
          { key: 'armorClass', type: 'number' },
          { key: 'hitPoints', type: 'number' },
          { key: 'speed', type: 'number' },
          { key: 'challengeRating', type: 'number' },
          { key: 'traits', type: 'list' },
        ],
      },
    },
  ],
  items,
}

writeFileSync(out, `${JSON.stringify(manifest, null, 2)}\n`)
console.log(`items: ${items.length}`)
console.log(JSON.stringify(items.reduce((acc, item) => {
  acc[item.typeKey] = (acc[item.typeKey] || 0) + 1
  return acc
}, {})))
