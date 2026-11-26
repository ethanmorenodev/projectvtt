/**
 * Estado de actores y tiradas de una campaña. Funciones puras: un evento
 * con versión más nueva sustituye al actor; una tirada duplicada no se
 * inserta dos veces.
 */

import type { ActorDto, RollDto } from '../api/types';

export function upsertActor(actors: ActorDto[], actor: ActorDto): ActorDto[] {
  const index = actors.findIndex((item) => item.id === actor.id);
  if (index < 0) {
    return [...actors, actor];
  }
  if (actors[index].version > actor.version) {
    return actors;
  }
  const next = actors.slice();
  next[index] = actor;
  return next;
}

export function prependRoll(rolls: RollDto[], roll: RollDto, limit = 50): RollDto[] {
  if (rolls.some((item) => item.rollId === roll.rollId)) {
    return rolls;
  }
  return [roll, ...rolls].slice(0, limit);
}
