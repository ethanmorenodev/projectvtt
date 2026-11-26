import { describe, expect, it } from 'vitest';
import type { MapUpdatedEvent } from '../api/types';
import { mapDisplayName, mapWasRemoved, shouldReloadMap } from './upload';

const eventFixture = (overrides: Partial<MapUpdatedEvent> = {}): MapUpdatedEvent => ({
  eventId: 'event-1',
  campaignId: 'campaign-1',
  actorUserId: 'user-1',
  occurredAt: '2026-11-09T00:00:00Z',
  schemaVersion: 1,
  sceneId: 'scene-1',
  map: {
    sceneId: 'scene-1',
    fileName: 'mapa.png',
    contentType: 'image/png',
    sizeBytes: 1024,
    updatedAt: '2026-11-09T00:00:00Z',
  },
  ...overrides,
});

describe('eventos del mapa', () => {
  it('shouldReloadMap solo afecta a la escena del evento', () => {
    expect(shouldReloadMap(eventFixture(), 'scene-1')).toBe(true);
    expect(shouldReloadMap(eventFixture(), 'scene-2')).toBe(false);
  });

  it('mapWasRemoved detecta el borrado (map null)', () => {
    expect(mapWasRemoved(eventFixture())).toBe(false);
    expect(mapWasRemoved(eventFixture({ map: null }))).toBe(true);
  });

  it('mapDisplayName muestra el nombre del archivo o uno genérico', () => {
    expect(mapDisplayName(eventFixture())).toBe('mapa.png');
    expect(mapDisplayName(eventFixture({ map: null }))).toBe('mapa compartido');
  });
});
