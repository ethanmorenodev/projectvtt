/**
 * Datos del prototipo del explorador de Biblioteca (PMB-4.1) — reflejan el
 * seed real `backend/Api/Content/Seed/dnd5e-srd-5.1.json` (43 items:
 * 9 razas + 9 subrazas con herencia, 12 clases, 13 trasfondos). Los nombres
 * y resúmenes son el original en inglés del SRD (traducción pendiente,
 * plan §16); la interfaz va en español. Solo desarrollo.
 */

export type ProtoType = 'race' | 'class' | 'background'

export const TYPES: { id: ProtoType; label: string; system: string }[] = [
  { id: 'race', label: 'Razas', system: 'dnd5e' },
  { id: 'class', label: 'Clases', system: 'dnd5e' },
  { id: 'background', label: 'Trasfondos', system: 'dnd5e' },
]

export type ProtoItem = {
  key: string
  name: string
  summary: string
  props: string[]
  parent?: string
  inheritance?: string
  scope?: 'campaña'
  status?: 'Borrador'
  /** Etiquetas: solo homebrew de muestra (el seed SRD real tiene `tags = []`). */
  tags?: string[]
  /** Usos de MUESTRA (PMB-4.5): la API aún no expone `usageCount`. */
  usages?: number
}

/** Fila de «Ver usos» (contrato aprobado PMB-4.5): sin snapshots. */
export type ProtoUsage = {
  actorName: string
  campaignName: string
  revisionNumber: number
  createdAt: string
}

export const ITEMS: ProtoItem[] = [
  { key: 'race/dwarf', name: 'Dwarf', summary: 'Bold and hardy, dwarves are known as skilled warriors, miners, and workers of stone and metal.', props: ['Velocidad: 25 ft', 'Tamaño: Medium', 'Bonificación: Constitution +2', 'Idiomas: Common, Dwarvish', 'Rasgos: 5'] },
  { key: 'race/elf', name: 'Elf', summary: 'Elves are a magical people of otherworldly grace, living in the world but not entirely part of it.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Dexterity +2', 'Idiomas: Common, Elvish', 'Rasgos: 5'] },
  { key: 'race/halfling', name: 'Halfling', summary: 'The comforts of home are the goals of most halflings’ lives: a place to settle in peace and quiet.', props: ['Velocidad: 25 ft', 'Tamaño: Small', 'Bonificación: Dexterity +2', 'Idiomas: Common, Halfling', 'Rasgos: 3'] },
  { key: 'race/human', name: 'Human', summary: 'Humans are the most adaptable and ambitious people among the common races.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: todos los atributos +1', 'Idiomas: Common + 1 a elegir'] },
  { key: 'race/dragonborn', name: 'Dragonborn', summary: 'Born of dragons, dragonborn walk proudly through a world that greets them with fearful incomprehension.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Strength +2, Charisma +1', 'Idiomas: Common, Draconic', 'Rasgos: 3'] },
  { key: 'race/gnome', name: 'Gnome', summary: 'A gnome’s energy and enthusiasm for living shines through every inch of their tiny bodies.', props: ['Velocidad: 25 ft', 'Tamaño: Small', 'Bonificación: Intelligence +2', 'Idiomas: Common, Gnomish', 'Rasgos: 2'] },
  { key: 'race/half-elf', name: 'Half-Elf', summary: 'Half-elves combine what some say are the best qualities of their elf and human parents.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Charisma +2, otra a elegir +1', 'Idiomas: Common, Elvish + 1', 'Rasgos: 3'] },
  { key: 'race/half-orc', name: 'Half-Orc', summary: 'Half-orcs combine human adaptability with the raw power of their orc ancestors.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Strength +2, Constitution +1', 'Idiomas: Common, Orc', 'Rasgos: 4'] },
  { key: 'race/tiefling', name: 'Tiefling', summary: 'Tieflings are derived from human bloodlines touched by the touch of the Lower Planes.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Charisma +2, Intelligence +1', 'Idiomas: Common, Infernal', 'Rasgos: 3'] },
  { key: 'race/dwarf/hill-dwarf', name: 'Hill Dwarf', summary: 'As a hill dwarf, you have keen senses, deep intuition, and remarkable resilience.', props: ['Velocidad: 25 ft', 'Tamaño: Medium', 'Bonificación: Wisdom +1', 'Rasgos: 1'], parent: 'race/dwarf', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/dwarf/mountain-dwarf', name: 'Mountain Dwarf', summary: 'As a mountain dwarf, you’re strong and hardy, accustomed to a difficult life in rugged terrain.', props: ['Velocidad: 25 ft', 'Tamaño: Medium', 'Bonificación: Strength +2', 'Rasgos: 1'], parent: 'race/dwarf', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/elf/high-elf', name: 'High Elf', summary: 'As a high elf, you have a keen mind and a mastery of at least the basics of magic.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Intelligence +1', 'Idiomas: +1 a elegir', 'Rasgos: 2'], parent: 'race/elf', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/elf/wood-elf', name: 'Wood Elf', summary: 'As a wood elf, you have keen senses and intuition, and your fleet feet carry you quickly and stealthily.', props: ['Velocidad: 35 ft', 'Tamaño: Medium', 'Bonificación: Wisdom +1', 'Rasgos: 2'], parent: 'race/elf', inheritance: 'abilityBonuses · traits · languages → extend; speed → override' },
  { key: 'race/elf/drow', name: 'Drow', summary: 'As a drow, you are infused with the magic of the Underdark.', props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Charisma +1', 'Rasgos: 4'], parent: 'race/elf', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/halfling/lightfoot', name: 'Lightfoot Halfling', summary: 'As a lightfoot, you can easily hide from notice, even using other people as cover.', props: ['Velocidad: 25 ft', 'Tamaño: Small', 'Bonificación: Charisma +1', 'Rasgos: 1'], parent: 'race/halfling', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/halfling/stout', name: 'Stout Halfling', summary: 'As a stout, you’re hardier than average and have some resistance to poison.', props: ['Velocidad: 25 ft', 'Tamaño: Small', 'Bonificación: Constitution +1', 'Rasgos: 1'], parent: 'race/halfling', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/gnome/forest-gnome', name: 'Forest Gnome', summary: 'As a forest gnome, you have a natural knack for illusion and inherent quickness and stealth.', props: ['Velocidad: 25 ft', 'Tamaño: Small', 'Bonificación: Dexterity +1', 'Rasgos: 2'], parent: 'race/gnome', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'race/gnome/rock-gnome', name: 'Rock Gnome', summary: 'As a rock gnome, you have a natural inventiveness and hardiness beyond that of other gnomes.', props: ['Velocidad: 25 ft', 'Tamaño: Small', 'Bonificación: Constitution +1', 'Rasgos: 2'], parent: 'race/gnome', inheritance: 'abilityBonuses · traits · languages → extend' },
  { key: 'class/barbarian', name: 'Barbarian', summary: 'For some, their rage springs from a communion with fierce animal spirits. For others, it is an unquenchable rage.', props: ['Dado de vida: d12', 'Salvaciones: Strength, Constitution', 'Atributo principal: Strength', 'Habilidades a elegir: 2'] },
  { key: 'class/bard', name: 'Bard', summary: 'An adventurer whose voice weaves magic, inspiring allies and disheartening foes.', props: ['Dado de vida: d8', 'Salvaciones: Dexterity, Charisma', 'Atributo principal: Charisma', 'Habilidades a elegir: 3'] },
  { key: 'class/cleric', name: 'Cleric', summary: 'A priestly champion who wields divine magic in service of a higher power.', props: ['Dado de vida: d8', 'Salvaciones: Wisdom, Charisma', 'Atributo principal: Wisdom', 'Habilidades a elegir: 2'] },
  { key: 'class/druid', name: 'Druid', summary: 'A priest of the Old Faith, wielding the powers of nature and adopting animal forms.', props: ['Dado de vida: d8', 'Salvaciones: Intelligence, Wisdom', 'Atributo principal: Wisdom', 'Habilidades a elegir: 2'] },
  { key: 'class/fighter', name: 'Fighter', summary: 'A master of martial combat, skilled with a variety of weapons and armor.', props: ['Dado de vida: d10', 'Salvaciones: Strength, Constitution', 'Atributo principal: Strength o Dexterity', 'Habilidades a elegir: 2'] },
  { key: 'class/monk', name: 'Monk', summary: 'A master of martial arts, harnessing the power of the body in pursuit of spiritual perfection.', props: ['Dado de vida: d8', 'Salvaciones: Strength, Dexterity', 'Atributo principal: Dexterity o Wisdom', 'Habilidades a elegir: 2'] },
  { key: 'class/paladin', name: 'Paladin', summary: 'A holy warrior bound to a sacred oath, wielding divine might against the forces of evil.', props: ['Dado de vida: d10', 'Salvaciones: Wisdom, Charisma', 'Atributo principal: Strength o Charisma', 'Habilidades a elegir: 2'] },
  { key: 'class/ranger', name: 'Ranger', summary: 'A warrior of the wilderness, skilled in tracking and survival.', props: ['Dado de vida: d10', 'Salvaciones: Strength, Dexterity', 'Atributo principal: Dexterity o Wisdom', 'Habilidades a elegir: 3'] },
  { key: 'class/rogue', name: 'Rogue', summary: 'A scoundrel who uses stealth, trickery and skill to overcome obstacles and enemies.', props: ['Dado de vida: d8', 'Salvaciones: Dexterity, Intelligence', 'Atributo principal: Dexterity', 'Habilidades a elegir: 4'] },
  { key: 'class/sorcerer', name: 'Sorcerer', summary: 'A spellcaster who draws on inherent magic from a gift or bloodline.', props: ['Dado de vida: d6', 'Salvaciones: Constitution, Charisma', 'Atributo principal: Charisma', 'Habilidades a elegir: 2'] },
  { key: 'class/warlock', name: 'Warlock', summary: 'A wielder of magic derived from a bargain with an extraplanar entity.', props: ['Dado de vida: d8', 'Salvaciones: Wisdom, Charisma', 'Atributo principal: Charisma', 'Habilidades a elegir: 2'] },
  { key: 'class/wizard', name: 'Wizard', summary: 'A scholarly magic-user capable of manipulating the structures of reality.', props: ['Dado de vida: d6', 'Salvaciones: Intelligence, Wisdom', 'Atributo principal: Intelligence', 'Habilidades a elegir: 2'] },
  { key: 'background/acolyte', name: 'Acolyte', summary: 'You have spent your life in the service of a temple to a specific god or pantheon of gods.', props: ['Rasgo: Shelter of the Faithful', 'Pericias: Insight, Religion', 'Idiomas a elegir: 2'] },
  { key: 'background/charlatan', name: 'Charlatan', summary: 'You have always had a way with people, using charm and deception to get what you want.', props: ['Rasgo: False Identity', 'Pericias: Deception, Sleight of Hand', 'Herramientas: Disguise kit, Forgery kit'] },
  { key: 'background/criminal', name: 'Criminal', summary: 'You are an experienced criminal with a history of breaking the law and contacts in the underworld.', props: ['Rasgo: Criminal Contact', 'Pericias: Deception, Stealth', 'Herramientas: One type of gaming set, Thieves’ tools'] },
  { key: 'background/entertainer', name: 'Entertainer', summary: 'You thrive in front of an audience, knowing how to entrance, entertain and inspire them.', props: ['Rasgo: By Popular Demand', 'Pericias: Acrobatics, Performance', 'Herramientas: Disguise kit, One musical instrument'] },
  { key: 'background/folk-hero', name: 'Folk Hero', summary: 'You come from a humble social rank, but you are destined for so much more through a defining event.', props: ['Rasgo: Rustic Hospitality', 'Pericias: Animal Handling, Survival', 'Herramientas: Artisan’s tools, Vehicles (land)'] },
  { key: 'background/guild-artisan', name: 'Guild Artisan', summary: 'You are a member of an artisan’s guild, skilled in a particular field and closely connected to other artisans.', props: ['Rasgo: Guild Membership', 'Pericias: Insight, Persuasion', 'Idiomas a elegir: 1', 'Herramientas: Artisan’s tools'] },
  { key: 'background/hermit', name: 'Hermit', summary: 'You lived in seclusion for a formative part of your life, finding quiet and solitude.', props: ['Rasgo: Discovery', 'Pericias: Medicine, Religion', 'Idiomas a elegir: 1', 'Herramientas: Herbalism kit'] },
  { key: 'background/noble', name: 'Noble', summary: 'You understand wealth, power and privilege, born into a family of wealth and title.', props: ['Rasgo: Position of Privilege', 'Pericias: History, Persuasion', 'Idiomas a elegir: 1', 'Herramientas: One type of gaming set'] },
  { key: 'background/outlander', name: 'Outlander', summary: 'You grew up in the wilds, far from civilization and the comforts of town and technology.', props: ['Rasgo: Wanderer', 'Pericias: Athletics, Survival', 'Idiomas a elegir: 1', 'Herramientas: One musical instrument'] },
  { key: 'background/sage', name: 'Sage', summary: 'You spent years learning the lore of the multiverse, studying tomes of ancient knowledge.', props: ['Rasgo: Researcher', 'Pericias: Arcana, History', 'Idiomas a elegir: 2'] },
  { key: 'background/sailor', name: 'Sailor', summary: 'You sailed on a seagoing vessel, whether as a sailor or as a passenger.', props: ['Rasgo: Ship’s Passage', 'Pericias: Athletics, Perception', 'Herramientas: Navigator’s tools, Vehicles (water)'] },
  { key: 'background/soldier', name: 'Soldier', summary: 'War has been your life for as long as you care to remember, trained in the arts of battle.', props: ['Rasgo: Military Rank', 'Pericias: Athletics, Intimidation', 'Herramientas: One type of gaming set, Vehicles (land)'] },
  { key: 'background/urchin', name: 'Urchin', summary: 'You grew up on the streets alone, orphaned and poor, surviving by your wits.', props: ['Rasgo: City Secrets', 'Pericias: Sleight of Hand, Stealth', 'Herramientas: Disguise kit, Thieves’ tools'] },
]

/** Homebrew de campaña solo en la variante «extremo» (nombre largo + borrador). */
export const HOMEBREW_EXTREMO: ProtoItem = {
  key: 'race/guardia-ciudadela',
  name: 'Guardia de élite de la ciudadela de Scaldar con armadura ceremonial',
  summary: 'Variante homebrew de campaña: elite de la guardia portuaria con equipo ceremonial (borrador del DM).',
  props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Constitution +2', 'Rasgos: 2'],
  scope: 'campaña',
  status: 'Borrador',
}

/**
 * Items homebrew de muestra para la variante «acciones» (PMB-4.5): etiquetas
 * y conteos de uso ficticios (el seed SRD real no tiene ninguno de los dos).
 * Solo se usan en `#prototipo-biblioteca-acciones`.
 */
export const HOMEBREW_ACCIONES: ProtoItem[] = [
  {
    key: 'race/guardia-scaldar',
    name: 'Guardia de Scaldar',
    summary: 'Variante homebrew de campaña: soldado de la guardia portuaria con entrenamiento en escudos.',
    props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Strength +2', 'Rasgos: 2'],
    scope: 'campaña',
    tags: ['guardia', 'Scaldar', 'militar'],
    usages: 4,
  },
  {
    key: 'race/guardia-scaldar/veterana',
    name: 'Veterana de la guardia',
    summary: 'Subvariante homebrew: veteranía con escudo pesado y estandarte de la ciudadela.',
    props: ['Velocidad: 30 ft', 'Tamaño: Medium', 'Bonificación: Constitution +1', 'Rasgos: 1'],
    parent: 'race/guardia-scaldar',
    inheritance: 'traits · languages → extend',
    scope: 'campaña',
    status: 'Borrador',
    tags: ['guardia', 'Scaldar'],
    usages: 1,
  },
  {
    key: 'background/pirata-escarlata',
    name: 'Pirata de la Escarlata',
    summary: 'Trasfondo homebrew de campaña: contrabando en las aguas de Scaldar.',
    props: ['Rasgo: Tripulación leal', 'Pericias: Athletics, Perception'],
    scope: 'campaña',
    tags: ['piratas', 'Scaldar'],
    usages: 0,
  },
]

/** Usos de MUESTRA por item (PMB-4.5): forma del contrato aprobado, sin snapshots. */
export const USAGE_SAMPLE: Record<string, ProtoUsage[]> = {
  'race/guardia-scaldar': [
    { actorName: 'Kaelen Piedra Alta', campaignName: 'La Corona Rota', revisionNumber: 3, createdAt: '2026-10-12T18:40:00Z' },
    { actorName: 'Mira Colmillo', campaignName: 'La Corona Rota', revisionNumber: 2, createdAt: '2026-10-28T09:15:00Z' },
    { actorName: 'Sable Errante', campaignName: 'Niebla sobre Vardes', revisionNumber: 1, createdAt: '2026-11-02T21:05:00Z' },
    { actorName: 'Thalor Vistalarga', campaignName: 'La Corona Rota', revisionNumber: 3, createdAt: '2026-11-05T12:30:00Z' },
  ],
  'race/guardia-scaldar/veterana': [
    { actorName: 'Kaelen Piedra Alta', campaignName: 'La Corona Rota', revisionNumber: 1, createdAt: '2026-11-06T19:20:00Z' },
  ],
}

/**
 * Relaciones de MUESTRA para el prototipo de relaciones tipadas (PMB-4.6):
 * el seed SRD real no incluye ninguna. Los `kind` son los documentados en
 * CONTENT_MODEL §8 — el servidor validará los permitidos por sistema/tipo.
 */
export type ProtoRelation = {
  from: string
  kind: 'grants' | 'requires' | 'excludes' | 'memberOf' | 'locatedIn' | 'leadsTo' | 'owns'
  to: string
  label?: string
}

export const RELATION_KINDS: ProtoRelation['kind'][] = [
  'grants', 'requires', 'excludes', 'memberOf', 'locatedIn', 'leadsTo', 'owns',
]

export const RELATIONS: ProtoRelation[] = [
  { from: 'race/dwarf', kind: 'grants', to: 'class/fighter', label: 'Tradición marcial enana' },
  { from: 'class/wizard', kind: 'requires', to: 'background/sage', label: 'Trasfondo arcano recomendado' },
  { from: 'background/soldier', kind: 'leadsTo', to: 'class/fighter' },
  { from: 'race/elf/high-elf', kind: 'memberOf', to: 'race/elf' },
  { from: 'background/criminal', kind: 'leadsTo', to: 'race/dwarf', label: 'Contactos enanos' },
]

/** Paquete de procedencia real (seed). */
export const PACK = {
  name: 'D&D 5e — System Reference Document 5.1',
  key: 'dnd5e-srd-5.1',
  version: '5.1',
  license: 'CC BY 4.0',
  licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
  attribution:
    'Wizards of the Coast, System Reference Document 5.1 (2016), licensed under Creative Commons Attribution 4.0 International (CC BY 4.0).',
}
