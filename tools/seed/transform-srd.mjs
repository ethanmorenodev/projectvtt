/**
 * Genera `backend/Api/Content/Seed/dnd5e-srd-5.1-content.json` a partir de
 * la descarga de https://www.dnd5eapi.co/ (SRD 5.1, CC BY 4.0).
 *
 * Uso:
 *   1. Descargar cada índice de spells/equipment/monsters a `tools/seed/cache/`
 *      como `spells-<index>.json`, `items-<index>.json`, `monsters-<index>.json`.
 *   2. `node tools/seed/transform-srd.mjs`
 *
 * El manifiesto usa la misma clave de paquete que `dnd5e-srd-5.1.json`
 * (el loader es idempotente: find-or-create por (system, key) de tipo y por
 * (packKey, packItemKey) de ítem).
 */
import { readFileSync, writeFileSync, readdirSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const cache = join(here, 'cache')
const out = join(here, '../../backend/Api/Content/Seed/dnd5e-srd-5.1-content.json')

const load = (name) => JSON.parse(readFileSync(join(cache, name), 'utf8'))
const firstSentence = (arr) => (Array.isArray(arr) ? arr[0] : arr || '')

const items = []

for (const file of readdirSync(cache)) {
  if (!file.endsWith('.json')) continue
  const data = load(file)
  const slug = data.index
  const summary = firstSentence(data.desc || data.description || [data.name])
  const description = Array.isArray(data.desc) ? data.desc.join('\n\n') : (data.description || '')

  if (file.startsWith('spells-')) {
    const effects = []
    if (data.damage?.damage_at_slot_level) {
      const dice = Object.values(data.damage.damage_at_slot_level)[0]
      effects.push({ key: `daño ${dice}`, kind: 'custom', value: 0 })
    }
    if (data.heal_at_slot_level) {
      const heal = Object.values(data.heal_at_slot_level)[0]
      effects.push({ key: `curación ${heal}`, kind: 'custom', value: 0 })
    }
    if (effects.length === 0) {
      effects.push({ key: summary.slice(0, 60), kind: 'custom', value: 0 })
    }
    const casting = (data.casting_time || '').toLowerCase()
    const activation = casting.includes('bonus') ? 'bonus'
      : casting.includes('reaction') ? 'reaction'
      : casting.includes('action') ? 'action'
      : 'free'
    const rawDuration = (data.duration || '').toLowerCase()
    const duration = rawDuration.includes('concentration') ? 'concentration'
      : rawDuration.includes('round') ? `rounds:${parseInt(rawDuration, 10) || 1}`
      : 'instant'
    items.push({
      typeKey: 'ability',
      itemKey: `ability/${slug}`,
      name: data.name,
      summary,
      description,
      tags: ['spell', 'srd'],
      properties: {
        activation,
        duration,
        resolution: 'manual',
        effects,
        costs: data.level > 0 ? [{ key: 'spellSlot', amount: data.level }] : undefined,
      },
    })
  } else if (file.startsWith('items-')) {
    const effects = []
    if (data.damage?.damage_dice) {
      effects.push({ key: `daño ${data.damage.damage_dice}`, kind: 'custom', value: 0 })
    }
    if (data.armor_class?.base) {
      effects.push({ key: `CA ${data.armor_class.base}`, kind: 'custom', value: data.armor_class.base })
    }
    items.push({
      typeKey: 'item',
      itemKey: `item/${slug}`,
      name: data.name,
      summary,
      description,
      tags: ['equipment', 'srd'],
      properties: {
        weight: data.weight ?? 0,
        value: data.cost?.quantity ?? 0,
        rarity: 'common',
        effects: effects.length ? effects : [{ key: 'equipo de aventura', kind: 'custom', value: 0 }],
      },
    })
  } else if (file.startsWith('monsters-')) {
    const traits = []
    if (Array.isArray(data.special_abilities)) {
      for (const trait of data.special_abilities) {
        traits.push(`${trait.name} — ${firstSentence([trait.desc])}`)
      }
    }
    if (Array.isArray(data.actions)) {
      for (const action of data.actions) {
        traits.push(`${action.name} — ${firstSentence([action.desc])}`)
      }
    }
    items.push({
      typeKey: 'creature',
      itemKey: `creature/${slug}`,
      name: data.name,
      summary,
      description,
      tags: ['creature', 'srd'],
      properties: {
        armorClass: data.armor_class?.[0]?.value ?? 10,
        hitPoints: data.hit_points ?? 10,
        speed: parseFloat(data.speed?.walk || '30') || 30,
        challengeRating: data.challenge_rating ?? 0,
        traits: traits.slice(0, 6),
      },
    })
  }
}

const manifest = {
  pack: {
    key: 'dnd5e-srd-5.1',
    name: 'D&D 5e — System Reference Document 5.1',
    version: '5.1',
    system: 'dnd5e',
    sourceUrl: 'https://www.dndbeyond.com/sources/dnd/srd-5-1',
    license: 'CC BY 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
    attribution: [
      'Wizards of the Coast, System Reference Document 5.1 (2016), licensed under Creative Commons Attribution 4.0 International (CC BY 4.0).',
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

writeFileSync(out, JSON.stringify(manifest, null, 2))
console.log(`items: ${items.length}`)
const byType = items.reduce((acc, it) => { acc[it.typeKey] = (acc[it.typeKey] || 0) + 1; return acc }, {})
console.log(JSON.stringify(byType))
