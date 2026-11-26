/**
 * Tipos de contrato del cliente web. Deben mantenerse alineados con los
 * DTO del backend (backend/Api…/Dtos.cs y Realtime/Contracts.cs).
 * El JSON llega en camelCase con enums como texto.
 */

export type CampaignRole = 'Player' | 'GM' | 'Owner';

/** Forma del grid de una escena (ADR-023). */
export type GridShape = 'square' | 'hex';

export interface UserDto {
  id: string;
  userName: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: UserDto;
}

export interface CampaignSummary {
  id: string;
  name: string;
  role: CampaignRole;
  createdAt: string;
  archivedAt: string | null;
  description: string | null;
  hasCover: boolean;
  lastActivityAt: string | null;
  memberCount: number;
}

export interface CampaignDetail {
  id: string;
  name: string;
  role: CampaignRole;
  createdAt: string;
  archivedAt: string | null;
  memberCount: number;
  description: string | null;
  hasCover: boolean;
}

/** Actividad reciente multi-campaña (PMB-2.2, pantalla de Inicio). */
export interface ActivityEntry {
  campaignId: string;
  campaignName: string;
  userName: string;
  kind: string;
  summary: string;
  createdAt: string;
}

/** Invitación de campaña (código canjeable). */
export interface InviteInfo {
  id: string;
  code: string;
  role: CampaignRole;
  createdAt: string;
  expiresAt: string;
  revokedAt: string | null;
}

/** Ruleset disponible en el servidor (GET /rulesets). */
export interface RulesetInfo {
  key: string;
  displayName: string;
  attributeKeys: string[];
}

export interface SceneSummary {
  id: string;
  campaignId: string;
  name: string;
  gridCellSize: number;
  gridShape: GridShape;
  createdAt: string;
  tokenCount: number;
  hasCover: boolean;
}

export interface SceneDetail {
  id: string;
  campaignId: string;
  name: string;
  gridCellSize: number;
  gridShape: GridShape;
  createdAt: string;
  hasCover: boolean;
}

export interface TokenDto {
  id: string;
  sceneId: string;
  name: string;
  x: number;
  y: number;
  version: number;
  updatedByUserId: string | null;
  updatedAt: string;
  actorId: string | null;
}

export interface SceneState {
  sceneId: string;
  campaignId: string;
  sceneName: string;
  gridCellSize: number;
  gridShape: GridShape;
  tokens: TokenDto[];
}

export interface MoveTokenCommand {
  commandId: string;
  sceneId: string;
  tokenId: string;
  expectedVersion: number;
  x: number;
  y: number;
}

export interface MoveTokenResult {
  commandId: string;
  tokenId: string;
  version: number;
  x: number;
  y: number;
  duplicate: boolean;
}

export interface TokenMovedEvent {
  eventId: string;
  campaignId: string;
  sceneId: string;
  tokenId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  aggregateVersion: number;
  x: number;
  y: number;
}

export interface TokenCreatedEvent {
  eventId: string;
  campaignId: string;
  sceneId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  token: TokenDto;
}

// --- Actores y tiradas (Épica E) ---------------------------------------

export interface ResourceValue {
  current: number;
  max: number;
}

export interface ActorDto {
  id: string;
  campaignId: string;
  name: string;
  rulesetKey: string;
  attributes: Record<string, number>;
  resources: Record<string, ResourceValue>;
  version: number;
  createdByUserId: string | null;
  createdAt: string;
  updatedAt: string;
  // Ampliación de personaje (PMB-3.3): aditivo.
  kind: string;
  portraitId: string | null;
  biography: string | null;
  personality: string | null;
  level: number;
  systemData: Record<string, unknown> | null;
  /** Borrador del creador (PMB-3.5): presente = "continuar borrador". */
  build: Record<string, unknown> | null;
}

/** Contribución de un efecto reconocido a una tirada (ADR-015). */
export interface RollEffect {
  name: string;
  kind: 'advantage' | 'disadvantage' | 'modifier';
  value: number;
}

export interface RollBreakdown {
  rulesetKey: string;
  kind: string;
  attribute: string;
  attributeValue: number;
  die: number;
  modifier: number;
  extra: number;
  effectModifier: number;
  dice: number[];
  rollMode: 'normal' | 'advantage' | 'disadvantage';
  effects: RollEffect[];
  total: number;
  dc: number | null;
  success: boolean | null;
  formula: string;
}

export interface RollDto {
  rollId: string;
  campaignId: string;
  actorId: string;
  attribute: string;
  breakdown: RollBreakdown;
  rolledByUserId: string;
  occurredAt: string;
}

export interface RollResult {
  roll: RollDto;
  duplicate: boolean;
}

export interface ActorCreatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  actor: ActorDto;
}

export interface ActorUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  aggregateVersion: number;
  actor: ActorDto;
}

export interface RollResolvedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  roll: RollDto;
}

// --- Inventario (Épica E) ----------------------------------------------

export interface ActorItemDto {
  id: string;
  actorId: string;
  name: string;
  quantity: number;
  version: number;
  createdAt: string;
  updatedAt: string;
  /** Hueco de equipamiento (ADR-041): null = mochila. */
  slotId: string | null;
  contentItemId: string | null;
  weight: number | null;
  volume: number | null;
}

/** Grafo y cronología de lore (PMB-7.3). */
export interface LoreNodeDto {
  id: string;
  name: string;
  typeKey: string;
  summary: string | null;
  isPrivate: boolean;
}

export interface LoreEdgeDto {
  id: string;
  fromItemId: string;
  kind: string;
  toItemId: string;
  label: string | null;
}

export interface LoreGraphDto {
  nodes: LoreNodeDto[];
  edges: LoreEdgeDto[];
}

export interface LoreEventDto {
  id: string;
  name: string;
  summary: string | null;
  happenedAt: string | null;
  order: number;
}

/** Hueco de equipamiento declarado por el blueprint del sistema (ADR-041). */
export interface EquipmentSlotDto {
  id: string;
  label: string;
  icon: string | null;
  accepts: string[];
  optional: boolean;
  multiple: boolean;
}

export interface ActorInventoryDto {
  actorId: string;
  items: ActorItemDto[];
}

export interface InventoryUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  actorId: string;
  items: ActorItemDto[];
}

// --- Efectos y condiciones (Épica E) ------------------------------------

export interface ActorEffectDto {
  id: string;
  actorId: string;
  name: string;
  createdAt: string;
}

export interface ActorEffectsDto {
  actorId: string;
  effects: ActorEffectDto[];
}

export interface EffectsUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  actorId: string;
  effects: ActorEffectDto[];
}

// --- Chat de campaña (Épica F) -----------------------------------------

export interface ChatMessageDto {
  id: string;
  campaignId: string;
  userId: string;
  userName: string;
  body: string;
  createdAt: string;
}

export interface MessagePostedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  message: ChatMessageDto;
}

// --- Mensajes privados (Épica F, ADR-017) ------------------------------

export interface CampaignMember {
  userId: string;
  userName: string;
  role: CampaignRole;
  joinedAt: string;
}

export interface DirectMessageDto {
  id: string;
  campaignId: string;
  fromUserId: string;
  fromUserName: string;
  toUserId: string;
  toUserName: string;
  body: string;
  createdAt: string;
}

export interface DirectMessagePostedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  message: DirectMessageDto;
}

// --- Combate e iniciativa (Épica F, ADR-018) ---------------------------

export interface CombatantDto {
  id: string;
  sceneId: string;
  name: string;
  initiative: number;
  tokenId: string | null;
  createdAt: string;
}

export interface CombatStateDto {
  sceneId: string;
  active: boolean;
  round: number;
  currentCombatantId: string | null;
  combatants: CombatantDto[];
}

export interface CombatUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  combat: CombatStateDto;
}

// --- Notas privadas del GM (Épica F, ADR-019) --------------------------

export interface NoteDto {
  id: string;
  campaignId: string;
  title: string;
  body: string;
  createdAt: string;
  updatedAt: string;
}

// --- Niebla de guerra manual (Épica F, ADR-020) ------------------------

export interface FogCellDto {
  x: number;
  y: number;
}

export interface FogStateDto {
  sceneId: string;
  gridCellSize: number;
  revealed: FogCellDto[];
}

export interface FogUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  fog: FogStateDto;
}

// --- Historial de cambios (Épica F, ADR-021) ---------------------------

export interface HistoryEntryDto {
  id: string;
  campaignId: string;
  userId: string;
  userName: string;
  kind: string;
  summary: string;
  createdAt: string;
}

// --- Mapa persistido (ADR-028) -----------------------------------------

export interface MapAssetDto {
  sceneId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  updatedAt: string;
}

export interface MapUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  sceneId: string;
  map: MapAssetDto | null;
}

// --- Creador de personajes (PMB-3.2/3.5, ADR-034) -----------------------

export type BlueprintStepKind =
  | 'identity'
  | 'choice'
  | 'attributes'
  | 'equipment'
  | 'text'
  | 'review';

export interface BlueprintStepDef {
  key: string;
  title: string;
  kind: BlueprintStepKind;
  contentType?: string;
  fields?: string[];
  /** Opciones del sistema para el paso (p. ej. habilidades 5e). */
  options?: string[];
  validation?: Record<string, unknown>;
  optional?: boolean;
  skippable?: boolean;
}

export interface CharacterBlueprintDto {
  id: string;
  system: string;
  key: string;
  name: string;
  steps: BlueprintStepDef[];
  derivedStats: string[];
  startingEquipmentRules: unknown;
}

export interface BuildStepResult {
  step: string;
  ok: boolean;
  errors: string[];
}

export interface DerivedAbility {
  score: number;
  modifier: number;
}

export interface BuildPreviewResponse {
  valid: boolean;
  stepResults: BuildStepResult[];
  derivedStats: {
    level: number;
    abilities: Record<string, DerivedAbility>;
    speed: number;
    size: string;
    languages: string[];
    pendingBonuses: Array<{ ability: string; bonus: number; applied: boolean }>;
  };
}

/** Item de contenido (ADR-031) visible para el usuario actual. */
export interface ContentItemDto {
  id: string;
  system: string;
  typeId: string;
  typeKey: string;
  name: string;
  summary: string | null;
  description: string | null;
  parentId: string | null;
  tags: string[];
  imageId: string | null;
  focal: string | null;
  properties: Record<string, unknown>;
  inheritance: Record<string, string>;
  scope: string;
  campaignId: string | null;
  workflowState: string;
  currentRevision: number;
  createdByUserId: string | null;
  createdAt: string;
  updatedAt: string;
  /** Procedencia (ADR-036): paquete de origen, si lo hay. */
  packKey: string | null;
  /** Clave del item dentro de su paquete (identidad importada). */
  packItemKey: string | null;
  /** «Solo DM» (ADR-031 §6): hacia quien no puede ver el item no llega. */
  isPrivate: boolean;
  /** Claves de campos privados: solo hacia quien recibió sus valores. */
  privateFields: string[];
  /**
   * Usos visibles para el solicitante (PMB-4.5, aditivo): solo de campañas
   * donde es miembro, la misma regla de `GET /content/items/{id}/usages`.
   */
  usageCount: number;
}

/**
 * Resumen de «Ver usos» (PMB-4.5, aditivo): sin snapshot; solo de campañas
 * donde el solicitante es miembro.
 */
export interface ItemUsageSummaryDto {
  actorName: string;
  campaignName: string;
  revisionNumber: number;
  createdAt: string;
}

/** Uso de contenido por un personaje, con snapshot materializado. */
export interface ItemUsageDto {
  id: string;
  actorId: string;
  itemId: string;
  revisionNumber: number;
  snapshot: unknown;
  createdAt: string;
}

/** Tipo de contenido (ADR-031 §3): los tipos son datos. */
export interface ContentTypeDto {
  id: string;
  system: string;
  key: string;
  displayName: string;
  schema: {
    fields?: Array<{ key: string; type: string; required?: boolean }>;
    /** Kinds de relación permitidos por el sistema/tipo (PMB-4.6). */
    allowedRelations?: string[];
  } | null;
  createdAt: string;
}

/** Relación tipada entre items (PMB-4.6, aditivo aprobado 2026-11-16). */
export interface ContentRelationDto {
  id: string;
  fromItemId: string;
  kind: string;
  toItemId: string;
  label: string | null;
  metadata: unknown | null;
}

/** Propuesta pendiente de revisión del DM (PMB-4.8). */
export interface ContentProposalDto {
  id: string;
  name: string;
  authorName: string;
  workflowState: string;
  currentRevision: number;
  changes: string[];
  updatedAt: string;
}

/** Permiso explícito sobre un item (ADR-031 §6). */
export interface ContentGrantDto {
  id: string;
  itemId: string;
  subjectKind: string;
  subjectId: string;
  permission: string;
}

/** Imagen de la biblioteca (PMB-4.7, ADR-035/036). */
export interface ImageAssetDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  role: string;
  albums: string[];
  tags: string[];
  license: string;
  licenseUrl: string | null;
  author: string;
  origin: string;
  attribution: string[];
  focal: string | null;
  packKey: string | null;
  createdAt: string;
  /** Referencias desde items (`ContentItem.ImageId`) y variantes (PMB-4.10). */
  referenceCount: number;
  variants: string[];
}

/** Paquete importado con su procedencia y licencia (ADR-036). */
export interface ContentPackDto {
  id: string;
  key: string;
  name: string;
  version: string;
  system: string;
  sourceUrl: string | null;
  license: string;
  licenseUrl: string | null;
  attribution: string[];
  importedAt: string;
}

/** Revisión inmutable publicada (ADR-031 §7). */
export interface ContentRevisionDto {
  id: string;
  number: number;
  payload: Record<string, unknown> | null;
  message: string | null;
  createdByUserId: string | null;
  createdAt: string;
}

// --- Anotaciones compartidas de escena (ADR-043) ------------------------

export interface SceneAnnotationPointDto {
  x: number;
  y: number;
}

export interface SceneAnnotationDto {
  id: string;
  sceneId: string;
  authorUserId: string;
  kind: 'draw' | 'note';
  points: SceneAnnotationPointDto[];
  text: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

/** Estado completo de las anotaciones de una escena (HTTP y evento). */
export interface SceneAnnotationsState {
  sceneId: string;
  items: SceneAnnotationDto[];
  version: number;
}

/** Evento `SceneAnnotationsUpdated`: envelope estándar (ARCHITECTURE §7). */
export interface SceneAnnotationsUpdatedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  annotations: SceneAnnotationsState;
}

/** Evento `SceneDeleted`: envelope estándar (ARCHITECTURE §7, ADR-045). */
export interface SceneDeletedEvent {
  eventId: string;
  campaignId: string;
  actorUserId: string;
  occurredAt: string;
  schemaVersion: number;
  sceneId: string;
}

/** Comando `SetSceneAnnotation`: `kind` no se necesita con `clear`. */
export interface SetSceneAnnotationRequest {
  commandId: string;
  expectedVersion: number;
  annotationId?: string | null;
  /** No se necesita cuando clear=true. */
  kind?: 'draw' | 'note' | null;
  points?: SceneAnnotationPointDto[];
  text?: string | null;
  clear: boolean;
}
