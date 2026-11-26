import { describe, expect, it } from 'vitest';
import {
  clampZoom,
  initialCamera,
  MAX_ZOOM,
  MIN_ZOOM,
  pan,
  screenToWorld,
  worldToScreen,
  zoomAt,
} from './camera';

describe('camera', () => {
  it('screenToWorld y worldToScreen son inversas', () => {
    const camera = { tx: 120, ty: -40, zoom: 1.5 };
    const world = screenToWorld(camera, 300, 200);
    const screen = worldToScreen(camera, world.x, world.y);
    expect(screen.x).toBeCloseTo(300);
    expect(screen.y).toBeCloseTo(200);
  });

  it('pan traslada la vista en píxeles de pantalla', () => {
    const camera = pan(initialCamera(), 50, -30);
    expect(camera.tx).toBe(50);
    expect(camera.ty).toBe(-30);
    expect(camera.zoom).toBe(1);
  });

  it('zoomAt mantiene anclado el punto de pantalla bajo el cursor', () => {
    const camera = { tx: 0, ty: 0, zoom: 1 };
    const anchor = screenToWorld(camera, 200, 150);
    const zoomed = zoomAt(camera, 200, 150, 2);

    expect(zoomed.zoom).toBe(2);
    const after = screenToWorld(zoomed, 200, 150);
    expect(after.x).toBeCloseTo(anchor.x);
    expect(after.y).toBeCloseTo(anchor.y);
  });

  it('clampZoom respeta los límites 0.25–4', () => {
    expect(clampZoom(0.01)).toBe(MIN_ZOOM);
    expect(clampZoom(100)).toBe(MAX_ZOOM);
    expect(clampZoom(1.5)).toBe(1.5);
    expect(zoomAt(initialCamera(), 0, 0, 0.1).zoom).toBe(MIN_ZOOM);
    expect(zoomAt(initialCamera(), 0, 0, 10).zoom).toBe(MAX_ZOOM);
  });
});
