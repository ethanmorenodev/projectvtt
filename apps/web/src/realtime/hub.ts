/**
 * Envoltorio del hub `/hubs/campaign` (@microsoft/signalr). Traduce el
 * contrato del backend (ADR-010) a llamadas tipadas y traduce también los
 * eventos `TokenMoved` / `TokenCreated` / `ActorCreated` / `ActorUpdated` /
 * `InventoryUpdated` / `EffectsUpdated` / `RollResolved` / `MessagePosted` /
 * `DirectMessagePosted` / `CombatUpdated` / `FogUpdated` / `MapUpdated` /
 * `SceneAnnotationsUpdated` / `SceneDeleted` a callbacks.
 */

import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type {
  ActorCreatedEvent,
  ActorUpdatedEvent,
  CombatUpdatedEvent,
  DirectMessagePostedEvent,
  EffectsUpdatedEvent,
  FogUpdatedEvent,
  InventoryUpdatedEvent,
  MapUpdatedEvent,
  MessagePostedEvent,
  MoveTokenCommand,
  MoveTokenResult,
  RollResolvedEvent,
  SceneAnnotationsUpdatedEvent,
  SceneDeletedEvent,
  SceneState,
  TokenCreatedEvent,
  TokenMovedEvent,
} from '../api/types';
import { getApiToken } from '../api/client';

export interface HubHandlers {
  onTokenMoved?: (event: TokenMovedEvent) => void;
  onTokenCreated?: (event: TokenCreatedEvent) => void;
  onActorCreated?: (event: ActorCreatedEvent) => void;
  onActorUpdated?: (event: ActorUpdatedEvent) => void;
  onInventoryUpdated?: (event: InventoryUpdatedEvent) => void;
  onEffectsUpdated?: (event: EffectsUpdatedEvent) => void;
  onRollResolved?: (event: RollResolvedEvent) => void;
  onMessagePosted?: (event: MessagePostedEvent) => void;
  onDirectMessagePosted?: (event: DirectMessagePostedEvent) => void;
  onCombatUpdated?: (event: CombatUpdatedEvent) => void;
  onFogUpdated?: (event: FogUpdatedEvent) => void;
  onMapUpdated?: (event: MapUpdatedEvent) => void;
  onSceneAnnotationsUpdated?: (event: SceneAnnotationsUpdatedEvent) => void;
  onSceneDeleted?: (event: SceneDeletedEvent) => void;
  onConnectionChange?: (connected: boolean) => void;
}

export class CampaignRealtime {
  private constructor(private readonly connection: HubConnection) {}

  static create(handlers: HubHandlers): CampaignRealtime {
    const url = new URL('/hubs/campaign', window.location.origin).toString();
    const connection = new HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory: () => getApiToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('TokenMoved', handlers.onTokenMoved ?? (() => undefined));
    connection.on('TokenCreated', handlers.onTokenCreated ?? (() => undefined));
    connection.on('ActorCreated', handlers.onActorCreated ?? (() => undefined));
    connection.on('ActorUpdated', handlers.onActorUpdated ?? (() => undefined));
    connection.on('InventoryUpdated', handlers.onInventoryUpdated ?? (() => undefined));
    connection.on('EffectsUpdated', handlers.onEffectsUpdated ?? (() => undefined));
    connection.on('RollResolved', handlers.onRollResolved ?? (() => undefined));
    connection.on('MessagePosted', handlers.onMessagePosted ?? (() => undefined));
    connection.on('DirectMessagePosted', handlers.onDirectMessagePosted ?? (() => undefined));
    connection.on('CombatUpdated', handlers.onCombatUpdated ?? (() => undefined));
    connection.on('FogUpdated', handlers.onFogUpdated ?? (() => undefined));
    connection.on('MapUpdated', handlers.onMapUpdated ?? (() => undefined));
    connection.on('SceneAnnotationsUpdated', handlers.onSceneAnnotationsUpdated ?? (() => undefined));
    connection.on('SceneDeleted', handlers.onSceneDeleted ?? (() => undefined));
    connection.onreconnected(() => handlers.onConnectionChange?.(true));
    connection.onreconnecting(() => handlers.onConnectionChange?.(false));
    connection.onclose(() => handlers.onConnectionChange?.(false));

    return new CampaignRealtime(connection);
  }

  async start(): Promise<void> {
    await this.connection.start();
  }

  async joinCampaign(campaignId: string): Promise<void> {
    await this.connection.invoke('JoinCampaign', campaignId);
  }

  async syncScene(sceneId: string): Promise<SceneState> {
    return await this.connection.invoke<SceneState>('SyncScene', sceneId);
  }

  async moveToken(command: MoveTokenCommand): Promise<MoveTokenResult> {
    return await this.connection.invoke<MoveTokenResult>('MoveToken', command);
  }

  async stop(): Promise<void> {
    await this.connection.stop();
  }
}
