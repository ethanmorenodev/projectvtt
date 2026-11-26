# Contratos compartidos

Contratos entre backend, cliente web y cliente de escritorio. Regla central: **los clientes no inventan modelos paralelos** (`ARCHITECTURE.md` §9).

## Estado (Épica E, 2026-11-04)

Comando/evento principal: **`MoveToken` / `TokenMoved`** (ver ADR-010, Propuesta):

- `events/move-token.command.v1.schema.json` — comando con `commandId` idempotente y `expectedVersion` para concurrencia optimista.
- `events/token-moved.event.v1.schema.json` — evento con envelope de `ARCHITECTURE.md` §7 (`eventId`, `campaignId`, `sceneId`, `actorUserId`, `occurredAt`, `schemaVersion`, `aggregateVersion`).

Eventos aditivos:

- **`TokenCreated`** (Épica D, `events/token-created.event.v1.schema.json`) — notifica a la campaña que un token se creó por HTTP. Desde la Épica E el token incluye `actorId` (vinculación opcional a un Actor).
- **`ActorCreated`** / **`ActorUpdated`** (Épica E, `events/actor-created.event.v1.schema.json` y `actor-updated.event.v1.schema.json`) — actores creados o modificados por HTTP, con sus atributos y recursos validados por el Ruleset (ADR-012).
- **`RollResolved`** (Épica E, `events/roll-resolved.event.v1.schema.json`) — tirada resuelta por el servidor con desglose auditable (`total = die + modifier + extra + effectModifier`) y `rollId` idempotente; incluye la modalidad (`rollMode`), los dados lanzados (`dice`) y los efectos reconocidos del catálogo (`effects`), ADR-015.
- **`InventoryUpdated`** (Épica E, `events/inventory-updated.event.v1.schema.json`) — el inventario de un Actor cambió (ítem creado, editado o borrado); viaja completo (`actorId` + `items`) para que el cliente sustituya su lista. Ver ADR-013 (Propuesta).
- **`EffectsUpdated`** (Épica E, `events/effects-updated.event.v1.schema.json`) — se aplicó o retiró un efecto/condición de un Actor; la lista viaja completa (`actorId` + `effects`), sin duración por ahora. Ver ADR-014 (Propuesta).
- **`MessagePosted`** (Épica F, `events/message-posted.event.v1.schema.json`) — un miembro publicó un mensaje en el chat de campaña; viaja el mensaje con el nombre de su autor (`message` con `userName`), sin `aggregateVersion`. Ver ADR-016 (Propuesta).
- **`DirectMessagePosted`** (Épica F, `events/direct-message-posted.event.v1.schema.json`) — mensaje privado 1:1 dentro de la campaña; **no se emite al grupo de la campaña**: el servidor lo entrega solo a las conexiones de los dos participantes (`message` con `fromUserName`/`toUserName`). Ver ADR-017 (Propuesta).
- **`CombatUpdated`** (Épica F, `events/combat-updated.event.v1.schema.json`) — cambió el combate de una Escena (inicio, combatientes, avance de turno o fin); el estado viaja completo (`combat` con `round`, `currentCombatantId` y `combatants` ordenados por turno). Ver ADR-018 (Propuesta).
- **`FogUpdated`** (Épica F, `events/fog-updated.event.v1.schema.json`) — cambió la niebla de guerra manual de una Escena (revelar/ocultar celda); viaja el estado completo con las celdas **reveladas** (lo demás oculto; no filtra nada secreto). Ver ADR-020 (Propuesta).
- **`MapUpdated`** (Épica D/H, `events/map-updated.event.v1.schema.json`) — subió, se reemplazó o se borró el mapa persistido de una Escena (`map` con metadatos, o `null` al borrar); el cliente vuelve a pedir la imagen por HTTP autenticado. Ver ADR-028 (Propuesta). Al borrar una escena **sigue emitiéndose con `Map=null` por compatibilidad** con clientes antiguos, pero ya no es la señal canónica de borrado.
- **`SceneDeleted`** (EXP-37, ADR-045, `events/scene-deleted.event.v1.schema.json`) — se borró una Escena y su estado dependiente; envelope estándar (`eventId`, `campaignId`, `actorUserId`, `occurredAt`, `schemaVersion`, `sceneId`), sin `aggregateVersion`. Lo emite `SceneService` al grupo de campaña **después de persistir**, junto con el `MapUpdated(null)` de compatibilidad. Los clientes nuevos recargan la lista o vuelven al espacio de campaña al recibirlo; la carga inicial y la reconexión siguen siendo HTTP. Contrato aprobado por el propietario en esta sesión («Autorizado»).
- **`SetSceneAnnotation` / `SceneAnnotationsUpdated`** (EXP-31, ADR-043) — **anuncio de contrato nuevo aprobado por el propietario en esta sesión** (`DECISIONS.md` ADR-043). `events/set-scene-annotation.schema.json`: comando HTTP `PUT /scenes/{id}/annotations` con `commandId` idempotente, `expectedVersion`, `kind: draw|note`, puntos y texto; solo GM/Owner, `clear` vacía la escena. `events/scene-annotations-updated.schema.json`: evento al grupo de campaña con el envelope estándar (`eventId`, `campaignId`, `actorUserId`, `occurredAt`, `schemaVersion`) y el estado completo en `annotations` (carga/reconexión sigue siendo HTTP). El servidor persiste antes de emitir y no reemite en reintentos. Tipos C#: `backend/Api/Annotations/SceneAnnotationService.cs`; tipos TS: `apps/web/src/api/types.ts` y `realtime/hub.ts`.

Emisor de los diez eventos nuevos: `ActorService`, `ActorItemService`, `ActorEffectService`, `ChatService`, `DirectMessageService`, `CombatService`, `FogService`, `MapService` y `RollService` vía `IHubContext`; consumidor: `CampaignRealtime` en el cliente web. Las peticiones `CreateRollRequest`, `CreateMessageRequest` y `CreateDirectMessageRequest` viajan por HTTP (`POST /campaigns/{id}/rolls`, `/messages` y `/direct-messages`) y no tienen esquema aparte hasta que exista exportación OpenAPI.

Los tipos C# equivalentes viven en `backend/Api/Realtime/Contracts.cs` y `backend/Api/Rules/Dtos.cs`; los tipos TS de consumo en `apps/web/src/api/types.ts`; deben mantenerse alineados con estos esquemas. Aún no hay SDKs generados; la especificación OpenAPI exportada está en [`../../docs/api/openapi.json`](../../docs/api/openapi.json) (ver «Contratos HTTP (post-MVP)»).

## Qué vivirá aquí

- `openapi/` — especificación OpenAPI de los endpoints HTTP, versionada.
- `events/` — esquemas JSON Schema de comandos y eventos en tiempo real.
- `sdk-typescript/` y `sdk-dotnet/` — SDKs generados desde los contratos, cuando sea viable.

Ninguno de estos directorios existe todavía: se crean junto con su primer contenido real.

## Reglas

1. Todo contrato lleva versión explícita: `schemaVersion` en eventos; versión de ruta o de campo en HTTP.
2. Envelope mínimo de eventos (`ARCHITECTURE.md` §7): `eventId`, `campaignId`, `sceneId` o `aggregateId`, `actorUserId`, `occurredAt`, `schemaVersion`, `aggregateVersion`, más los datos del cambio.
3. Un cambio incompatible requiere versión nueva o migración coordinada, anunciado en `DECISIONS.md` **antes** de implementarlo en cualquier cliente.
4. Los nombres y esquemas propuestos son propuestas hasta que el coordinador o un ADR los aprueben.
5. El servidor valida permisos, reglas y estado; los clientes solo consumen y presentan.

## Contratos HTTP (post-MVP)

Copia versionada: [`../../docs/api/openapi.json`](../../docs/api/openapi.json), OpenAPI 3.1.1 generado por `AddOpenApi()` / `MapOpenApi()` en `backend/Api/Program.cs` (el valor de `servers.url` refleja el puerto usado al regenerarlo, no un despliegue). Regenerada tras EXP-31 para incluir `/api/v1/scenes/{sceneId}/annotations`; vuelve a generarla cuando cambie cualquier contrato HTTP. Todo está bajo `/api/v1` excepto `/health` y `/metrics`. El servidor aplica la autorización; en la tabla se resume la regla real de cada grupo:

| Grupo | Ruta base | Autorización según el código |
|---|---|---|
| Campaigns (con portada) | `/api/v1/campaigns` | Leer campaña, miembros y portada requiere miembro; renombrar, archivar, gestionar miembros/invitaciones y subir/borrar la portada requiere Owner; crear campaña y canjear una invitación solo requieren sesión. |
| Scenes (con portada) | `/api/v1/scenes`, `/api/v1/campaigns/{campaignId}/scenes` | Leer escenas, tokens y portada requiere miembro; crear escena/token, configurar el grid y subir/borrar la portada requiere GM u Owner. **Nuevo (EXP-37, aprobado por el propietario en esta sesión):** `DELETE /api/v1/scenes/{sceneId}` solo GM/Owner; borra en cascada tokens, anotaciones, niebla y combate, y elimina los blobs de mapa y portada. Emite `MapUpdated` con `Map=null` (ADR-028) **por compatibilidad** y `SceneDeleted` (ADR-045, contrato propio) como señal canónica de borrado; los clientes nuevos recargan la lista con `SceneDeleted`. |
| Actors | `/api/v1/actors`, `/api/v1/campaigns/{campaignId}/actors` | Leer requiere miembro; crear ficha puede cualquier miembro y queda vinculada a su creador; editar solo el creador o GM/Owner. |
| Content (types/items/grants/relations/packs) | `/api/v1/content` | **Nuevo (EXP-40, aprobado por el propietario en esta sesión):** `POST /content/types` y `POST /content/packs/import` requieren la política `ContentOperator` (claim `content-operator`, allowlist `Content:OperatorUsers`, vacía por defecto). El listado de tipos y paquetes sigue requiriendo solo sesión; ver ítems depende del ámbito, el estado y los grants del miembro; editar/publicar requiere autor, dueño, GM/Owner o grant de edición; aprobar/rechazar propuestas y ver secretos de DM requiere GM/Owner (o el dueño en ítems personales). |
| Images | `/api/v1/images` | Listar y descargar solo las imágenes propias o las de paquetes; subir, editar, borrar y reemplazar requiere ser el dueño de la imagen (cualquier usuario con sesión). |
| Credits | `/api/v1/credits/export` | Con sesión; exporta las atribuciones de los paquetes y de las imágenes propias del usuario. |
| Activity | `/api/v1/activity`, `/api/v1/campaigns/{campaignId}/history` | Solo lectura, limitada a las campañas de las que el usuario es miembro; sin notas ni mensajes privados. |
| Inventory / equipment | `/api/v1/actors/{actorId}/items`, `/api/v1/actors/{actorId}/equipment-effects`, `/api/v1/equipment-slots/{system}`, `POST /api/v1/actors/{actorId}/items/{itemId}/transfer` | Leer requiere miembro; crear, editar, borrar y equipar ítems requiere el creador del actor o GM/Owner; las ranuras del sistema son catálogo con sesión. **Nuevo (EXP-36, aprobado por el propietario en esta sesión):** `transfer` mueve o divide un ítem entre actores de la misma campaña, exige permiso de edición sobre **ambos**, valida cantidad y `version` optimista (409), no hereda el hueco de equipamiento (ADR-041) y publica `InventoryUpdated` para los dos actores. |
| Auth | `/api/v1/auth` | `register` y `login` son anónimos y están limitados por tasa; `logout` y `me` requieren sesión Bearer. |
| Health / metrics | `/health`, `/metrics` | Públicos, sin autenticación: liveness JSON y métricas Prometheus (ADR-026). |

Para regenerar la copia con una BD SQLite aislada y sin tocar una API ya en marcha en `:5000` (PowerShell; `curl.exe` en otra terminal):

```powershell
$env:Database__Provider='sqlite'
$env:ConnectionStrings__Default='Data Source=./projectvtt-openapi.db'
$env:PROJECTVTT_RESET_DEMO_DB_ON_MISMATCH='1'
$env:ASPNETCORE_URLS='http://127.0.0.1:5099'
dotnet run --project backend/Api --no-launch-profile --artifacts-path ./.artifacts-openapi
curl.exe http://127.0.0.1:5099/openapi/v1.json -o docs/api/openapi.json
```

El cliente de escritorio debe reutilizar estos DTOs y contratos en lugar de definir modelos paralelos; cualquier cambio compartido se anuncia y coordina antes de implementarlo en los clientes (AGENTS.md §6).

## Siguiente hito

Generar los SDKs TypeScript y .NET desde los contratos (`ARCHITECTURE.md` §9) y refrescar la copia versionada del OpenAPI cuando cambien los endpoints. Nuevos comandos/eventos de las Épicas G+ se añadirán aquí con su ADR.
