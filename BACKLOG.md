# ProjectVTT — Backlog inicial

Prioridades:
- **P0:** bloquea el primer vertical slice o el MVP.
- **P1:** importante para una primera experiencia completa.
- **P2:** posterior al MVP.

Cada tarea debe convertirse en una issue pequeña antes de asignarse a un agente.

## Épica A — Preparación

- [x] **P0** Confirmar sistema de reglas inicial. → D&D 5e / 5.5e (ADR-005, 2026-11-03)
- [x] **P0** Confirmar cliente escritorio y versión objetivo de Godot. → Godot 4.7.2 + C# (ADR-006/ADR-008, 2026-11-03)
- [x] **P0** Confirmar alcance de paridad web/escritorio. → **confirmado por el propietario 2026-11-09: vertical slice primero** (login, campañas, escenas, mapa, tokens y movimiento en vivo); chat, combate, niebla e historial irán después (ver ADR-006)
- [x] **P0** Crear monorepo y convenciones. → `docs/CONVENTIONS.md`, estructura `apps/ backend/ packages/ tests/ infra/ docs/`
- [x] **P0** Añadir CI con build y tests. → `.github/workflows/ci.yml` (pendiente de ejecución real en GitHub)
- [x] **P0** Añadir Docker Compose para desarrollo. → `infra/docker/compose.dev.yml` (pendiente de ejecución: Docker no disponible en el entorno actual)
- [x] **P0** Crear ADRs para decisiones confirmadas. → ADR-001..006 y ADR-008 aceptadas; ADR-007 sigue en propuesta

## Épica B — Backend y acceso

- [x] **P0** Crear API ASP.NET Core. → `backend/Api` (minimal APIs + ProblemDetails/OpenAPI)
- [x] **P0** Configurar PostgreSQL y migraciones. → EF Core 10 + Npgsql, migración `InitialCreate`, drift check en CI (ADR-009)
- [x] **P0** Implementar autenticación. → registro/login/logout/me con sesión opaca hasheada (ADR-009, Propuesta)
- [x] **P0** Crear campañas. → CRUD de campañas con autorización por recurso
- [x] **P0** Implementar membresías e invitaciones. → roles, invitaciones caducables, canje idempotente
- [x] **P0** Implementar roles y autorización por recurso. → Player < GM < Owner; no miembro→404, sin rol→403, último Owner→409
- [x] **P0** Añadir pruebas de acceso no autorizado. → 33 pruebas de integración (SQLite, sin Docker)

Notas de Épica B (2026-11-03): no verificado en runtime con PostgreSQL/Docker (no disponible en el entorno); el DDL se validó con `dotnet ef migrations script --idempotent` y el drift con `has-pending-model-changes`. Rate limit, CORS y OpenAPI definidos pero no probados fuera de pruebas de integración.

## Épica C — Tiempo real

- [x] **P0** Configurar SignalR. → hub `/hubs/campaign` autenticado con la sesión opaca
- [x] **P0** Asociar conexiones a campañas autorizadas. → grupos por campaña; `JoinCampaign` verifica membresía antes de añadir
- [x] **P0** Definir primer contrato de comando/evento. → `MoveToken`/`TokenMoved` con JSON Schema en `packages/contracts/events/` (ADR-010, Propuesta)
- [x] **P0** Implementar movimiento de token validado en servidor. → membresía + coordenadas + versión esperada (Player puede mover; GM/Owner crea escenas/tokens)
- [x] **P0** Persistir el movimiento antes de confirmarlo. → `SaveChanges` previo a emitir `TokenMoved`
- [x] **P0** Gestionar comandos duplicados. → idempotencia por `commandId` en `processed_commands`
- [x] **P0** Reconectar y resincronizar estado. → `SyncScene` devuelve estado autoritativo con versiones
- [x] **P0** Probar dos clientes simultáneos. → 7 pruebas con dos conexiones SignalR (46 en total)

Notas de Épica C (2026-11-03): las pruebas usan LongPolling sobre el TestServer en memoria; el transporte WebSockets no está cubierto por pruebas automatizadas (probado solo en compilación/lógica). `processed_commands` no tiene todavía poda/retención.

## Épica D — Cliente web y mapa

- [x] **P0** Crear aplicación web. → `apps/web` React 19 + Vite, shell con sesión y navegación campañas→escenas→escena
- [x] **P0** Integrar PixiJS. → `pixi.js` 8.22 encapsulado en `src/pixi/SceneStage.ts`
- [x] **P0** Crear vista de campaña/escena. → `LoginView`, `CampaignsView`, `ScenesView`, `SceneView`
- [x] **P0** Importar mapa con validación. → `validateMapFile` (PNG/JPEG/WebP/SVG, 10 MiB) + **mapa persistido y compartido** en el servidor: `GET|PUT|DELETE /api/v1/scenes/{id}/map` con firma binaria, `IAssetStore` (filesystem; S3/MinIO pendiente ADR-007) y evento `MapUpdated` (ADR-028, Propuesta)
- [x] **P0** Implementar zoom y pan. → `camera.ts` (zoom 0.25–4 anclado al cursor, pan por arrastre)
- [x] **P0** Dibujar grid cuadrado. → `GridCellSize` persistido en escena (8..512) con `PATCH /api/v1/scenes/{id}` (GM+) y migración `AddSceneGridCellSize`
- [x] **P0** Crear, seleccionar y mover token. → arrastre con predicción local; confirmación/conflicto del servidor con `SyncScene`
- [x] **P0** Sincronizar con backend. → SignalR `@microsoft/signalr`, eventos `TokenMoved`/`TokenCreated`, resync en conflicto y reconexión
- [x] **P0** Ajuste opcional al grid. → `snapToGrid` en el cliente; se envían coordenadas ya ajustadas
- [x] **P1** Añadir grid hexagonal. → `GridShape` por escena (`square`/`hex`, GM+ en `PATCH /scenes/{id}`), hexágonos flat-top odd-q con snap y niebla sensibles a la forma (huecos hexagonales), render por familias de líneas (ADR-023, Propuesta)

Notas de Épica D (2026-11-03): backend 49 pruebas (SQLite) y web 20 pruebas (Vitest) + `tsc && vite build` ejecutados con éxito. No verificado en runtime con navegador/PostgreSQL/Docker (no disponible en el entorno): el transporte WebSockets del hub y el renderizado Pixi quedan pendientes de prueba manual o E2E (Épica H). El evento aditivo `TokenCreated` se añadió al contrato de ADR-010 (esquema en `packages/contracts/events/token-created.event.v1.schema.json`, Propuesta). Ajuste al grid añadido el 2026-11-03 (preferencia local, no persistida).

Verificación (2026-11-09, grid hexagonal): `dotnet build/test backend/ProjectVTT.slnx` → 201 pruebas (41 unitarias + 160 integración, SQLite; 5 nuevas de forma de grid: cuadrada por defecto, cambio a hexagonal por GM+, conservación al cambiar solo la celda, 400 con valor inválido y 403 a jugador); web `tsc --noEmit && vite build` + 76 pruebas (Vitest; 9 nuevas de `hex.ts`/`snapPoint`/`worldToGridCell`; los tests corrigieron expectativas mal calculadas del retículo); migración `AddSceneGridShape` sin drift (`has-pending-model-changes`). No probado con PostgreSQL/Docker ni con E2E automatizado (Épica H).

## Épica E — Actores y reglas

- [x] **P0** Definir modelo genérico Actor. → `backend/Api/Actors`, `Domain/Actor`, migración `AddActorsAndTokenActor`
- [x] **P0** Definir esquema de atributos y recursos. → JSON validado por el Ruleset; `ResourceValue` actual/máximo
- [x] **P0** Crear interfaz del Ruleset tras elegir el sistema. → `IRuleset` + `Dnd5eRuleset` (ADR-005)
- [x] **P0** Implementar primera tirada. → `POST /api/v1/campaigns/{id}/rolls`, dado en servidor, evento `RollResolved`
- [x] **P0** Implementar modificadores. → modificador 5e en Ruleset; extra y CD opcionales
- [x] **P0** UI web de ficha y tiradas. → panel en `SceneView` / `ActorPanel`; token opcionalmente vinculado al crear; edición con `PATCH` y concurrencia optimista (409)
- [x] **P1** Inventario. → tabla relacional `actor_items` (nombre + cantidad), CRUD `/api/v1/actors/{id}/items` con permisos ADR-012 y evento `InventoryUpdated` (ADR-013, Propuesta)
- [x] **P1** Efectos y condiciones. → tabla relacional `actor_effects` (nombre único por actor, sin duración), alta/baja en `/api/v1/actors/{id}/effects` con permisos ADR-012 y evento `EffectsUpdated` (ADR-014, Propuesta)
- [x] **P1** Automatización específica del Ruleset. → `IRuleset.PlanRoll` + catálogo de `Dnd5eRuleset` (ventaja/desventaja 2d20 y modificadores) aplicado en `RollService`; desglose `RollBreakdown` ampliado (ADR-015, Propuesta)

Notas de Épica E (2026-11-04): permisos confirmados por el propietario (ADR-012, Aceptada): crear puede cualquier miembro (propietario = creador), editar y tirar solo con los propios actores o siendo GM+. UI de edición de ficha añadida (nombre, atributos y PG con `PATCH` y versión; el 409 cierra el editor). Sin pruebas E2E de la UI; el mapa compartido sigue fuera de esta épica.

Notas de Épica E — inventario (2026-11-06): ítems genéricos nombre + cantidad (1..9999) en tabla relacional `actor_items` (ARCHITECTURE §8), con `Version` por ítem para concurrencia optimista y borrado en cascada con el actor (migración `AddActorItems`). Mismos permisos que editar la ficha (ADR-012), sin duplicar la regla: helpers `RequireActorAccessAsync`/`RequireEditableActorAsync` compartidos con el `PATCH` del actor. Cada mutación devuelve el inventario completo y emite `InventoryUpdated` al grupo de la campaña (ADR-013, Propuesta). UI en el panel de ficha: lista con `×cantidad`, añadir/editar/borrar solo para propietario o GM (solo lectura en actores ajenos).

Notas de Épica E — efectos y condiciones (2026-11-06): entidad genérica `actor_effects` (solo nombre, 1..100, **sin duración** — decisión del propietario 2026-11-06: nada puede decrementarla hasta que existan turnos, Épica F) con índice único por actor (409 si ya está aplicado, también en carrera). Permisos ADR-012 con los mismos helpers de `ActorService`; las mutaciones devuelven la lista completa y emiten `EffectsUpdated` (ADR-014, Propuesta). UI en el panel de ficha: lista con "Retirar" y formulario "Aplicar", en solo lectura para actores ajenos. La aplicación de efectos a tiradas y atributos queda en "Automatización específica del Ruleset".

Notas de Épica E — automatización del Ruleset (2026-11-08): `RollService` lee los efectos activos del actor y llama a `IRuleset.PlanRoll` (mecánica y reconocimiento por catálogo confirmados por el propietario el 2026-11-08, ver ADR-015): el catálogo de `Dnd5eRuleset` traduce nombres libres (sin distinguir mayúsculas ni tildes) en ventaja/desventaja (2d20; se anulan entre sí) o modificadores planos (`bendecido` +2, `maldito` −2). El servidor lanza los dados que indique el plan y `ResolveAbilityCheck` conserva el mayor (ventaja) o el menor (desventaja). `RollBreakdown` se amplía con `rollMode`, `dice`, `effectModifier` y `effects` (contrato `roll-resolved` actualizado, `schemaVersion` sigue en 1); las tiradas guardadas antes del cambio se normalizan al leer, sin migración. UI: prefijo "Ventaja · "/"Desventaja · " en la lista de tiradas; el catálogo vive solo en el servidor (la web no lo replica). Pendientes: confirmación del catálogo, aviso previo a la tirada y efectos sobre recursos/atributos (Épica F).

Verificación (2026-11-08): `dotnet build/test backend/ProjectVTT.slnx` → 131 pruebas (36 unitarias + 95 integración, SQLite; 9 nuevas unitarias de `PlanRoll`/resolución con ventaja-desventaja y 2 de normalización de desglose antiguo, 1 nueva de integración con efecto `Envenenado` → `rollMode=disadvantage`, `dice` con 2 dados y `die=min`); web `tsc --noEmit && vite build` + 39 pruebas (Vitest; 1 nueva de `rollModeLabel`); migraciones sin drift (`has-pending-model-changes`); schema del evento comprobado como JSON válido. UI verificada en navegador con backend real + Vite: tirada con `Concentrado` (fuera de catálogo) sale en modalidad normal `1d20(8) + 3 + 0 + 0 = 11`; tras aplicar `Envenenado`, la tirada sale `Desventaja · 2d20(4,4) → 4 (desventaja) + 3 + 0 + 0 = 7 · Efectos: Envenenado (desventaja)`; 0 errores de consola. No probado con PostgreSQL/Docker (no disponible en el entorno) ni con E2E automatizado (Épica H).

Verificación (2026-11-06): `dotnet build/test backend/ProjectVTT.slnx` → 121 pruebas (27 unitarias + 94 integración, SQLite; 14 nuevas de inventario y 11 de efectos: CRUD, permisos 403/404/200, validación 400, conflicto 409 por versión y por unicidad, IDOR 404 y eventos `InventoryUpdated`/`EffectsUpdated` a dos clientes); web `tsc --noEmit && vite build` + 38 pruebas (Vitest; 4 nuevas de inventario y 3 de efectos); migraciones sin drift (`has-pending-model-changes`, `AddActorItems` y `AddActorEffects`). UI verificada en navegador con backend real + Vite: GM añade, edita y borra ítems, aplica y retira efectos (con 409 por duplicado); jugador en actor ajeno en solo lectura (sin controles) y en el suyo con permisos completos; entradas inválidas deshabilitan el envío; 0 errores de consola. No probado con PostgreSQL/Docker (no disponible en el entorno) ni con E2E automatizado (Épica H).

Verificación (2026-11-04): `dotnet build/test backend/ProjectVTT.slnx` → 96 pruebas (27 unitarias + 69 integración, SQLite; incluye eventos `ActorCreated`/`ActorUpdated` a dos clientes); web `tsc --noEmit && vite build` + 31 pruebas (Vitest); migraciones sin drift (`has-pending-model-changes`). UI verificada en navegador con backend real + Vite (flujo GM y jugador): creación de fichas, tiradas con desglose, permisos ADR-012 (jugador sin tirar ni editar en actores ajenos, sin controles GM) y evento `RollResolved` en vivo entre dos clientes. No probado con PostgreSQL/Docker (no disponible en el entorno) ni con E2E automatizado (Épica H).

## Épica F — Herramientas de sesión

- [x] **P1** Chat de campaña. → tabla relacional `chat_messages` (cuerpo 1..2000), `GET|POST /api/v1/campaigns/{id}/messages` (historial de 100) con permisos de miembro y evento `MessagePosted` (ADR-016, Propuesta)
- [x] **P1** Mensajes privados. → whispers 1:1 dentro de la campaña (alcance confirmado por el propietario 2026-11-09): tabla `direct_messages`, `GET|POST /api/v1/campaigns/{id}/direct-messages` con ambos participantes como miembros y evento `DirectMessagePosted` solo a los dos (ADR-017, Propuesta)
- [x] **P1** Iniciativa y turnos. → combate por escena (`scene_combats` + `combatants` con iniciativa manual y token opcional), inicio/avance/fin en `/api/v1/scenes/{id}/combat` (modificar es de GM+) y evento `CombatUpdated` con estado completo (ADR-018, Propuesta)
- [x] **P1** Niebla de guerra manual. → niebla por celdas del grid de la escena (`scene_fog_cells` guarda las reveladas), `GET|POST /api/v1/scenes/{id}/fog` (`reveal`/`hide`, GM+) y evento `FogUpdated` a la campaña; overlay en el mapa con huecos por celda (ADR-020, Propuesta)
- [x] **P1** Notas privadas de GM. → notas de campaña (`campaign_notes`, título 1..100 + cuerpo 0..5000) con CRUD en `/api/v1/campaigns/{id}/notes` y `/api/v1/notes/{id}` **solo para GM u Owner** (403 a jugadores, sin IDOR) y sin evento en vivo (ADR-019, Propuesta)
- [x] **P1** Historial de cambios. → registro append-only `campaign_history` escrito por el servidor en la misma transacción de cada mutación (escenas, tokens, fichas, inventario, efectos, tiradas y combate; **sin** notas ni mensajes privados), `GET /api/v1/campaigns/{id}/history` (100 recientes, miembros) y panel en la página de campaña (ADR-021, Propuesta)

Verificación (2026-11-09, historial de cambios): `dotnet build/test backend/ProjectVTT.slnx` → 191 pruebas (36 unitarias + 155 integración, SQLite; 7 nuevas de historial: registro de mutaciones ordenado, tiradas y combate, **notas y mensajes privados NO registrados (privacidad)**, límite de 100, lectura por jugador, 404 a no miembro y aislamiento entre campañas; los ids v7 hacen estable el orden con timestamps iguales); web `tsc --noEmit && vite build` + 67 pruebas (Vitest; 2 nuevas de `historyLabel`/`historyTime`); migración `AddCampaignHistory` sin drift (`has-pending-model-changes`). **Épica F completa.** No probado con PostgreSQL/Docker ni con E2E automatizado (Épica H).

Verificación (2026-11-09, niebla de guerra): `dotnet build/test backend/ProjectVTT.slnx` → 184 pruebas (36 unitarias + 148 integración, SQLite; 8 nuevas de niebla: estado inicial todo oculto, revelar idempotente, ocultar idempotente, niebla por escena, jugador lee pero 403 al modificar, no miembro 404, validación de celdas y evento `FogUpdated` a la campaña); web `tsc --noEmit && vite build` + 65 pruebas (Vitest; 4 nuevas de `emptyFogState`/`isRevealed`/`worldToCell`); migración `AddSceneFogCells` sin drift (`has-pending-model-changes`); schema del evento comprobado como JSON válido. No probado con PostgreSQL/Docker ni con E2E automatizado (Épica H).

Verificación (2026-11-09, notas del GM): `dotnet build/test backend/ProjectVTT.slnx` → 176 pruebas (36 unitarias + 140 integración, SQLite; 10 nuevas de notas: CRUD completo con GM invitado, edición parcial, 403 a jugador en leer/crear/editar/borrar, 404 a no miembro y a GM de otra campaña (sin IDOR), validación de `title`/`body` y borrado); web `tsc --noEmit && vite build` + 61 pruebas (Vitest; 5 nuevas de `upsertNote`/`removeNote`/`toCreateNoteRequest`/`toUpdateNoteRequest`); migración `AddCampaignNotes` sin drift (`has-pending-model-changes`). Sin evento en vivo (ADR-019). No probado con PostgreSQL/Docker ni con E2E automatizado (Épica H).

Verificación (2026-11-09, iniciativa y turnos): `dotnet build/test backend/ProjectVTT.slnx` → 166 pruebas (36 unitarias + 130 integración, SQLite; 17 nuevas de combate: inicio/fin con 409 en estados imposibles y reinicio, combatientes ordenados por iniciativa, nombre por defecto desde token, 404 de token de otra escena, edición y baja (baja del actual vacía el turno), avance con vuelta de ronda, 409 sin combatientes, permisos (jugador lee pero 403 al modificar, no miembro 404) y evento `CombatUpdated` a dos clientes); web `tsc --noEmit && vite build` + 56 pruebas (Vitest; 6 nuevas de `emptyCombatState`/`isCurrentTurn`/`toCreateCombatantRequest`; el test detectó y corrigió un bug: `Number('')` aceptaba la iniciativa vacía como 0); migración `AddSceneCombats` sin drift (`has-pending-model-changes`); schema del evento comprobado como JSON válido. No probado con PostgreSQL/Docker (no disponible en el entorno) ni con E2E automatizado (Épica H).

Verificación (2026-11-09, mensajes privados): `dotnet build/test backend/ProjectVTT.slnx` → 149 pruebas (36 unitarias + 113 integración, SQLite; 10 nuevas de mensajes privados: envío y conversación en ambos sentidos con orden y aislamiento entre pares, 201 jugador, 404 de no miembro y de destinatario no miembro, 400 por auto-envío/faltante/cuerpo inválido con campo `toUserId`/`body` y evento `DirectMessagePosted` a remitente y destinatario con **sin filtración a un tercer miembro**); web `tsc --noEmit && vite build` + 50 pruebas (Vitest; 6 nuevas de `peerOf`/`appendDirectMessage` con dedupe y límite, `applyDirectMessages` y `toCreateDirectMessageRequest`); migración `AddDirectMessages` sin drift (`has-pending-model-changes`); schema del evento comprobado como JSON válido. No probado con PostgreSQL/Docker (no disponible en el entorno) ni con E2E automatizado (Épica H).

Verificación (2026-11-08): `dotnet build/test backend/ProjectVTT.slnx` → 139 pruebas (36 unitarias + 103 integración, SQLite; 8 nuevas de chat: envío e historial con orden y límite de 100, 201 de jugador, 404 de no miembro en GET y POST, validación 400 con campo `body` y evento `MessagePosted` a dos clientes); web `tsc --noEmit && vite build` + 44 pruebas (Vitest; 5 nuevas de `appendMessage` con dedupe/límite y `toCreateMessageRequest`); migración `AddChatMessages` sin drift (`has-pending-model-changes`); schema del evento comprobado como JSON válido. No probado con PostgreSQL/Docker (no disponible en el entorno) ni con E2E automatizado (Épica H).

## Épica G — Escritorio

- [x] **P0/P1** Crear proyecto Godot si se confirma como cliente separado. → `apps/desktop` (Godot 4.7.2 .NET, ADR-006/ADR-030)
- [x] **P0/P1** Implementar autenticación y conexión. → `ApiClient` (login/registro con Bearer) y `SceneConnection` (SignalR `/hubs/campaign` con reconexión) (ADR-030)
- [x] **P0/P1** Consumir contratos compartidos. → DTO gemelos de `apps/web/src/api/types.ts` y `Realtime/Contracts.cs` (camelCase, enums como texto)
- [x] **P0/P1** Abrir campaña y escena. → vistas de Campañas y Escenas (crear, listar y abrir)
- [x] **P0/P1** Renderizar mapa y tokens. → mapa por `Image` (png/jpeg/webp) y chips de token arrastrables con snap al grid cuadrado/hex (ADR-023)
- [x] **P0/P1** Sincronizar movimientos. → `MoveToken` con predicción local y `expectedVersion`, eventos `TokenMoved`/`TokenCreated`/`MapUpdated`, resync ante conflicto (misma semántica que la web)
- [x] **P1** Alcanzar paridad funcional acordada. → la **acordada para el MVP es el vertical slice** (confirmación del propietario 2026-11-09, ADR-006); chat, mensajes privados, combate, niebla, notas e historial quedan como paridad amplia posterior

Verificación (2026-11-09, Épica G vertical slice): `dotnet build apps/desktop/ProjectVTT.Desktop.csproj` → 0 errores; **smoke end-to-end headless** (`Godot_v4.7.2 mono --headless --path apps/desktop -- --smoke` contra el backend real en `:5000`) → registro, campaña, escena, token, hub + `JoinCampaign`, `SyncScene`, `MoveToken` con resultado (256, 256), **evento `TokenMoved` recibido** con la versión correcta, `GET /map` sin mapa (404) y montaje de las vistas reales (campañas/escenas/escena construidas y cargadas) — todo OK; regresión backend 230/230 (48 unitarias + 178 integración + 3 de rendimiento + 1 E2E) y web 79/79. Encontrado y resuelto en el camino: (1) `SignalR.Client` 10.x + juego net8.0 = `MissingMethodException` en runtime (se fija a 8.0.33, ADR-030); (2) la BD demo (`%TEMP%\projectvtt-demo.db`) era anterior al esquema `scene_maps` y devolvía 500 en `/map` (recreada; trampa conocida: `EnsureCreated` no añade tablas). No probado: interacción visual/arrastre en ventana real (sin automatización de UI para Godot) ni mapas SVG.

## Épica H — Operación y calidad

- [x] **P0** Logs estructurados. → JSON por línea en consola (`JsonConsoleFormatter`, timestamp UTC), `RequestLoggingMiddleware` con plantilla estable por petición (método, ruta **sin query**, estado, duración, usuario) y sin secretos (garantizado por 5 tests unitarios); telemetría EF a Warning (ADR-022, Propuesta)
- [x] **P0** Gestión segura de configuración y secretos. → sin credenciales en el repo (user-secrets / `ConnectionStrings__Default` / `infra/docker/.env`), compose de desarrollo sin default inseguro y `StartupGuard.Validate` fail-fast sin imprimir valores (ADR-024, Propuesta)
- [x] **P0** Límites de subida de archivos. → validación de tipo, tamaño (`Assets:MaxBytes`, configurable, 10 MiB por defecto) y firma binaria en el servidor para la subida de mapas (ADR-028, Propuesta)
- [x] **P0** Pruebas de integración. → **interpretación confirmada por el propietario 2026-11-09: la suite de integración establecida** (178 de integración + 3 de rendimiento + 1 E2E sobre flujos críticos, más 48 unitarias; `dotnet test backend/ProjectVTT.slnx`) cumple `PROJECT_SPEC.md` §6 ("pruebas unitarias, integración y E2E para flujos críticos")
- [x] **P0** Pruebas E2E del vertical slice. → `tests/E2E` con xUnit + Microsoft.Playwright (navegador Chromium headless): fixture que arranca API real (SQLite) + `vite preview` y un test con dos usuarios por UI moviendo un token en una escena compartida (creación, selección, arrastre, persistencia y evento en vivo); corre en `dotnet test` (ADR-025, Propuesta)
- [x] **P1** Docker Compose self-hosted. → `infra/docker/compose.selfhosted.yml` (PostgreSQL + API + web con proxy nginx/WebSocket) + Dockerfiles de API y web + `docs/DEPLOY.md` (migraciones, verificación, backups con `pg_dump`, actualización y seguridad). **Documentado y compuesto; ejecución pendiente de un entorno con Docker** (decisión del propietario 2026-11-09)
- [x] **P1** Backups y restauración. → `BackupService` (instantánea SQLite en caliente + assets) expuesto como CLI `tools/ProjectVTT.Tools` (`backup`/`list`/`restore`) con scripts `ProjectVTT-backup/restore.ps1`; restauración offline con el servidor parado (ADR-029, Propuesta)
- [x] **P0/P1** Monitorización básica. → `/health` (liveness) + `/health/ready` (readiness con base de datos) + `/metrics` en formato Prometheus sin dependencias (contadores por método/estado, duraciones y gauges de proceso; solo agregados, sin datos sensibles) (ADR-026, Propuesta)
- [x] **P1** Pruebas de rendimiento. → `tests/Performance` con la API real (SQLite): lecturas (200/20), escrituras de chat (80/10, con conteo que demuestra que no se pierden) y tiradas (30/10), con calentamiento previo, percentiles p50/p95 y umbral de regresión (ADR-027, Propuesta)

Verificación (2026-11-09, backups y restauración): `dotnet test backend/ProjectVTT.slnx` → 230 pruebas (48 unitarias + 178 integración + 3 de rendimiento + 1 E2E, SQLite; 4 nuevas de backup: instantánea con base + assets restaurable (verificada abriéndola aparte), listado ordenado, **restauración completa** con el "servidor parado" (datos previos repuestos, posteriores eliminados, assets repuestos) y guard contra path traversal). CLI verificada en runtime: `backup` (286 KB + 27 assets), `list` y `restore` sobre la BD demo. Bugs detectados en el camino: `BackupDatabase` exige la conexión abierta y, sobre todo, **restaurar con handles de SQLite abiertos no surte efecto** (el pool debe vaciarse: la restauración real es offline). No probado con PostgreSQL/Docker (con PG el mecanismo es pg_dump, documentado).

Verificación (2026-11-09, mapas persistidos): `dotnet test backend/ProjectVTT.slnx` → 226 pruebas (48 unitarias + 174 integración + 3 de rendimiento + 1 E2E, SQLite; 9 nuevas de mapas: subida con round-trip de bytes, reemplazo, 404 sin mapa, tipo inválido 400, firma falsificada 400, tamaño excesivo 400, jugador solo lectura (403), no miembro 404 y evento `MapUpdated` con borrado); web `tsc --noEmit && vite build` + 79 pruebas (Vitest; 3 nuevas de `shouldReloadMap`/`mapWasRemoved`/`mapDisplayName`); migración `AddSceneMaps` sin drift. No probado con PostgreSQL/Docker ni con S3/MinIO (ADR-007 pendiente).

Verificación (2026-11-09, pruebas de rendimiento): `dotnet test backend/ProjectVTT.slnx` → 217 pruebas (48 unitarias + 165 integración + 3 de rendimiento + 1 E2E, SQLite; escenarios con la API real: lecturas 200/20 p50=3,7 ms p95=26,8 ms, escrituras de chat 80/10 p50=2,8 ms p95=782,6 ms (single-writer de SQLite bajo paralelismo) y tiradas 30/10 p50=6,1 ms p95=332,8 ms; todas con calentamiento previo y conteo de historial que demuestra **cero escrituras perdidas**). Un bug del propio test detectado y corregido: el jugador no se unía a la campaña (404 en las escrituras) y el arranque en frío contaminaba el p95 (se añadió calentamiento). No probado con PostgreSQL/Docker.

Verificación (2026-11-09, monitorización básica): `dotnet test backend/ProjectVTT.slnx` → 214 pruebas (48 unitarias + 165 integración + 1 E2E, SQLite; 3 unitarias nuevas de `MetricsCollector` (formato Prometheus, agregados por método/estado, escape de etiquetas) y 5 de integración (`/health` histórico, `/health/ready` con BD, `/metrics` con familias esperadas, contadores que crecen y **sin rutas ni credenciales**)); runtime verificado con el stack real: `health=ok`, `ready=Healthy` y `/metrics` con uptime/working set/contadores. No probado con PostgreSQL/Docker.

Verificación (2026-11-09, E2E del vertical slice): `dotnet test backend/ProjectVTT.slnx` → 206 pruebas (45 unitarias + 160 integración + 1 E2E, SQLite; la E2E arranca API real + `vite preview` + Chromium con Playwright y ejecuta el vertical slice completo por UI: dos usuarios, login, token creado por el GM, selección y arrastre por el jugador, persistencia confirmada en el servidor y recepción en vivo por el GM). Durante el desarrollo, los tests E2E detectaron y se corrigieron: carrera de clics contra la inicialización de PixiJS, espera de un botón solo-GM en la página del jugador y la diferencia de altura del canvas entre roles (la posición del token debe tomarse del servidor, no del centro). Nota: los anteriores "No probado con E2E automatizado" de estas notas quedan superados para el vertical slice. No probado con PostgreSQL/Docker.

Verificación (2026-11-09, configuración y secretos): `dotnet build/test backend/ProjectVTT.slnx` → 205 pruebas (45 unitarias + 160 integración, SQLite; 4 unitarias nuevas de `StartupGuard`: validación de proveedor y cadena de conexión, case-insensitive y **mensajes sin valores de configuración**). Escaneo del repo sin credenciales reales (solo el respaldo inerte del factory de diseño de EF, documentado); `appsettings.Development.json` sin contraseña (user-secrets) y `compose.dev.yml` exige `POSTGRES_PASSWORD` vía `infra/docker/.env` (`.env.example` incluido, `.env` en gitignore). No probado con PostgreSQL/Docker ni con E2E automatizado (Épica H).

Verificación (2026-11-09, logs estructurados): `dotnet test backend/ProjectVTT.slnx` → 196 pruebas (41 unitarias + 155 integración, SQLite; 5 unitarias nuevas de `RequestLoggingMiddleware`: campos del log, usuario/`anonimo`, **query string jamás logueado** (el hub acepta `?access_token=`), **`Authorization` jamás logueado** y log aunque la petición falle); runtime comprobado con el backend real: líneas JSON con timestamp UTC, `GET /health?access_token=SECRETO-QUERY` se loguea como `/health` (sin query), la contraseña del login no aparece y la telemetría EF queda a Warning. No probado con PostgreSQL/Docker ni con E2E automatizado (Épica H).

Verificación (2026-11-09, cierre del MVP): suite completa reejecutada como compuerta final — backend 230/230 (48 unitarias + 178 integración + 3 de rendimiento + 1 E2E), web 79/79 y smoke del escritorio `SMOKE OK` (vertical slice end-to-end con vistas montadas). Con las tres confirmaciones del propietario de este día (catálogo ADR-015, self-hosted documentado, pruebas de integración = suite actual) **el MVP queda funcionalmente completo** según `PROJECT_SPEC.md` §2: P0 16/16 y P1 8/8 (el self-hosted como «documentado mediante Docker Compose», con la ejecución real pendiente de Docker). Único trabajo humano pendiente del MVP: pruebas con usuarios reales (Fase 5).

## Primeras cinco tareas recomendadas

1. Resolver las decisiones pendientes de `DECISIONS.md`.
2. Inspeccionar el entorno real y crear el monorepo mínimo.
3. Preparar CI y Docker Compose.
4. Definir contratos de campaña, escena y movimiento de token.
5. Construir el flujo de dos usuarios moviendo un token en una escena compartida.
