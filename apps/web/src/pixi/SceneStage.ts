/**
 * Escenario PixiJS de la escena: mapa de fondo, grid cuadrado, tokens con
 * selección, zoom (rueda), pan (arrastre sobre el fondo) y arrastre de
 * tokens con confirmación del servidor vía callback `onTokenDrop`.
 *
 * Este módulo solo presenta estado: las reglas (versiones, permisos,
 * persistencia) viven en el backend y en `tokenStore`.
 */

import {
  Application,
  Circle,
  Container,
  Graphics,
  Rectangle,
  Sprite,
  Text,
  Texture,
  type FederatedPointerEvent,
} from 'pixi.js';
import { initialCamera, pan, screenToWorld, zoomAt, type Camera } from './camera';
import type { TokenState, TokenView } from '../realtime/tokenStore';
import type { FogCellDto, GridShape } from '../api/types';
import { worldToGridCell } from '../fog/fog';
import { hexCellCenter } from '../grid/hex';

export interface StageCallbacks {
  /** Selección cambiada por clic (null = sin selección). */
  onSelectToken: (id: string | null) => void;
  /** Soltó un token tras arrastrarlo: el llamador envía MoveToken. */
  onTokenDrop: (id: string, x: number, y: number) => void;
  /** Clic en una celda en modo edición de niebla (el llamador revela/oculta). */
  onFogCell?: (x: number, y: number) => void;
}

interface TokenNode {
  id: string;
  container: Container;
  body: Graphics;
  ring: Graphics;
  label: Text;
  name: string;
}

const GRID_EXTENT = 4096;
const GRID_COLOR = 0x2a3140;
const TOKEN_COLOR = 0x4f8cff;
const SELECT_COLOR = 0xffd166;
const FOG_COLOR = 0x05070a;

export class SceneStage {
  private camera: Camera = initialCamera();
  private gridCellSize = 32;
  private gridShape: GridShape = 'square';
  private selectedId: string | null = null;
  private readonly nodes = new Map<string, TokenNode>();
  private mapSprite: Sprite | null = null;
  private mapRequest = 0;
  private fogRevealed: FogCellDto[] = [];
  private fogVisible = false;
  private fogEditing = false;
  private panDrag: { lastX: number; lastY: number; startX: number; startY: number; moved: boolean } | null = null;
  private tokenDrag: { id: string; moved: boolean } | null = null;
  private removeWheel: (() => void) | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private destroyed = false;

  private constructor(
    private readonly app: Application,
    private readonly world: Container,
    private readonly grid: Graphics,
    private readonly tokenLayer: Container,
    private readonly fogLayer: Graphics,
    private readonly callbacks: StageCallbacks,
  ) {}

  static async create(container: HTMLElement, callbacks: StageCallbacks): Promise<SceneStage> {
    const app = new Application();
    await app.init({ background: 0x12151a, resizeTo: container, antialias: true });
    container.appendChild(app.canvas);

    const world = new Container();
    const background = new Graphics();
    background.eventMode = 'static';
    // Hit-area infinita: todo clic sin objeto encima inicia el pan.
    background.hitArea = new Rectangle(-1e7, -1e7, 2e7, 2e7);
    const grid = new Graphics();
    const tokenLayer = new Container();
    // La niebla tapa TODO (mapa y tokens), por eso va encima.
    const fogLayer = new Graphics();
    fogLayer.eventMode = 'none';

    world.addChild(background, grid, tokenLayer, fogLayer);
    app.stage.addChild(world);
    app.stage.eventMode = 'static';
    app.stage.hitArea = app.screen;

    const stage = new SceneStage(app, world, grid, tokenLayer, fogLayer, callbacks);
    stage.wire(background);
    stage.redrawGrid();
    return stage;
  }

  // --- Estado ----------------------------------------------------------

  /** Sincroniza los tokens con el estado (autoritativo u optimista). */
  syncTokens(state: TokenState): void {
    if (this.destroyed) {
      return;
    }

    const seen = new Set<string>();
    for (const token of Object.values(state)) {
      seen.add(token.id);
      let node = this.nodes.get(token.id);
      if (!node) {
        node = this.createTokenNode(token);
        this.nodes.set(token.id, node);
        this.tokenLayer.addChild(node.container);
      }
      // Durante un arrastre local no se pisa la posición prevista.
      if (!this.tokenDrag || this.tokenDrag.id !== token.id) {
        node.container.position.set(token.x, token.y);
      }
      if (node.name !== token.name) {
        node.name = token.name;
        node.label.text = token.name;
      }
    }

    for (const [id, node] of this.nodes) {
      if (seen.has(id)) {
        continue;
      }
      this.tokenLayer.removeChild(node.container);
      node.container.destroy({ children: true });
      this.nodes.delete(id);
      if (this.selectedId === id) {
        this.setSelected(null);
      }
    }
  }

  setSelected(id: string | null): void {
    this.selectedId = id;
    for (const [tokenId, node] of this.nodes) {
      node.ring.visible = tokenId === id;
    }
  }

  setGrid(cellSize: number, shape: GridShape = 'square'): void {
    this.gridCellSize = cellSize;
    this.gridShape = shape;
    this.redrawGrid();
    this.redrawFog();
    for (const node of this.nodes.values()) {
      this.drawToken(node);
    }
  }

  /**
   * Niebla de guerra (ADR-020): celdas reveladas y si el overlay se
   * muestra (jugadores siempre; el GM solo en modo edición). Lo no
   * revelado queda tapado, tokens incluidos.
   */
  setFog(revealed: FogCellDto[], visible: boolean): void {
    if (this.destroyed) {
      return;
    }
    this.fogRevealed = revealed;
    this.fogVisible = visible;
    this.redrawFog();
  }

  /** Modo edición de niebla: los clics revelan/ocultan en vez de seleccionar. */
  setFogEditing(enabled: boolean): void {
    this.fogEditing = enabled;
  }

  /**
   * Fija o retira el mapa de fondo (la URL es del llamador, que la revoca
   * cuando se reemplaza). Carga la imagen de forma asíncrona y resuelve
   * `false` si no puede decodificarse; una sustitución posterior invalida
   * la carga anterior.
   */
  async setMap(url: string | null): Promise<boolean> {
    this.removeMap();
    if (!url) {
      return true;
    }
    const request = this.mapRequest;

    try {
      const image = new Image();
      image.src = url;
      await image.decode();
      if (this.destroyed || request !== this.mapRequest) {
        return false;
      }
      const sprite = new Sprite(Texture.from(image));
      this.mapSprite = sprite;
      // Entre el fondo y el grid: el grid se dibuja encima del mapa.
      this.world.addChildAt(sprite, 1);
      return true;
    } catch {
      return false;
    }
  }

  /** Centro de la vista en coordenadas de mundo (para crear tokens ahí). */
  centerWorld(): { x: number; y: number } {
    return screenToWorld(this.camera, this.app.screen.width / 2, this.app.screen.height / 2);
  }

  /**
   * Ajusta el renderizador cuando cambia el contenedor sin que cambie la
   * ventana (p. ej. al activar el modo Enfocar). `resizeTo` de Pixi solo
   * observa `window.resize`, así que el cambio de layout no lo dispara.
   */
  resizeToContainer(): void {
    if (this.destroyed) {
      return;
    }
    const parent = this.app.canvas.parentElement;
    if (!parent) {
      return;
    }
    this.app.renderer.resize(parent.clientWidth, parent.clientHeight);
    this.app.stage.hitArea = this.app.screen;
  }

  destroy(): void {
    if (this.destroyed) {
      return;
    }
    this.destroyed = true;
    this.removeWheel?.();
    this.resizeObserver?.disconnect();
    this.removeMap();
    this.app.destroy(true, { children: true });
  }

  // --- Internos --------------------------------------------------------

  private wire(background: Graphics): void {
    const { app } = this;

    background.on('pointerdown', (event: FederatedPointerEvent) => {
      this.panDrag = {
        lastX: event.global.x,
        lastY: event.global.y,
        startX: event.global.x,
        startY: event.global.y,
        moved: false,
      };
      event.stopPropagation();
    });

    app.stage.on('pointermove', (event: FederatedPointerEvent) => {
      if (this.panDrag) {
        const dx = event.global.x - this.panDrag.lastX;
        const dy = event.global.y - this.panDrag.lastY;
        if (dx !== 0 || dy !== 0) {
          this.panDrag.moved = true;
          this.panDrag.lastX = event.global.x;
          this.panDrag.lastY = event.global.y;
          this.applyCamera(pan(this.camera, dx, dy));
        }
        return;
      }

      if (this.tokenDrag) {
        const point = screenToWorld(this.camera, event.global.x, event.global.y);
        const node = this.nodes.get(this.tokenDrag.id);
        if (node) {
          node.container.position.set(point.x, point.y);
          this.tokenDrag.moved = true;
        }
      }
    });

    const finishDrag = () => {
      if (this.panDrag) {
        const { startX, startY, moved } = this.panDrag;
        this.panDrag = null;
        if (!moved) {
          if (this.fogEditing) {
            this.emitFogCell(startX, startY);
          } else {
            this.callbacks.onSelectToken(null);
          }
        }
      }

      if (this.tokenDrag) {
        const { id, moved } = this.tokenDrag;
        this.tokenDrag = null;
        if (moved) {
          const node = this.nodes.get(id);
          if (node) {
            this.callbacks.onTokenDrop(id, node.container.position.x, node.container.position.y);
          }
        }
      }
    };
    app.stage.on('pointerup', finishDrag);
    app.stage.on('pointerupoutside', finishDrag);

    // Rueda de zoom anclada al cursor (listener nativo para poder impedir
    // el scroll de la página: el listener de Pixi es pasivo).
    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      const rect = app.canvas.getBoundingClientRect();
      const factor = Math.exp(-event.deltaY * 0.0015);
      this.applyCamera(
        zoomAt(this.camera, event.clientX - rect.left, event.clientY - rect.top, factor),
      );
    };
    app.canvas.addEventListener('wheel', onWheel, { passive: false });
    this.removeWheel = () => app.canvas.removeEventListener('wheel', onWheel);

    this.resizeObserver = new ResizeObserver(() => {
      app.stage.hitArea = app.screen;
    });
    this.resizeObserver.observe(app.canvas);
  }

  private applyCamera(camera: Camera): void {
    this.camera = camera;
    this.world.position.set(camera.tx, camera.ty);
    this.world.scale.set(camera.zoom);
  }

  private redrawGrid(): void {
    this.grid.clear();
    if (this.gridShape === 'hex') {
      this.redrawHexGrid();
    } else {
      const cell = this.gridCellSize;
      this.grid.moveTo(-GRID_EXTENT, -GRID_EXTENT).lineTo(-GRID_EXTENT, GRID_EXTENT);
      for (let x = -GRID_EXTENT; x <= GRID_EXTENT; x += cell) {
        this.grid.moveTo(x, -GRID_EXTENT).lineTo(x, GRID_EXTENT);
      }
      for (let y = -GRID_EXTENT; y <= GRID_EXTENT; y += cell) {
        this.grid.moveTo(-GRID_EXTENT, y).lineTo(GRID_EXTENT, y);
      }
    }
    this.grid.stroke({ width: 1, color: GRID_COLOR, alpha: 0.7 });
  }

  /**
   * Grid hexagonal flat-top (ADR-023): las aristas forman tres familias
   * de líneas paralelas con la misma separación perpendicular
   * (√3·R/2 con R = ancho/2), más barato que dibujar cada hexágono.
   */
  private redrawHexGrid(): void {
    const r = this.gridCellSize / 2;
    const spacing = (Math.sqrt(3) * r) / 2;
    const extent = GRID_EXTENT * 1.5;
    const s60 = Math.sqrt(3) / 2;
    const families = [
      { dirX: 1, dirY: 0, nX: 0, nY: 1 },
      { dirX: 0.5, dirY: s60, nX: -s60, nY: 0.5 },
      { dirX: -0.5, dirY: s60, nX: s60, nY: 0.5 },
    ];
    const maxOffset = Math.ceil(extent / spacing);
    for (const family of families) {
      for (let m = -maxOffset; m <= maxOffset; m++) {
        const cx = family.nX * m * spacing;
        const cy = family.nY * m * spacing;
        this.grid
          .moveTo(cx - family.dirX * extent, cy - family.dirY * extent)
          .lineTo(cx + family.dirX * extent, cy + family.dirY * extent);
      }
    }
  }

  /**
   * Niebla: un rect oscuro sobre el área del grid con un hueco por cada
   * celda revelada (`cut()`), cuadrado o hexagonal según la forma del
   * grid. Las celdas fuera del área se ignoran para no romper el corte.
   */
  private redrawFog(): void {
    this.fogLayer.clear();
    if (!this.fogVisible) {
      return;
    }

    const cell = this.gridCellSize;
    this.fogLayer
      .rect(-GRID_EXTENT, -GRID_EXTENT, GRID_EXTENT * 2, GRID_EXTENT * 2)
      .fill({ color: FOG_COLOR, alpha: 0.95 });
    for (const revealed of this.fogRevealed) {
      if (this.gridShape === 'hex') {
        const center = hexCellCenter(revealed.x, revealed.y, cell);
        if (
          center.x < -GRID_EXTENT ||
          center.y < -GRID_EXTENT ||
          center.x > GRID_EXTENT ||
          center.y > GRID_EXTENT
        ) {
          continue;
        }
        const radius = cell / 2;
        const points: number[] = [];
        for (let vertex = 0; vertex < 6; vertex++) {
          const angle = (Math.PI / 3) * vertex;
          points.push(
            center.x + radius * Math.cos(angle),
            center.y + radius * Math.sin(angle),
          );
        }
        this.fogLayer.poly(points).cut();
      } else {
        const x = revealed.x * cell;
        const y = revealed.y * cell;
        if (
          x < -GRID_EXTENT ||
          y < -GRID_EXTENT ||
          x + cell > GRID_EXTENT ||
          y + cell > GRID_EXTENT
        ) {
          continue;
        }
        this.fogLayer.rect(x, y, cell, cell).cut();
      }
    }
  }

  /** Convierte una posición de pantalla en celda y la pasa al llamador. */
  private emitFogCell(screenX: number, screenY: number): void {
    const point = screenToWorld(this.camera, screenX, screenY);
    const cell = worldToGridCell(point.x, point.y, this.gridCellSize, this.gridShape);
    this.callbacks.onFogCell?.(cell.x, cell.y);
  }

  private tokenRadius(): number {
    return Math.max(8, this.gridCellSize * 0.4);
  }

  private createTokenNode(token: TokenView): TokenNode {
    const radius = this.tokenRadius();
    const container = new Container();
    container.eventMode = 'static';
    container.cursor = 'pointer';
    container.hitArea = new Circle(0, 0, radius + 4);
    container.position.set(token.x, token.y);

    const body = new Graphics();
    const ring = new Graphics();
    ring.visible = false;
    const label = new Text({
      text: token.name,
      style: { fontFamily: 'monospace', fontSize: 12, fill: 0xffffff, lineHeight: 14 },
    });
    label.anchor.set(0.5, 0);

    container.addChild(body, ring, label);

    const node: TokenNode = { id: token.id, container, body, ring, label, name: token.name };
    this.drawToken(node);

    container.on('pointerdown', (event: FederatedPointerEvent) => {
      // En modo niebla el clic revela/oculta la celda bajo el token.
      if (this.fogEditing) {
        this.emitFogCell(event.global.x, event.global.y);
        event.stopPropagation();
        return;
      }
      this.callbacks.onSelectToken(token.id);
      this.setSelected(token.id);
      this.tokenDrag = { id: token.id, moved: false };
      event.stopPropagation();
    });

    return node;
  }

  private drawToken(node: TokenNode): void {
    const radius = this.tokenRadius();
    node.container.hitArea = new Circle(0, 0, radius + 4);
    node.body.clear().circle(0, 0, radius).fill(TOKEN_COLOR);
    node.ring
      .clear()
      .circle(0, 0, radius + 4)
      .stroke({ width: 2, color: SELECT_COLOR });
    node.ring.visible = this.selectedId === node.id;
    node.label.position.set(0, -radius - 16);
  }

  private removeMap(): void {
    // Invalida cualquier carga de mapa en curso.
    this.mapRequest += 1;
    if (!this.mapSprite) {
      return;
    }
    const sprite = this.mapSprite;
    this.mapSprite = null;
    this.world.removeChild(sprite);
    sprite.destroy({ children: true, texture: true });
  }
}
