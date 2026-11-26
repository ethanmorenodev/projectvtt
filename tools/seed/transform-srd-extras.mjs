/**
 * Genera `backend/Api/Content/Seed/dnd5e-srd-5.1-extras.json` con las
 * condiciones y subclases del SRD 5.1 (CC BY 4.0) desde dnd5eapi.co.
 *
 * Uso: `node tools/seed/transform-srd-extras.mjs`
 *
 * Usa el mismo paquete que los otros manifiestos del SRD; el loader es
 * idempotente (find-or-create por (system, key) de tipo y por
 * (packKey, packItemKey) de item).
 */
import { writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const out = join(here, '../../backend/Api/Content/Seed/dnd5e-srd-5.1-extras.json')
const base = 'https://www.dnd5eapi.co/api'

const get = async (path) => {
  const response = await fetch(`${base}${path}`)
  if (!response.ok) {
    throw new Error(`${path} -> HTTP ${response.status}`)
  }
  return response.json()
}

const text = (value) => (Array.isArray(value) ? value.join('\n') : String(value ?? ''))
const first = (value) =>
  (Array.isArray(value) ? value[0] : value || '')
    .replace(/^[-*]\s*/, '')
    .trim()

const items = []

const conditions = await get('/conditions')
for (const entry of conditions.results) {
  // La lista devuelve URLs /api/2014/... que no sirven aquí; se reconstruye
  // la ruta con el índice (igual que en el transformador principal).
  const data = await get(`/conditions/${entry.index}`)
  items.push({
    typeKey: 'condition',
    itemKey: `condition/${data.index}`,
    name: data.name,
    summary: first(data.desc).slice(0, 200),
    description: text(data.desc),
    tags: ['condition', 'srd'],
    properties: { description: data.desc ?? [] },
  })
}

const subclasses = await get('/subclasses')
for (const entry of subclasses.results) {
  const data = await get(`/subclasses/${entry.index}`)
  const className = data.class?.name ?? 'Clase'
  const flavor = data.subclass_flavor ?? 'Arquetipo'
  items.push({
    typeKey: 'subclass',
    itemKey: `subclass/${data.index}`,
    name: data.name,
    summary: `${flavor} · ${className}`,
    description: text(data.desc) || `${data.name} — ${flavor} del ${className}.`,
    tags: ['subclass', 'srd'],
    properties: {
      classKey: data.class?.index ?? '',
      features: (data.features ?? []).map((feature) => feature.name),
    },
  })
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
      'Condiciones y subclases transcritas desde dnd5eapi.co (SRD 5.1, CC BY 4.0).',
    ],
  },
  types: [
    {
      key: 'condition',
      displayName: 'Condición',
      schema: {
        fields: [{ key: 'description', type: 'list' }],
      },
    },
    {
      key: 'subclass',
      displayName: 'Subclase',
      schema: {
        fields: [
          { key: 'classKey', type: 'string' },
          { key: 'features', type: 'list' },
        ],
      },
    },
  ],
  items,
}

writeFileSync(out, JSON.stringify(manifest, null, 2))
console.log(`items: ${items.length}`)
const byType = items.reduce((acc, item) => {
  acc[item.typeKey] = (acc[item.typeKey] || 0) + 1
  return acc
}, {})
console.log(JSON.stringify(byType))
