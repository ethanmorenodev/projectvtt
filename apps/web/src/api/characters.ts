import { apiFetch } from './client'
import type {
  ActorDto,
  BuildPreviewResponse,
  CampaignDetail,
  CharacterBlueprintDto,
  ContentItemDto,
} from './types'

/**
 * Creador de personajes (PMB-3.5, ADR-034): consume el blueprint por
 * sistema y el endpoint de previsualización — las reglas viven solo en el
 * servidor; aquí solo se recogen elecciones y se muestran resultados.
 */

export interface BuildStepsState {
  identity?: { name?: string; description?: string; personality?: string }
  race?: { itemId?: string; variantItemId?: string | null }
  class?: { itemId?: string }
  background?: { itemId?: string }
  attributes?: { method?: 'manual' | 'pointBuy'; values?: Record<string, number> }
  skills?: { chosen?: string[] }
  equipment?: Record<string, unknown>
  story?: Record<string, unknown>
  [key: string]: unknown
}

/** Blueprint del sistema (el primero de la lista si no hay clave). */
export async function fetchBlueprint(system = 'dnd5e'): Promise<CharacterBlueprintDto> {
  const blueprints = await apiFetch<CharacterBlueprintDto[]>(
    `/api/v1/characters/blueprints?system=${system}`,
  )
  const blueprint = blueprints.find((entry) => entry.key === 'standard') ?? blueprints[0]
  if (!blueprint) {
    throw new Error('No hay blueprint de creación para este sistema.')
  }
  return blueprint
}

/** Contenido visible para las elecciones (global + campaña del usuario). */
export async function fetchChoiceContent(
  typeKey: string,
  campaignId: string,
): Promise<ContentItemDto[]> {
  return apiFetch<ContentItemDto[]>(
    `/api/v1/content/items?system=dnd5e&typeKey=${typeKey}&campaignId=${campaignId}`,
  )
}

/** Prevalidación del build en servidor (autoridad de reglas). */
export function previewBuild(
  campaignId: string,
  blueprintKey: string,
  steps: BuildStepsState,
): Promise<BuildPreviewResponse> {
  return apiFetch<BuildPreviewResponse>('/api/v1/characters/build/preview', {
    method: 'POST',
    body: { system: 'dnd5e', blueprintKey, campaignId, steps },
  })
}

/** Borrador persistente: el build vive en el Actor (ADR-034 §2). */
export function saveDraftActor(
  campaignId: string,
  name: string,
  build: Record<string, unknown>,
  actor?: { id: string; version: number },
  portraitId?: string | null,
): Promise<ActorDto> {
  if (actor) {
    return apiFetch<ActorDto>(`/api/v1/actors/${actor.id}`, {
      method: 'PATCH',
      body: { version: actor.version, name, build, portraitId: portraitId ?? undefined },
    })
  }
  return apiFetch<ActorDto>(`/api/v1/campaigns/${campaignId}/actors`, {
    method: 'POST',
    body: { name, rulesetKey: 'dnd5e', attributes: {}, resources: {}, kind: 'pc', level: 1, build, portraitId: portraitId ?? undefined },
  })
}

/** Personaje final: atributos derivados + contenido fijado con usos. */
export async function finishCharacter(params: {
  campaign: CampaignDetail
  actor?: { id: string; version: number }
  name: string
  attributes: Record<string, number>
  systemData: Record<string, unknown>
  usageItemIds: string[]
  portraitId?: string | null
}): Promise<ActorDto> {
  const { campaign, actor, name, attributes, systemData, usageItemIds, portraitId } = params
  const result = actor
    ? await apiFetch<ActorDto>(`/api/v1/actors/${actor.id}`, {
        method: 'PATCH',
        body: {
          version: actor.version,
          name,
          kind: 'pc',
          level: 1,
          attributes,
          systemData,
          portraitId: portraitId ?? undefined,
        },
      })
    : await apiFetch<ActorDto>(`/api/v1/campaigns/${campaign.id}/actors`, {
        method: 'POST',
        body: {
          name,
          rulesetKey: 'dnd5e',
          attributes,
          resources: {},
          kind: 'pc',
          level: 1,
          systemData,
          portraitId: portraitId ?? undefined,
        },
      })

  // El contenido usado queda fijado por revisión con snapshot (ADR-031 §7).
  for (const itemId of usageItemIds) {
    await apiFetch(`/api/v1/actors/${result.id}/usages`, {
      method: 'POST',
      body: { itemId },
    })
  }
  return result
}
