import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';
import { prependRoll, upsertActor } from '../actors/actorStore';
import { applyInventory, type InventoryByActor } from '../actors/inventory';
import { applyEffects, type EffectsByActor } from '../actors/effects';
import { ApiError, apiFetch } from '../api/client';
import { isEnabled, play } from '../audio/sound';
import { fetchAnnotations } from '../api/annotations';
import { uploadSceneCover, useSceneCover } from '../api/sceneCovers';
import { appendMessage } from '../chat/chat';
import {
  appendDirectMessage,
  applyDirectMessages,
  type DirectMessagesByPeer,
} from '../chat/dm';
import { emptyCombatState } from '../combat/combat';
import { emptyFogState, isRevealed } from '../fog/fog';
import {
  deleteSceneMap,
  fetchSceneMap,
  mapDisplayName,
  mapWasRemoved,
  shouldReloadMap,
  uploadSceneMap,
} from '../map/upload';
import { validateMapFile } from '../map/validateMapFile';
import { SceneStage } from '../pixi/SceneStage';
import { snapPoint } from '../pixi/snap';
import { CampaignRealtime } from '../realtime/hub';
import {
  applyLocalMove,
  applyMoveResult,
  applySync,
  applyTokenCreated,
  applyTokenMoved,
  emptyTokens,
  upsertToken,
  type TokenState,
} from '../realtime/tokenStore';
import { replace as replaceAnnotations, useSceneAnnotations } from '../realtime/annotationStore';
import type {
  ActorDto,
  ActorEffectDto,
  ActorEffectsDto,
  ActorInventoryDto,
  ActorItemDto,
  CampaignDetail,
  CampaignMember,
  ChatMessageDto,
  CombatStateDto,
  DirectMessageDto,
  FogStateDto,
  GridShape,
  HistoryEntryDto,
  RollDto,
  SceneDetail,
  SceneSummary,
  TokenDto,
} from '../api/types';
import { ActorPanel } from './ActorPanel';
import { CharacterHub } from './CharacterHub';
import { ChatPanel } from './ChatPanel';
import { CombatPanel } from './CombatPanel';
import { ToolLauncher, ToolsHost } from './SceneTools';
import { MapToolsView } from './MapToolsView';
import { NotesPanel } from './NotesPanel';
import { useWindows, WindowProvider } from '../windows';
import type { ToolDescriptor } from '../windows';
import { t } from '../i18n';
import { MESA_KEYS } from '../ui/shortcuts';
import './mesa.css';

interface Props {
  campaign: CampaignDetail;
  scene: SceneDetail;
  onBack: () => void;
  /** Cambiar de escena desde el raíl (la App remonta la vista). */
  onOpenScene?: (scene: { id: string }) => void;
  onOpenLibrary?: () => void;
}

/** Contador discreto de ventanas abiertas (dock inferior, PMB-2.8b). */
function WindowsCount() {
  const { windows } = useWindows();
  return (
    <span className="muted">
      {t.mesa.windows(windows.filter((win) => !win.minimized).length)}
    </span>
  );
}

/** Acciones rápidas del dock (Fase B, PMB-8.6c). Abre herramientas ya
 *  existentes; no sustituye el sistema de ventanas (ADR-033). */
function TableDock({ canManage, fogEditing, onToggleFog, onAddToken }: {
  canManage: boolean;
  fogEditing: boolean;
  onToggleFog: () => void;
  onAddToken: () => void;
}) {
  const { windows, open, focus } = useWindows();
  const openTool = (id: string) => {
    const instance = windows.find((win) => win.toolId === id);
    if (instance) focus(instance.id);
    else open(id);
  };
  return (
    <div className="table-dock" role="group" aria-label={t.mesa.quickActions}>
      <button type="button" onClick={() => openTool('ficha')}>
        <img src="/icons/dice.svg" alt="" aria-hidden="true" />
        <span>{t.mesa.dockRoll}</span>
      </button>
      <button type="button" onClick={() => openTool('mapa-herramientas')}>
        <img src="/icons/position-marker.svg" alt="" aria-hidden="true" />
        <span>{t.mesa.dockAnnotate}</span>
      </button>
      <button type="button" onClick={() => openTool('mapa-herramientas')}>
        <img src="/icons/magnifying-glass.svg" alt="" aria-hidden="true" />
        <span>{t.mesa.dockMeasure}</span>
      </button>
      <button type="button" onClick={onToggleFog} aria-pressed={fogEditing}>
        <img src="/icons/fog.svg" alt="" aria-hidden="true" />
        <span>{t.mesa.dockFog}</span>
      </button>
      {canManage ? (
        <button type="button" className="table-dock__primary" onClick={onAddToken}>
          <span aria-hidden="true">＋</span> {t.mesa.addToken}
        </button>
      ) : null}
    </div>
  );
}

/** Atajos de la mesa: viven dentro de WindowProvider para abrir/enfocar
 *  ventanas existentes en lugar de duplicarlas (ADR-033). */
function TableShortcuts({ onToggleFog }: { onToggleFog: () => void }) {
  const { windows, open, focus } = useWindows();
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      const typing = target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA'
        || target?.tagName === 'SELECT' || target?.isContentEditable;
      if (typing || event.ctrlKey || event.metaKey || event.altKey) return;
      const openTool = (id: string) => {
        const instance = windows.find((win) => win.toolId === id);
        if (instance) focus(instance.id);
        else open(id);
      };
      switch (event.key.toLowerCase()) {
        case MESA_KEYS.roll: openTool('ficha'); break;
        case MESA_KEYS.annotate:
        case MESA_KEYS.measure: openTool('mapa-herramientas'); break;
        case MESA_KEYS.fog: onToggleFog(); break;
        default: return;
      }
      event.preventDefault();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [windows, open, focus, onToggleFog]);
  return null;
}

function SceneCover({ sceneId, hasCover, className }: {
  sceneId: string; hasCover: boolean; className: string
}) {
  const url = useSceneCover(sceneId, hasCover)
  return url
    ? <img className={className} src={url} alt="" aria-hidden="true" />
    : <span className="table-scenes__thumb" aria-hidden="true" />
}

type Connection = 'connecting' | 'connected' | 'reconnecting' | 'error';

/**
 * Vista de escena: monta el escenario PixiJS, conecta el hub de la campaña
 * y mantiene el estado de tokens. El movimiento se predice en local y el
 * servidor confirma (o rechaza con resincronización).
 */
export function SceneView({ campaign, scene, onBack, onOpenScene, onOpenLibrary }: Props) {
  const mountRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<SceneStage | null>(null);
  const realtimeRef = useRef<CampaignRealtime | null>(null);
  // `onBack` se pasa inline desde App: un ref evita reconectar el hub al cambiar.
  const onBackRef = useRef(onBack);
  onBackRef.current = onBack;
  const tokensRef = useRef<TokenState>(emptyTokens());
  const sceneRef = useRef<SceneDetail>(scene);
  const mapUrlRef = useRef<string | null>(null);
  const tokenCounterRef = useRef(1);

  const { user } = useAuth();
  const [connection, setConnection] = useState<Connection>('connecting');
  const [notice, setNotice] = useState<string | null>(null);
  const [tokens, setTokens] = useState<TokenState>(emptyTokens());
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [mapName, setMapName] = useState<string | null>(null);
  const [gridInput, setGridInput] = useState(String(scene.gridCellSize));
  const [gridShape, setGridShape] = useState<GridShape>(scene.gridShape);
  const [snapEnabled, setSnapEnabled] = useState(false);
  const [actors, setActors] = useState<ActorDto[]>([]);
  const [rolls, setRolls] = useState<RollDto[]>([]);
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [members, setMembers] = useState<CampaignMember[]>([]);
  const [dmTarget, setDmTarget] = useState<string | null>(null);
  const [dms, setDms] = useState<DirectMessagesByPeer>({});
  const [combat, setCombat] = useState<CombatStateDto>(() => emptyCombatState(scene.id));
  const [fog, setFog] = useState<FogStateDto>(() => emptyFogState(scene.id, scene.gridCellSize));
  const [fogEditing, setFogEditing] = useState(false);
  const [selectedActorId, setSelectedActorId] = useState<string | null>(null);
  const [inventory, setInventory] = useState<InventoryByActor>({});
  const [effects, setEffects] = useState<EffectsByActor>({});
  const [linkActorId, setLinkActorId] = useState('');
  const snapRef = useRef(false);
  snapRef.current = snapEnabled;

  const canManage = campaign.role === 'Owner' || campaign.role === 'GM';
  // Anotaciones compartidas: la HTTP y el evento del hub alimentan este store.
  const annotations = useSceneAnnotations(scene.id);
  const fogRef = useRef(fog);
  fogRef.current = fog;
  const fogEditRef = useRef(false);
  fogEditRef.current = fogEditing && canManage;
  const fogVisibleRef = useRef(true);
  fogVisibleRef.current = !canManage || fogEditing;

  const commit = useCallback((next: TokenState) => {
    tokensRef.current = next;
    setTokens(next);
    stageRef.current?.syncTokens(next);
  }, []);

  const resync = useCallback(async (rt: CampaignRealtime, stage: SceneStage) => {
    const state = await rt.syncScene(sceneRef.current.id);
    sceneRef.current = {
      ...sceneRef.current,
      gridCellSize: state.gridCellSize,
      gridShape: state.gridShape,
    };
    stage.setGrid(state.gridCellSize, state.gridShape);
    setGridInput(String(state.gridCellSize));
    setGridShape(state.gridShape);
    commit(applySync(state.tokens));
  }, [commit]);

  // Clic en una celda en modo niebla: revela u oculta según el estado
  // actual (el servidor es el autoritativo y emite `FogUpdated`).
  const handleFogCell = useCallback(async (x: number, y: number) => {
    const current = fogRef.current;
    if (!fogEditRef.current) {
      return;
    }

    const action = isRevealed(current, x, y) ? 'hide' : 'reveal';
    try {
      const next = await apiFetch<FogStateDto>(
        `/api/v1/scenes/${sceneRef.current.id}/fog/${action}`,
        { method: 'POST', body: { x, y } },
      );
      setFog(next);
      setNotice(null);
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.fog);
    }
  }, []);

  // Limpia el mapa mostrado sin pedirlo al servidor (tras un borrado).
  const clearMap = useCallback(() => {
    const previous = mapUrlRef.current;
    mapUrlRef.current = null;
    void stageRef.current?.setMap(null);
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    setMapName(null);
  }, []);

  // Carga el mapa compartido del servidor como URL de objeto (la imagen
  // va autenticada por HTTP, sin credenciales en la URL). Si llega un
  // nombre por el evento se muestra; si no, uno genérico.
  const reloadMap = useCallback(async (displayName?: string) => {
    const stage = stageRef.current;
    if (!stage) {
      return;
    }

    const loaded = await fetchSceneMap(sceneRef.current.id);
    const previous = mapUrlRef.current;
    if (!loaded) {
      mapUrlRef.current = null;
      await stage.setMap(null);
      if (previous) {
        URL.revokeObjectURL(previous);
      }
      setMapName(null);
      return;
    }

    mapUrlRef.current = loaded.url;
    const ok = await stage.setMap(loaded.url);
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    if (ok) {
      setMapName(displayName ?? loaded.fileName);
    } else {
      URL.revokeObjectURL(loaded.url);
      if (mapUrlRef.current === loaded.url) {
        mapUrlRef.current = null;
      }
    }
  }, []);

  const removeMap = async () => {
    try {
      await deleteSceneMap(sceneRef.current.id);
      clearMap();
      setNotice(null);
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.removeMap);
    }
  };

  const handleDrop = useCallback(
    async (tokenId: string, x: number, y: number) => {
      const rt = realtimeRef.current;
      const stage = stageRef.current;
      const current = tokensRef.current[tokenId];
      if (!rt || !stage || !current) {
        return;
      }

      // Predicción local inmediata; la versión enviada es la que creemos.
      const expectedVersion = current.version;
      const point = snapRef.current
        ? snapPoint(x, y, sceneRef.current.gridCellSize, sceneRef.current.gridShape)
        : { x, y };
      commit(applyLocalMove(tokensRef.current, tokenId, point.x, point.y, expectedVersion));

      try {
        const result = await rt.moveToken({
          commandId: crypto.randomUUID(),
          sceneId: sceneRef.current.id,
          tokenId,
          expectedVersion,
          x: point.x,
          y: point.y,
        });
        commit(applyMoveResult(tokensRef.current, result));
        setNotice(null);
      } catch (cause) {
        // Conflicto o rechazo: manda el estado del servidor.
        setNotice(cause instanceof Error ? cause.message : t.mesa.errors.moveRejected);
        try {
          await resync(rt, stage);
        } catch {
          // La siguiente sincronización (o evento) corregirá el estado.
        }
      }
    },
    [commit, resync],
  );

  useEffect(() => {
    const mount = mountRef.current;
    if (!mount) {
      return;
    }

    let disposed = false;

    void (async () => {
      try {
        const stage = await SceneStage.create(mount, {
          onSelectToken: (id) => setSelectedId(id),
          onTokenDrop: (id, x, y) => void handleDrop(id, x, y),
          onFogCell: (x, y) => void handleFogCell(x, y),
        });
        if (disposed) {
          stage.destroy();
          return;
        }
        stageRef.current = stage;
        stage.setGrid(sceneRef.current.gridCellSize, sceneRef.current.gridShape);
        stage.syncTokens(tokensRef.current);
        stage.setFog(fogRef.current.revealed, fogVisibleRef.current);
        stage.setFogEditing(fogEditRef.current);
        void reloadMap();

        const rt = CampaignRealtime.create({
          onTokenMoved: (event) => {
            if (isEnabled()) play('token');
            commit(applyTokenMoved(tokensRef.current, event));
          },
          onTokenCreated: (event) => commit(applyTokenCreated(tokensRef.current, event)),
          onActorCreated: (event) => setActors((current) => upsertActor(current, event.actor)),
          onActorUpdated: (event) => setActors((current) => upsertActor(current, event.actor)),
          onInventoryUpdated: (event) =>
            setInventory((current) =>
              applyInventory(current, { actorId: event.actorId, items: event.items }),
            ),
          onEffectsUpdated: (event) =>
            setEffects((current) =>
              applyEffects(current, { actorId: event.actorId, effects: event.effects }),
            ),
          onRollResolved: (event) => {
            if (isEnabled()) play('roll');
            setRolls((current) => prependRoll(current, event.roll));
          },
          onMessagePosted: (event) => {
            if (isEnabled()) play('message');
            setMessages((current) => appendMessage(current, event.message));
          },
          onDirectMessagePosted: (event) =>
            setDms((current) =>
              appendDirectMessage(current, event.message, user?.id ?? ''),
            ),
          onCombatUpdated: (event) => setCombat(event.combat),
          onFogUpdated: (event) => setFog(event.fog),
          onMapUpdated: (event) => {
            if (!shouldReloadMap(event, sceneRef.current.id)) {
              return;
            }
            // El borrado se limpia en local (sin volver a pedirlo); una
            // subida recarga el archivo del servidor con su nombre.
            if (mapWasRemoved(event)) {
              clearMap();
            } else {
              void reloadMap(mapDisplayName(event));
            }
          },
          onSceneAnnotationsUpdated: (event) => replaceAnnotations(event.annotations),
          // ADR-045: si borran la escena abierta, vuelve al espacio de campaña
          // (la navegación existente de `onBack` remonta la vista).
          onSceneDeleted: (event) => {
            if (event.sceneId === sceneRef.current.id) {
              onBackRef.current();
            }
          },
          onConnectionChange: (connected) =>
            setConnection(connected ? 'connected' : 'reconnecting'),
        });
        if (disposed) {
          await rt.stop();
          return;
        }
        realtimeRef.current = rt;

        await rt.start();
        if (disposed) {
          await rt.stop();
          return;
        }
        await rt.joinCampaign(campaign.id);
        if (disposed) {
          await rt.stop();
          return;
        }
        await resync(rt, stage);
        if (!disposed) {
          setConnection('connected');
        }
      } catch (cause) {
        if (!disposed) {
          setConnection('error');
          setNotice(cause instanceof Error ? cause.message : t.mesa.errors.connect);
        }
      }
    })();

    return () => {
      disposed = true;
      stageRef.current?.destroy();
      stageRef.current = null;
      const rt = realtimeRef.current;
      realtimeRef.current = null;
      void rt?.stop();
      if (mapUrlRef.current) {
        URL.revokeObjectURL(mapUrlRef.current);
        mapUrlRef.current = null;
      }
    };
  }, [campaign.id, commit, handleDrop, reloadMap, resync]);

  // La niebla se pinta en el escenario: visible para jugadores siempre y
  // para el GM solo en modo edición (los eventos `FogUpdated` y los
  // clics actualizan el mismo estado).
  useEffect(() => {
    stageRef.current?.setFog(fog.revealed, !canManage || fogEditing);
    stageRef.current?.setFogEditing(canManage && fogEditing);
  }, [fog, canManage, fogEditing]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const [loadedActors, loadedRolls, loadedMessages, loadedMembers, loadedCombat, loadedFog] =
          await Promise.all([
            apiFetch<ActorDto[]>(`/api/v1/campaigns/${campaign.id}/actors`),
            apiFetch<RollDto[]>(`/api/v1/campaigns/${campaign.id}/rolls`),
            apiFetch<ChatMessageDto[]>(`/api/v1/campaigns/${campaign.id}/messages`),
            apiFetch<CampaignMember[]>(`/api/v1/campaigns/${campaign.id}/members`),
            apiFetch<CombatStateDto>(`/api/v1/scenes/${scene.id}/combat`),
            apiFetch<FogStateDto>(`/api/v1/scenes/${scene.id}/fog`),
          ]);
        if (!cancelled) {
          setActors(loadedActors);
          setRolls(loadedRolls);
          setMessages(loadedMessages);
          setMembers(loadedMembers);
          setCombat(loadedCombat);
          setFog(loadedFog);
        }
      } catch (cause) {
        if (!cancelled) {
          setNotice(cause instanceof Error ? cause.message : t.mesa.errors.loadCampaign);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [campaign.id]);

  // Carga inicial de anotaciones: `replace` descarta respuestas obsoletas si
  // ya llegó un evento más reciente mientras se hacía la petición (ADR-043).
  useEffect(() => {
    let cancelled = false;
    void fetchAnnotations(scene.id)
      .then((loaded) => {
        if (!cancelled) {
          replaceAnnotations(loaded);
        }
      })
      .catch((cause: unknown) => {
        // Solo se tolera el 500 heredado de una BD demo sin la tabla de
        // anotaciones (SqliteSchemaGuard ya regenera esas BDs al arrancar).
        // Cualquier otro fallo (401/403/red) se informa: no se ocultan errores.
        if (cancelled || (cause instanceof ApiError && cause.status === 500)) {
          return;
        }
        setNotice(cause instanceof Error ? cause.message : t.mesa.errors.loadAnnotations);
      });
    return () => {
      cancelled = true;
    };
  }, [scene.id]);

  // Carga la conversación privada seleccionada (los eventos
  // `DirectMessagePosted` actualizan el mismo mapa por peer).
  useEffect(() => {
    if (!dmTarget) {
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const loaded = await apiFetch<DirectMessageDto[]>(
          `/api/v1/campaigns/${campaign.id}/direct-messages/${dmTarget}`,
        );
        if (!cancelled) {
          setDms((current) => applyDirectMessages(current, dmTarget, loaded));
        }
      } catch (cause) {
        if (!cancelled) {
          setNotice(
            cause instanceof Error
              ? cause.message
              : t.mesa.errors.loadDm,
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [campaign.id, dmTarget]);

  // Carga el inventario y los efectos del actor seleccionado (la
  // respuesta y los eventos `InventoryUpdated`/`EffectsUpdated` actualizan
  // el mismo mapa por actorId).
  useEffect(() => {
    if (!selectedActorId) {
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const [loadedInventory, loadedEffects] = await Promise.all([
          apiFetch<ActorInventoryDto>(`/api/v1/actors/${selectedActorId}/items`),
          apiFetch<ActorEffectsDto>(`/api/v1/actors/${selectedActorId}/effects`),
        ]);
        if (!cancelled) {
          setInventory((current) => applyInventory(current, loadedInventory));
          setEffects((current) => applyEffects(current, loadedEffects));
        }
      } catch (cause) {
        if (!cancelled) {
          setNotice(
            cause instanceof Error
              ? cause.message
              : t.mesa.errors.loadInventory,
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [selectedActorId]);

  const createToken = async () => {
    const stage = stageRef.current;
    if (!stage) {
      return;
    }
    const center = stage.centerWorld();
    const point = snapEnabled
      ? snapPoint(center.x, center.y, sceneRef.current.gridCellSize, sceneRef.current.gridShape)
      : center;
    try {
      const created = await apiFetch<TokenDto>(
        `/api/v1/scenes/${sceneRef.current.id}/tokens`,
        {
          method: 'POST',
          body: {
            name: `Token ${tokenCounterRef.current++}`,
            x: point.x,
            y: point.y,
            actorId: linkActorId || undefined,
          },
        },
      );
      commit(upsertToken(tokensRef.current, created));
      setNotice(null);
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.createToken);
    }
  };

  const importMap = (event: ChangeEvent<HTMLInputElement>) => {
    const input = event.target;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    const validation = validateMapFile(file);
    if (!validation.ok) {
      setNotice(validation.reason);
      return;
    }

    const stage = stageRef.current;
    if (!stage) {
      return;
    }

    void (async () => {
      try {
        // El servidor valida y persiste el mapa compartido (ADR-028).
        await uploadSceneMap(sceneRef.current.id, file);

        // Vista inmediata con el archivo local; el evento `MapUpdated`
        // recargará el del servidor para todos los clientes.
        const previous = mapUrlRef.current;
        const url = URL.createObjectURL(file);
        mapUrlRef.current = url;
        const loaded = await stage.setMap(url);
        if (previous) {
          URL.revokeObjectURL(previous);
        }
        if (loaded) {
          setMapName(file.name);
          setNotice(null);
        } else {
          setNotice(t.mesa.errors.loadImage);
          URL.revokeObjectURL(url);
        }
      } catch (cause) {
        setNotice(cause instanceof Error ? cause.message : t.mesa.errors.uploadMap);
      }
    })();
  };



  // Creación de escena desde el panel del DM (misma API que el espacio de
  // campaña); la nueva escena se abre en cuanto el servidor la confirma.
  const createScene = async () => {
    if (!newSceneName.trim()) return
    setCreatingScene(true)
    try {
      const created = await apiFetch<SceneDetail>(
        `/api/v1/campaigns/${campaign.id}/scenes`,
        { method: 'POST', body: { name: newSceneName.trim() } })
      setSceneList((current) => [...current, { ...created, tokenCount: 0 }])
      setNewSceneName('')
      onOpenScene?.({ id: created.id })
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.loadCampaign)
    } finally {
      setCreatingScene(false)
    }
  };

  // La portada se sube desde la vista de historia; se refleja al instante en
  // la miniatura de la lista y en el fondo de la escena.
  const uploadCover = async (file: File) => {
    try {
      await uploadSceneCover(scene.id, file)
      setCoverOn(true)
      setSceneList((current) => current.map((entry) =>
        entry.id === scene.id ? { ...entry, hasCover: true } : entry))
      setNotice(t.mesa.coverUploaded)
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.loadImage)
    }
  };

  // Bandeja de dados: tirada local sencilla publicada como mensaje del diario
  // (no aplica reglas; el servicio de reglas sigue siendo el dueño de las
  // tiradas de atributo con `POST /campaigns/{id}/rolls`).
  const rollDice = async (sides: number) => {
    const value = 1 + Math.floor(Math.random() * sides)
    try {
      const message = await apiFetch<ChatMessageDto>(
        `/api/v1/campaigns/${campaign.id}/messages`,
        { method: 'POST', body: { body: `🎲 d${sides} → ${value}` } })
      setMessages((current) => appendMessage(current, message))
      setNotice(null)
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.loadCampaign)
    }
  };

  const saveGrid = async (event: FormEvent) => {
    event.preventDefault();
    try {
      const updated = await apiFetch<SceneDetail>(`/api/v1/scenes/${sceneRef.current.id}`, {
        method: 'PATCH',
        body: { gridCellSize: Number(gridInput), gridShape },
      });
      sceneRef.current = updated;
      stageRef.current?.setGrid(updated.gridCellSize, updated.gridShape);
      setGridInput(String(updated.gridCellSize));
      setGridShape(updated.gridShape);
      setNotice(null);
    } catch (cause) {
      setNotice(cause instanceof Error ? cause.message : t.mesa.errors.saveGrid);
    }
  };

  // Escenas del raíl (PMB-2.8b): lista de solo lectura para cambiar de
  // escena; la lógica del escenario no cambia.
  const [sceneList, setSceneList] = useState<SceneSummary[]>([]);

  // Distribución Fase B: paneles en los bordes y modo Enfocar. El estado es
  // solo de presentación: no sustituye al sistema de ventanas (ADR-033).
  const [leftTab, setLeftTab] = useState<'journal' | 'ficha' | 'combate' | 'notes'>('journal');
  const [rightTab, setRightTab] = useState<'scenes' | 'party' | 'history' | 'inspector'>('scenes');
  const [focus, setFocus] = useState(false);
  const [mode, setMode] = useState<'tactical' | 'story'>('tactical');
  const [sceneFilter, setSceneFilter] = useState('');
  const [newSceneName, setNewSceneName] = useState('');
  const [creatingScene, setCreatingScene] = useState(false);
  const [coverOn, setCoverOn] = useState(scene.hasCover);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [scenePickerOpen, setScenePickerOpen] = useState(false);
  const toggleFog = useCallback(() => setFogEditing((current) => !current), []);

  useEffect(() => {
    setCoverOn(scene.hasCover)
  }, [scene.id, scene.hasCover]);

  // Inspector con pestañas (Fase B, PMB-8.6c).
  const [inspectorTab, setInspectorTab] = useState<'details' | 'layers'>('details');
  const [history, setHistory] = useState<HistoryEntryDto[] | null>(null);
  useEffect(() => {
    if (rightTab !== 'history' || history !== null) return;
    let cancelled = false;
    void apiFetch<HistoryEntryDto[]>(`/api/v1/campaigns/${campaign.id}/history`)
      .then((data) => { if (!cancelled) setHistory(data); })
      .catch(() => { if (!cancelled) setHistory([]); });
    return () => { cancelled = true; };
  }, [campaign.id, rightTab, history]);

  useEffect(() => {
    let cancelled = false;
    void apiFetch<unknown>(`/api/v1/campaigns/${campaign.id}/scenes`)
      .then((data) => {
        if (!cancelled && Array.isArray(data)) {
          setSceneList(data as SceneSummary[]);
        }
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [campaign.id]);

  const selected = selectedId ? tokens[selectedId] : null;
  const npcTokens = Object.values(tokens).filter((token) => !token.actorId);

  useEffect(() => {
    if (selected?.actorId) {
      setSelectedActorId(selected.actorId);
    }
  }, [selected?.actorId]);

  // Seleccionar un token abre el inspector en el panel derecho: la
  // información contextual no debe quedar oculta tras la pestaña Grupo.
  useEffect(() => {
    if (selectedId) {
      setRightTab('inspector');
    }
  }, [selectedId]);

  // El modo Enfocar cambia el ancho del tablero sin redimensionar la ventana.
  useEffect(() => {
    const frame = requestAnimationFrame(() => stageRef.current?.resizeToContainer());
    return () => cancelAnimationFrame(frame);
  }, [focus]);
  const connectionLabel = t.mesa.connection;

  // Paneles acoplados: comparten props y estado con las antiguas ventanas,
  // pero viven siempre en el raíl izquierdo (sin ventanas que descubrir).
  const chatPanelProps = {
    campaignId: campaign.id,
    currentUserId: user?.id ?? '',
    messages,
    members,
    dmTarget,
    conversation: dmTarget ? dms[dmTarget] ?? [] : [],
    onSelectTarget: setDmTarget,
    onMessagePosted: (message: ChatMessageDto) =>
      setMessages((current) => appendMessage(current, message)),
    onDirectMessagePosted: (message: DirectMessageDto) =>
      setDms((current) => appendDirectMessage(current, message, user?.id ?? '')),
    onError: setNotice,
  };
  const actorPanelProps = {
    campaignId: campaign.id,
    canManage,
    currentUserId: user?.id ?? null,
    actors,
    rolls,
    selectedActorId,
    onSelectActor: setSelectedActorId,
    onActorCreated: (actor: ActorDto) => setActors((current) => upsertActor(current, actor)),
    onActorUpdated: (actor: ActorDto) => setActors((current) => upsertActor(current, actor)),
    items: selectedActorId ? inventory[selectedActorId] ?? [] : [],
    onInventoryChanged: (changed: { actorId: string; items: ActorItemDto[] }) =>
      setInventory((current) => applyInventory(current, changed)),
    effects: selectedActorId ? effects[selectedActorId] ?? [] : [],
    onEffectsChanged: (changed: { actorId: string; effects: ActorEffectDto[] }) =>
      setEffects((current) => applyEffects(current, changed)),
    onRollResolved: (roll: RollDto) => setRolls((current) => prependRoll(current, roll)),
    onError: setNotice,
  };
  const combatPanelProps = {
    sceneId: scene.id,
    canManage,
    combat,
    tokens: Object.values(tokens),
    onCombatChanged: setCombat,
    onError: setNotice,
  };

  // Solo herramientas opcionales siguen en ventanas flotantes (ADR-033).
  const tableTools: ToolDescriptor[] = [
    {
      id: 'hub',
      title: t.mesa.tools.hub,
      icon: <img src="/icons/family-tree.svg" alt="" />,
      singleton: true,
      defaultWidth: 720,
      defaultHeight: 520,
      content: () => (
        <CharacterHub
          campaignId={campaign.id}
          canManage={canManage}
          actors={actors}
          selectedActorId={selectedActorId}
          items={selectedActorId ? inventory[selectedActorId] ?? [] : []}
          effects={selectedActorId ? effects[selectedActorId] ?? [] : []}
          onSelectActor={setSelectedActorId}
          onActorUpdated={(actor) => setActors((current) => upsertActor(current, actor))}
        />
      ),
    },
    {
      id: 'mapa-herramientas',
      title: t.mesa.tools.mapTools,
      icon: <img src="/icons/position-marker.svg" alt="" />,
      singleton: true,
      defaultWidth: 420,
      defaultHeight: 440,
      content: () => <MapToolsView annotations={annotations} canWrite={canManage} />,
    },
  ]

  return (
    <WindowProvider context="table" userId={user?.id} tools={tableTools}>
      <TableShortcuts onToggleFog={toggleFog} />
      <div className={`table${focus ? ' table--focus' : ''}`}>
        {/* Un solo raíl: el global de la aplicación. Volver, herramientas y
            biblioteca viven ahora en la cabecera; las ventanas flotantes
            (ADR-033) y la lógica no cambian. */}
        <header className="table-header">
          <button type="button" className="table-header__action" onClick={onBack}
            aria-label={t.mesa.back} title={t.mesa.back}>
            <span aria-hidden="true">←</span>
          </button>
          <div className="table-modes" role="group" aria-label={t.mesa.storyView}>
            <button type="button" className={mode === 'story' ? 'is-on' : ''}
              aria-pressed={mode === 'story'} onClick={() => setMode('story')}>
              {t.mesa.story}
            </button>
            <button type="button" className={mode === 'tactical' ? 'is-on' : ''}
              aria-pressed={mode === 'tactical'} onClick={() => setMode('tactical')}>
              {t.mesa.tactical}
            </button>
          </div>
          <nav className="table-header__tools" aria-label={t.mesa.tools.title}>
            <ToolLauncher tools={tableTools} title={t.mesa.tools.title} />
          </nav>
          <button type="button" className="table-header__action" onClick={onOpenLibrary}
            aria-label={t.mesa.openLibrary} title={t.mesa.openLibrary}>
            <img src="/icons/open-book.svg" alt="" aria-hidden="true" />
          </button>
          <div className="table-scene-picker">
            <button type="button" className="table-scene-picker__button"
              aria-label={`${t.mesa.scenePicker}: ${scene.name}`}
              aria-expanded={scenePickerOpen}
              onClick={() => setScenePickerOpen((open) => !open)}>
              <img src="/icons/position-marker.svg" alt="" aria-hidden="true" />
              <span>{scene.name}</span>
              <span aria-hidden="true">▾</span>
            </button>
            {scenePickerOpen && (
              <div className="table-scene-picker__menu" role="menu" aria-label={t.mesa.scenePicker}>
                <div className="table-scene-picker__label">{t.mesa.scenePicker}</div>
                {sceneList.map((entry) => (
                  <button key={entry.id} type="button" role="menuitem"
                    aria-current={entry.id === scene.id ? 'page' : undefined}
                    onClick={() => {
                      setScenePickerOpen(false)
                      if (entry.id !== scene.id) onOpenScene?.(entry)
                    }}>
                    <span>{entry.name}</span>
                    <small>{t.mesa.tokens(entry.tokenCount)}</small>
                  </button>
                ))}
              </div>
            )}
          </div>
          <button
            type="button"
            className="table-focus"
            aria-pressed={focus}
            aria-label={focus ? t.mesa.exitFocus : t.mesa.focus}
            title={focus ? t.mesa.exitFocus : t.mesa.focus}
            onClick={() => setFocus((current) => !current)}
          >
            <img src="/icons/gaze.svg" alt="" aria-hidden="true" />
            <span>{focus ? t.mesa.exitFocus : t.mesa.focus}</span>
          </button>
          <span className={`status status-${connection}`}>{connectionLabel[connection]}</span>
        </header>

        <aside className="table-edge table-edge--left" aria-label={t.mesa.journal}>
          <div className="table-edge__tabs" role="tablist">
            <button type="button" role="tab" aria-selected={leftTab === 'journal'}
              onClick={() => setLeftTab('journal')}>{t.mesa.journal}</button>
            <button type="button" role="tab" aria-selected={leftTab === 'ficha'}
              onClick={() => setLeftTab('ficha')}>{t.mesa.tools.ficha}</button>
            <button type="button" role="tab" aria-selected={leftTab === 'combate'}
              onClick={() => setLeftTab('combate')}>{t.mesa.tools.combate}</button>
            {canManage ? (
              <button type="button" role="tab" aria-selected={leftTab === 'notes'}
                onClick={() => setLeftTab('notes')}>{t.mesa.notes}</button>
            ) : null}
          </div>
          <div className="table-edge__body">
            {leftTab === 'journal' ? <ChatPanel {...chatPanelProps} /> : null}
            {leftTab === 'ficha' ? <ActorPanel {...actorPanelProps} /> : null}
            {leftTab === 'combate' ? <CombatPanel {...combatPanelProps} /> : null}
            {leftTab === 'notes' ? <NotesPanel campaignId={campaign.id} /> : null}
          </div>
        </aside>

        <div className="table-board">
          <div className="scene-canvas" ref={mountRef} />
          {mode === 'story' ? (
            <div className={`table-story${coverOn ? ' table-story--cover' : ''}`}>
              {coverOn ? (
                <div className="table-story__art">
                  <SceneCover sceneId={scene.id} hasCover className="table-story__img" />
                </div>
              ) : null}
              <div className="table-story__content">
                <span>{t.mesa.sceneCurrent}</span>
                <h1>{scene.name}</h1>
                <p>{t.mesa.storyText}</p>
                {canManage ? (
                  <label className="table-story__upload">
                    {t.mesa.coverUpload}
                    <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml"
                      onChange={(event) => {
                        const file = event.target.files?.[0]
                        if (file) void uploadCover(file)
                        event.target.value = ''
                      }}
                    />
                  </label>
                ) : null}
                <button type="button" onClick={() => setMode('tactical')}>
                  {t.mesa.openTactical}
                </button>
              </div>
            </div>
          ) : null}
          <ToolsHost
            tools={tableTools}
            labels={{ minimize: t.mesa.tools.minimize, close: t.mesa.tools.close }}
          />
        </div>

        <aside className="table-edge table-edge--right" aria-label={t.mesa.scenePicker}>
          <div className="table-edge__tabs table-edge__tabs--dm" role="tablist">
            <button type="button" role="tab" aria-selected={rightTab === 'scenes'}
              onClick={() => setRightTab('scenes')}>ESCENAS</button>
            <button type="button" role="tab" aria-selected={rightTab === 'party'}
              onClick={() => setRightTab('party')}>{t.mesa.party}</button>
            <button type="button" role="tab" aria-selected={rightTab === 'history'}
              onClick={() => setRightTab('history')}>{t.mesa.story}</button>
            <button type="button" role="tab" aria-selected={rightTab === 'inspector'}
              onClick={() => setRightTab('inspector')}>{t.mesa.inspector}</button>
          </div>
          <div className="table-edge__body">
            {rightTab === 'scenes' ? (
              <div className="table-scenes">
                <input className="table-scenes__filter" aria-label={t.biblioteca.searchPlaceholder}
                  placeholder={t.biblioteca.searchPlaceholder} value={sceneFilter}
                  onChange={(event) => setSceneFilter(event.target.value)} />
                {sceneList.filter((entry) =>
                  entry.name.toLowerCase().includes(sceneFilter.toLowerCase())).length === 0 ? (
                  <p className="table-edge__empty">{t.mesa.scenesEmpty}</p>
                ) : (
                  <ul className="table-scenes__list">
                    {sceneList.filter((entry) =>
                      entry.name.toLowerCase().includes(sceneFilter.toLowerCase())).map((entry) => (
                      <li key={entry.id}>
                        <button type="button"
                          className={entry.id === scene.id ? 'is-active' : ''}
                          onClick={() => onOpenScene?.(entry)}>
                          <SceneCover sceneId={entry.id} hasCover={entry.hasCover}
                            className="table-scenes__cover" />
                          <span className="table-scenes__name">{entry.name}</span>
                          <span className="table-scenes__meta">
                            {entry.id === scene.id ? '▶' : entry.tokenCount > 0 ? entry.tokenCount : ''}
                          </span>
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
                <form className="table-scenes__new" onSubmit={(event) => {
                  event.preventDefault()
                  void createScene()
                }}>
                  <input aria-label={t.mesa.sceneNamePlaceholder}
                    placeholder={t.mesa.sceneNamePlaceholder} value={newSceneName}
                    onChange={(event) => setNewSceneName(event.target.value)} />
                  <button type="submit" disabled={creatingScene || !newSceneName.trim()}
                    aria-label={t.mesa.createScene}>＋</button>
                </form>
              </div>
            ) : null}

            {rightTab === 'party' ? (
              <div className="table-party">
                <h3 className="table-edge__label">{t.mesa.party}</h3>
                {members.length === 0 ? (
                  <p className="table-edge__empty">{t.mesa.partyEmpty}</p>
                ) : (
                  <ul>
                    {members.map((member) => (
                      <li key={member.userId}>
                        <span className="table-party__avatar" aria-hidden="true">
                          {member.userName.slice(0, 1).toUpperCase()}
                        </span>
                        <span><strong>{member.userName}</strong><small>{member.role}</small></span>
                      </li>
                    ))}
                  </ul>
                )}
                <h3 className="table-edge__label">{t.mesa.npcs}</h3>
                {npcTokens.length === 0 ? (
                  <p className="table-edge__empty">{t.mesa.npcsEmpty}</p>
                ) : (
                  <ul>
                    {npcTokens.map((token) => (
                      <li key={token.id}>
                        <span className="table-party__avatar" aria-hidden="true">
                          {token.name.slice(0, 1).toUpperCase()}
                        </span>
                        <span>
                          <strong>{token.name}</strong>
                          <small>{t.mesa.position(Math.round(token.x), Math.round(token.y), token.version)}</small>
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ) : null}

            {rightTab === 'history' ? (
              <div className="table-inspector__list">
                {history === null ? (
                  <p className="table-inspector__hint">…</p>
                ) : history.length === 0 ? (
                  <p className="table-inspector__hint">{t.mesa.historyEmpty}</p>
                ) : history.slice(0, 20).map((entry) => (
                  <div className="table-inspector__entry" key={entry.id}>
                    <strong>{entry.summary}</strong>
                    <span>{entry.userName} · {new Date(entry.createdAt).toLocaleString()}</span>
                  </div>
                ))}
              </div>
            ) : null}

            {rightTab === 'inspector' ? (
              <div className="table-inspector">
                <div className="table-inspector__title">{t.mesa.inspector}</div>
                <div className="table-inspector__tabs" role="tablist">
                  {(['details', 'layers'] as const).map((tab) => (
                    <button key={tab} type="button" role="tab"
                      aria-selected={inspectorTab === tab}
                      onClick={() => setInspectorTab(tab)}>
                      {tab === 'details' ? t.mesa.inspectorDetails : t.mesa.inspectorLayers}
                    </button>
                  ))}
                </div>
                {inspectorTab === 'details' ? (
                  selected ? (
                    <div className="table-inspector__token">
                      <strong>{selected.name}</strong>
                      <span>{selected.actorId ? t.mesa.withActor : t.mesa.withoutActor}</span>
                      <span>{t.mesa.position(Math.round(selected.x), Math.round(selected.y), selected.version)}</span>
                    </div>
                  ) : (
                    <div className="table-inspector__empty">
                      <span className="table-inspector__empty-title">{t.mesa.nothingSelected}</span>
                      <span className="table-inspector__empty-text">{t.mesa.nothingSelectedText}</span>
                    </div>
                  )
                ) : null}
                {inspectorTab === 'layers' ? (
                  <div className="table-inspector__list">
                    <div className="table-inspector__row"><span>{t.mesa.subnavScenes}</span><strong>{scene.name}</strong></div>
                    <div className="table-inspector__row"><span>{t.mesa.subnavLayers}</span><strong>{mapName ?? '—'}</strong></div>
                    <div className="table-inspector__row"><span>{t.mesa.dockFog}</span><strong>{fogEditing ? '✎' : '—'}</strong></div>
                    <p className="table-inspector__hint">{t.mesa.layersReserved}</p>
                  </div>
                ) : null}
              </div>
            ) : null}
          </div>
        </aside>

        <div className="table-bottom">
        <div className="table-dice" role="group" aria-label={t.mesa.diceTray}>
          {[4, 6, 8, 10, 12, 20].map((sides) => (
            <button type="button" key={sides} aria-label={`Tirar d${sides}`}
              onClick={() => void rollDice(sides)}>
              <span aria-hidden="true">◈</span><small>d{sides}</small>
            </button>
          ))}
        </div>
        <TableDock
          canManage={canManage}
          fogEditing={fogEditing}
          onToggleFog={toggleFog}
          onAddToken={() => void createToken()}
        />
        <div className="hud-row hud-row--status">
          <button type="button" className="table-advanced-toggle"
            aria-expanded={showAdvanced} onClick={() => setShowAdvanced((current) => !current)}>
            {t.mesa.advanced}
          </button>
          <span className="muted">
            {t.mesa.tokens(Object.keys(tokens).length)}
            {mapName ? t.mesa.mapName(mapName) : ''}
          </span>
          <WindowsCount />
        </div>
        {showAdvanced && (
        <div className="hud-row hud-row--advanced">
          {canManage && actors.length > 0 && (
            <label className="inline-form">
              {t.mesa.linkActor}
              <select
                value={linkActorId}
                onChange={(event) => setLinkActorId(event.target.value)}
                aria-label={t.mesa.newTokenActorLabel}
              >
                <option value="">{t.mesa.noActor}</option>
                {actors.map((actor) => (
                  <option key={actor.id} value={actor.id}>
                    {actor.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          <label className="check-label">
            <input
              type="checkbox"
              checked={snapEnabled}
              onChange={(event) => setSnapEnabled(event.target.checked)}
            />
            {t.mesa.snap}
          </label>
          {canManage && (
            <label className="check-label">
              <input
                type="checkbox"
                checked={fogEditing}
                onChange={(event) => setFogEditing(event.target.checked)}
              />
              {t.mesa.fog}
            </label>
          )}
          {canManage && (
            <label className="file-button">
              {t.mesa.importMap}
              <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" onChange={importMap} />
            </label>
          )}
          {canManage && mapName && (
            <button type="button" onClick={() => void removeMap()}>
              {t.mesa.removeMap}
            </button>
          )}
          {canManage && (
            <form onSubmit={saveGrid} className="inline-form">
              <label>
                {t.mesa.cell}
                <input
                  type="number"
                  min={8}
                  max={512}
                  value={gridInput}
                  onChange={(event) => setGridInput(event.target.value)}
                />
              </label>
              <label>
                {t.mesa.shape}
                <select
                  value={gridShape}
                  onChange={(event) => setGridShape(event.target.value as GridShape)}
                  aria-label="Forma del grid"
                >
                  <option value="square">{t.mesa.square}</option>
                  <option value="hex">{t.mesa.hex}</option>
                </select>
              </label>
              <button type="submit">{t.mesa.save}</button>
            </form>
          )}
        </div>
        )}
        {notice && <div className="hud-row notice">{notice}</div>}
        <div className="hud-row muted">
          {t.mesa.hint}
        </div>
      </div>
      </div>
    </WindowProvider>
  );
}
