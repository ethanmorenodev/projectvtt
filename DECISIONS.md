# ProjectVTT — Registro de decisiones (ADR)

Este documento registra decisiones arquitectónicas. Una propuesta no se considera aceptada hasta que su estado sea **Aceptada**.

## ADR-001 — Monolito modular para el MVP

**Estado:** Aceptada (2026-11-03)  
**Contexto:** el producto necesita varios dominios, pero el equipo inicial es pequeño y la integración distribuida añadiría complejidad.  
**Decisión:** desplegar un backend único organizado por módulos de dominio.  
**Consecuencias:** despliegue y depuración más simples; los límites de módulos deben mantenerse para evitar acoplamiento.

## ADR-002 — Backend autoritativo

**Estado:** Aceptada (2026-11-03)  
**Contexto:** varios usuarios pueden modificar una partida simultáneamente.  
**Decisión:** el servidor valida permisos y acciones, persiste el estado y publica eventos.  
**Consecuencias:** más trabajo de servidor, pero comportamiento consistente y menor riesgo de manipulación del cliente.

## ADR-003 — Clientes web y escritorio con contratos comunes

**Estado:** Aceptada (2026-11-03)  
**Contexto:** se desean clientes web y escritorio sin duplicar reglas de negocio.  
**Decisión:** contratos HTTP/eventos compartidos; web con React/TypeScript/PixiJS y escritorio con Godot/C# (versión 4.7.2, ver ADR-008), incluido en el MVP (ver ADR-006).  
**Consecuencias:** dos interfaces que mantener; contratos y pruebas de integración son críticos.

## ADR-004 — PostgreSQL como persistencia principal

**Estado:** Aceptada (2026-11-03)  
**Contexto:** se necesitan relaciones, permisos, consistencia y datos variables de Rulesets.  
**Decisión:** PostgreSQL, usando JSONB solo para partes variables y validadas del dominio.  
**Consecuencias:** modelo relacional para entidades principales y flexibilidad controlada para datos de sistema.

## ADR-005 — Primer Ruleset

**Estado:** Aceptada (2026-11-03)  
**Decisión:** **D&D 5e / 5.5e** como primer sistema de reglas.  
**Consecuencias:**
- Desbloquea la Épica E: interfaz del Ruleset, esquema de atributos y primera tirada.
- La restricción del Core sigue vigente: el núcleo mantiene conceptos genéricos y las reglas concretas viven solo en el Ruleset (`PROJECT_SPEC.md` §3, `ARCHITECTURE.md` §4).
- La versión concreta de la licencia/referencia de 5e vs 5.5e (2024) debe documentarse al definir el esquema de atributos.

## ADR-006 — Cliente de escritorio

**Estado:** Aceptada (2026-11-03; alcance de paridad confirmado el 2026-11-09; plataforma principal fijada el 2026-11-09)  
**Decisión:** **Godot 4 + C#, incluido desde el MVP**, conectado al backend común (mismos contratos que la web).  
**Paridad web/escritorio (confirmada por el propietario 2026-11-09):** el MVP del escritorio es el **vertical slice**: login, campañas, escenas, mapa, tokens y movimiento en vivo (el mismo flujo del E2E). El resto de la web (chat, mensajes privados, combate, niebla, notas, historial) llegará después; cada pieza añadida consumirá los mismos contratos HTTP/eventos ya existentes. **Plataforma principal (confirmada 2026-11-09): la web dirige el rediseño post-MVP y el escritorio se adapta posteriormente reutilizando los contratos** (ver ADR-039). Nota: `apps/desktop/project.godot` se restauró a su versión de repo (un editor no-.NET le había quitado la marca `C#`); Godot 4.7.2 .NET está instalado vía WinGet.

## ADR-007 — Alojamiento

**Estado:** Aceptada en su alcance self-hosted (2026-11-26; cierre de publicación EXP-46). Queda diferida la parte de cloud gestionado.
**Objetivo de producto:** cloud gestionado y self-hosted.  
**Decisión:** el despliegue self-hosted es el soporte oficial y está verificado (`docs/DEPLOY.md` §5: compose, migraciones, health/ready/metrics y seed). La versión de PostgreSQL es **17** (`postgres:17-alpine`), la misma que fija ADR-008 y se ha ejecutado de verdad; por tanto la duda «17 u 18» queda resuelta para producción. **No se elige proveedor cloud**: no hay despliegue real ni remote configurado, y elegirlo sin evidencia sería inventar una decisión irreversible. S3/MinIO sigue diferido: `IAssetStore` ya abstrae el almacenamiento y un backend surrogate se añadiría sin bifurcar la aplicación.
**Consecuencias:** la guía oficial cubre Docker Compose; `ADR-008` y `DEPLOY.md` referencian PostgreSQL 17. Para cloud hará falta una decisión del propietario con proveedor concreto y un despliegue real que la valide.

## ADR-008 — Toolchain de Fase 0

**Estado:** Aceptada (2026-11-03)  
**Contexto:** antes de escribir código hay que fijar versiones verificadas en lugar de elegirlas por inferencia (`ARCHITECTURE.md` §2).  
**Decisión:** versiones comprobadas en el entorno real de desarrollo y usadas como base de CI:

| Componente | Versión |
|---|---|
| .NET SDK / backend | 10.0.400 / `net10.0` |
| Node.js | 26.x |
| React / Vite / TypeScript / Vitest | 19.3 / 8.x / 7.x / 5.x |
| Godot + `Godot.NET.Sdk` | 4.7.2 (.NET) |
| PostgreSQL (compose dev) | 17-alpine |

**Consecuencias:** la tabla completa y el proceso de cambio están en `docs/CONVENTIONS.md`; actualizar CI, compose y documentación en el mismo cambio cuando se modifique una versión.  
**Resuelto:** PostgreSQL 17 para producción (ADR-007, verificado en el compose). El proveedor cloud sigue pendiente de decisión y despliegue real (ADR-007).

## ADR-009 — Autenticación y persistencia (Épica B)

**Estado:** Propuesta (implementada el 2026-11-03; pendiente de confirmación del propietario)  
**Contexto:** la Épica B necesita identidad de usuario, campañas con permisos y pruebas de integración sin depender de Docker.  
**Decisión:**
- Sesiones opacas: token aleatorio de 256 bits por sesión, solo se persiste su hash SHA-256 en `user_sessions`; el logout revoca la sesión (efectivo inmediato en cada petición).
- Envío del token como `Authorization: Bearer <token>` o `?access_token=` (para WebSocket/SignalR posteriores).
- Contraseñas con `PasswordHasher<T>` (PBKDF2, Microsoft.Extensions.Identity.Core, incluido en el framework compartido).
- EF Core 10 + Npgsql (PostgreSQL en desarrollo/producción) y SQLite en pruebas de integración; migraciones explícitas con `dotnet ef` (herramienta local del repo), sin auto-migrate al arrancar.
- Límite de peticiones por IP en registro/login (`RateLimit:*`).

**Alternativas consideradas:** JWT firmado (más complejidad de revocación y secretos que gestionar), Identity completo (demasiado alcance para el MVP), `EnsureCreated` en producción (no permite evolucionar el esquema).  
**Consecuencias:** cada petición consulta la base de datos para validar la sesión (aceptable al escala inicial); las pruebas no ejercitan PostgreSQL real (el DDL se valida con `migrations script` y el drift con `has-pending-model-changes` en CI).  
**Pendiente:** confirmación del propietario y decisión sobre caducidad por inactividad vs. fija (hoy: 720 min configurables).

## ADR-010 — Primer contrato de tiempo real (MoveToken / TokenMoved)

**Estado:** Propuesta (implementada el 2026-11-03; pendiente de confirmación del propietario)  
**Contexto:** la Épica C necesita el primer par comando/evento con idempotencia, concurrencia y resincronización (`ARCHITECTURE.md` §6-7; `packages/contracts/README.md`).  
**Decisión:**
- Hub SignalR `/hubs/campaign` autenticado con la sesión opaca existente (header o `?access_token=`). Las conexiones se agrupan por campaña solo tras verificar la membresía (`JoinCampaign`).
- Comando `MoveToken {commandId, sceneId, tokenId, expectedVersion, x, y}`; el servidor valida membresía, coordenadas y versión esperada, **persiste antes de emitir** y responde con la nueva versión.
- Idempotencia por `commandId` en tabla `processed_commands`: un reintento devuelve la respuesta original sin aplicar dos veces ni reemitir el evento.
- Concurrencia optimista con `Token.Version` como testigo de concurrencia de EF: si otro cambio ganó, el servidor responde conflicto.
- Evento `TokenMoved` con el envelope de `ARCHITECTURE.md` §7 (`schemaVersion = 1`), emitido al grupo de la campaña.
- `SyncScene` devuelve el estado autoritativo para carga inicial y reconexión (un cliente que reconecta nunca debe restaurar una posición obsoleta).
- Esquemas JSON Schema en `packages/contracts/events/`.

**Permisos provisionales:** cualquier miembro puede mover tokens y leer escenas; crear escena/token requiere GM o Owner. El control por token de los jugadores llegará con Actores (Épica E).  
**Alternativas consideradas:** transactional outbox (prematura sin flujo de eventos completo), tablas de outbox por evento (más complejidad de la necesaria ahora).  
**Consecuencias:** `processed_commands` crece sin poda (pendiente: retención/limpieza); las pruebas de integración usan LongPolling porque el TestServer en memoria no puede completar el handshake WebSocket (el transporte WebSockets no está cubierto por pruebas automatizadas).  
**Pendiente:** confirmación del propietario; retención de `processed_commands`; publicación fiable de eventos (outbox) cuando haya más emisores.

## ADR-011 — Cliente web y mapa (Épica D)

**Estado:** Propuesta (implementada el 2026-11-03; pendiente de confirmación del propietario)  
**Contexto:** la Épica D necesita mapa, cuadrícula, tokens y sincronización en el cliente web (`PROJECT_SPEC.md`, `BACKLOG.md`).  
**Decisión:**
- Cliente React 19 + Vite con `pixi.js` v8 encapsulado en `src/pixi/SceneStage.ts`; las vistas no dependen de Pixi directamente.
- Estado de sesión en `AuthContext` (token en `localStorage`, clave `projectvtt.token`); estado de tokens en `tokenStore` con funciones puras y guarda por versión (los eventos se aplican solo si traen versión más nueva).
- Movimiento con predicción local y confirmación del servidor: en conflicto se resincroniza con `SyncScene` (el servidor es la autoridad).
- **`gridCellSize` en escena** (8..512, por defecto 32) persistido en la base de datos, editable con `PATCH /api/v1/scenes/{id}` (GM+) y reflejado en `SceneState` para que todos los clientes dibujen la misma cuadrícula. Migración `AddSceneGridCellSize`.
- **Evento aditivo `TokenCreated`** (aditivo a ADR-010, `schemaVersion = 1`): la creación de tokens por HTTP se emite al grupo de la campaña vía `IHubContext` para que el resto de clientes la vea sin recargar. Esquema en `packages/contracts/events/token-created.event.v1.schema.json`.
- **Mapa importado solo en el cliente** (object URL + validación de tipo/tamaño: PNG/JPEG/WebP/SVG, 10 MiB): no se persiste ni se comparte entre clientes.

**Alternativas consideradas:** subir el mapa al servidor ya en esta épica (requiere object storage/ADR-007 y límites de subida, pendientes); grid hexagonal desde el inicio (P1).  
**Consecuencias:** cada cliente importa su propio mapa (no hay mapa compartido aún); el grid es cuadrado y fijo por escena; `TokenCreated` amplía el contrato de ADR-010 y debe anunciarse a los demás clientes (Épica G).  
**Pendiente:** persistencia/compartición de mapas (object storage, ADR-007); grid hexagonal (P1); confirmación del propietario.

## ADR-012 — Permisos de actores y tiradas (Épica E)

**Estado:** Aceptada (2026-11-04)  
**Contexto:** la Épica E introduce actores, atributos, recursos y tiradas. Hace falta decidir quién puede leer, editar y tirar con cada actor. Hasta ahora `ADR-010` dejaba el control por token de los jugadores para "Actores (Épica E)", y el código aplicaba una regla provisional que no estaba registrada.  
**Decisión:**
- Leer actores y listarlos requiere ser miembro de la campaña. Un actor de otra campaña, o inexistente, responde 404 sin revelar su existencia.
- Crear actores puede cualquier miembro de la campaña; el actor queda vinculado a su creador (`createdByUserId`), que es su propietario.
- Editar requiere ser el propietario (creador) o tener rol GM u Owner; sin permiso responde 403. La edición exige la versión esperada y responde 409 ante conflicto.
- Tirar dados (`RollService`) requiere ser miembro de la campaña: cada miembro tira con sus propios actores, y GM u Owner con cualquiera de la campaña (403 si no corresponde).
- El servidor lanza el dado; el cliente solo envía atributo, modificador y CD.
- Los eventos `ActorCreated`, `ActorUpdated` y `RollResolved` se emiten al grupo de la campaña. Esquemas en `packages/contracts/events/`.
- `TokenDto` y `TokenCreated` incluyen `actorId` (nullable). Es un cambio aditivo a ADR-010 y ADR-011.

**Alternativas consideradas:** que el GM asigne el propietario con un campo `ownerUserId` editable desde una UI de asignación (más flexible para reasignar fichas); que solo el GM pueda tirar con todos los actores. No se implementan: el propietario se fija al crear, por simplicidad.  
**Consecuencias:** un jugador no puede tirar ni editar fichas ajenas; los NPC los crea el GM y solo él tira con ellos. Un jugador puede crear su propia ficha dentro de los límites del Ruleset, visible para todos los miembros.  
**Pendiente:** transferencia de propiedad de un actor si se necesita más adelante.

## ADR-013 — Inventario básico por actor (Épica E)

**Estado:** Propuesta (implementada el 2026-11-06; pendiente de confirmación del propietario)  
**Contexto:** el ROADMAP (Fase 2) y el BACKLOG incluyen "Inventario básico" en la Épica E. Hay que decidir dónde se guardan los ítems, qué forma tienen (el producto es multisistema, `PROJECT_SPEC.md` §3), quién puede modificarlos y cómo se sincronizan entre clientes.  
**Decisión:**
- **Tabla relacional `actor_items`** (no JSON): `ARCHITECTURE.md` §8 exige persistencia relacional y prohíbe guardar todo el dominio como un único documento JSON; el inventario son datos de relaciones, no datos variables del Ruleset.
- **Ítem genérico `name` + `quantity`** (1..9999): sin peso, precio ni equipamiento, que son reglas de un sistema concreto; el significado lo define el Ruleset o la mesa.
- **Permisos = ADR-012**: leer requiere ser miembro de la campaña; crear, editar y borrar requiere ser el propietario del Actor o GM/Owner (404 si no es miembro, 403 si no corresponde). Implementado con los helpers `RequireActorAccessAsync`/`RequireEditableActorAsync` de `ActorService` (visibilidad `internal`), sin duplicar la regla en el servicio de inventario.
- **Concurrencia optimista por ítem** (`Version`, patrón Token/Actor): el `PATCH` exige `version` y responde 409 si el ítem cambió; el borrado no lleva versión (404 si no existe o pertenece a otro actor).
- **Evento `InventoryUpdated`** (aditivo a ADR-010, `schemaVersion = 1`): tras cada mutación se envía el inventario completo (`actorId` + `items`) al grupo de la campaña para que el cliente sustituya su lista. Sin `aggregateVersion`: la colección no tiene una única versión, cada ítem lleva la suya. Esquema en `packages/contracts/events/inventory-updated.event.v1.schema.json`.
- Las mutaciones HTTP devuelven `ActorInventoryDto` completo, con el mismo shape que el evento.

**Alternativas consideradas:** guardar el inventario como JSON dentro del Actor (menos tablas, pero viola `ARCHITECTURE.md` §8 y complica la concurrencia por ítem); un evento distinto por ítem creado/editado/borrado (más superficie de contrato sin necesidad); permisos más abiertos (que cualquier miembro edite cualquier inventario), rechazado por no coincidir con ADR-012.  
**Consecuencias:** el inventario es visible para todos los miembros y editable solo por su propietario o GM; amplía el contrato de ADR-010 y debe anunciarse a Épica G; la cantidad es un entero genérico, si un Ruleset necesita decimales habrá que extenderlo.  
**Pendiente:** confirmación del propietario; la automatización del Ruleset sobre el inventario (p. ej. consumir ítems en tiradas) queda fuera de este ADR.

## ADR-014 — Efectos y condiciones por actor (Épica E)

**Estado:** Propuesta (implementada el 2026-11-06; la ausencia de duración la confirmó el propietario el 2026-11-06; pendiente de confirmación del resto)  
**Contexto:** el BACKLOG (Épica E) incluye "Efectos y condiciones"; ROADMAP (Fase 3) y PROJECT_SPEC (P1) asocian las condiciones a los turnos, que aún no existen. `ARCHITECTURE.md` §4 define los efectos como concepto genérico del Core y deja su aplicación al Ruleset.  
**Decisión:**
- **Tabla relacional `actor_effects`** con solo `name` (1..100): **sin duración** — decisión del propietario (2026-11-06), nada puede decrementarla hasta que existan turnos (Épica F); se añadirá entonces como columna aditiva.
- **Unicidad por actor** (índice único `ActorId` + `Name`): una condición no se aplica dos veces; el segundo alta responde 409, también en carrera (vía `DbUpdateException` sobre el índice).
- **Permisos = ADR-012**: leer requiere ser miembro; aplicar y retirar requiere ser propietario del Actor o GM/Owner (404 si no es miembro, 403 si no corresponde), reutilizando `RequireActorAccessAsync`/`RequireEditableActorAsync` de `ActorService`.
- **Sin `PATCH` ni `Version`**: no hay campos que editar (se retira y vuelve a aplicar), así que no hace falta concurrencia optimista.
- **Evento `EffectsUpdated`** (aditivo a ADR-010, `schemaVersion = 1`): lista completa (`actorId` + `effects`) sin `aggregateVersion`, siguiendo el precedente de `InventoryUpdated`. Esquema en `packages/contracts/events/effects-updated.event.v1.schema.json`.
- La interpretación del efecto (a qué atributo modifica, si aplica modificadores o cuánto dura) es del Ruleset y queda fuera de esta tarea: ver "Automatización específica del Ruleset" (backlog).

**Alternativas consideradas:** duración en turnos desde el inicio (columna inerte hasta la Épica F); duración por tiempo real (`expiresAt`); clave machine separada de la etiqueta (hoy solo hace falta la identidad única; la clave puede entrar con el catálogo del Ruleset); permitir duplicados con pilas (exige reglas de apilado del Ruleset).  
**Consecuencias:** un efecto se identifica por su nombre visible (corregirlo implica retirar y reaplicar); amplía el contrato de ADR-010 y debe anunciarse a Épica G; al llegar los turnos habrá que decidir quién decrementa la duración y con qué evento.  
**Pendiente:** confirmación del propietario; duración/turnos (Épica F); aplicación a tiradas y atributos (Automatización del Ruleset).

## ADR-015 — Automatización de efectos en el Ruleset (Épica E)

**Estado:** Aceptada (2026-11-08 propuesta e implementada; la mecánica, el reconocimiento de efectos y **el catálogo** los confirmó el propietario el 2026-11-09)  
**Contexto:** el BACKLOG (Épica E) incluye "Automatización específica del Ruleset" y `ARCHITECTURE.md` §4 asigna al Ruleset la *aplicación de efectos*; ADR-014 dejó los efectos como nombres libres sin interpretar y anotó que la clave puede entrar con el catálogo del Ruleset.  
**Decisión:**
- **`IRuleset.PlanRoll(nombres)`**: el Core pasa los nombres de los efectos activos del Actor y el Ruleset devuelve un plan de tirada: cuántos dados lanza el servidor (1 en normal, 2 con ventaja o desventaja), la modalidad resultante y el modificador fijo acumulado. La aleatoriedad sigue viviendo solo en el servidor (`RollService`).
- **Mecánica (confirmada por el propietario 2026-11-08): ventaja/desventaja + modificadores.** Ventaja o desventaja → 2d20 y se conserva el mayor o el menor; ventaja y desventaja se anulan entre sí (regla de 5e, da igual el número de fuentes); los modificadores planos se suman y computan siempre.
- **Reconocimiento por nombre libre + catálogo sensible (confirmado por el propietario 2026-11-08):** el nombre tecleado se compara con el catálogo del Ruleset sin distinguir mayúsculas ni tildes; los conocidos se automatizan y los demás solo se muestran en la ficha. Sin migración y sin cambio en ADR-014; el nombre original (etiqueta) es lo que aparece en el desglose.
- **Catálogo inicial de `Dnd5eRuleset`** (solo en el servidor, la web no lo replica): ventaja: `ventaja`, `invisible`; desventaja: `desventaja`, `envenenado`, `aturdido`, `cegado`, `asustado`; modificador `+2`: `bendecido` (aproximación de Bendición); `−2`: `maldito`. Es una simplificación de 5e **confirmada por el propietario el 2026-11-09**: cambiarlo es tocar un diccionario del Ruleset, sin contrato ni migración.
- **Desglose ampliado de `RollBreakdown`** (aditivo a ADR-012/ADR-010, `schemaVersion` sigue en 1): `rollMode` (`normal`/`advantage`/`disadvantage`), `dice` (dados lanzados en orden), `effectModifier`, `effects` (nombre, tipo y valor de cada efecto reconocido) y `total = die + modifier + extra + effectModifier`; `die` pasa a ser "el dado conservado". Esquema actualizado en `packages/contracts/events/roll-resolved.event.v1.schema.json`; no es un evento nuevo.
- **Tiradas persistidas antes de este cambio** (historial e idempotencia en `processed_commands`) se normalizan al leer (`WithLegacyDefaults`): reciben `dice = [die]`, `rollMode = normal` y `effects = []`, sin reescribir la base de datos.

**Alternativas consideradas:** clave machine en `actor_effects` (migración y ampliación de ADR-014 para una identidad que hoy no hace falta); solo modificadores planos (más simple, pero no es la semántica de ventaja/desventaja de 5e, elegida por el propietario); exponer el catálogo en `GET /rulesets` para un aviso previo a la tirada (posible futuro, ahora la modalidad se muestra al recibir el resultado); interpretar también recursos (p. ej. ventaja con ventaja de dado) — fuera de alcance.  
**Consecuencias:** el desglose sigue siendo auditable por aritmética y ahora explica por qué una tirada tuvo ventaja o desventaja; amplía el contrato de `RollResolved` y debe anunciarse a Épica G (la web ya lo consume; el escritorio no usa el desglose); el catálogo vive en un solo sitio y su crecimiento es un cambio del Ruleset.  
**Pendiente:** aviso de efectos *antes* de tirar; aplicar efectos a recursos/atributos (p. ej. reducir `hitPoints`), que además dependerá de duración/turnos (Épica F).

## ADR-016 — Chat de campaña (Épica F)

**Estado:** Propuesta (implementada el 2026-11-08)  
**Contexto:** el BACKLOG (Épica F) incluye "Chat de campaña" y `PROJECT_SPEC.md` lo lista como herramienta de sesión; `ARCHITECTURE.md` §90 lo enumera como "Chat: mensajes". Las specs no fijan nombres de comandos/eventos para chat (`ARCHITECTURE.md` §7 aclara que sus nombres son ejemplos, no contratos).  
**Decisión:**
- **Tabla relacional `chat_messages`** (Id, CampaignId, UserId, Body 1..2000, CreatedAt) con FK en cascade a `campaigns` y `users` e índice `(CampaignId, CreatedAt)` (ARCHITECTURE §8: no JSON embebido). Sin `sceneId`: el chat es de campaña, no de mapa. Migración `AddChatMessages`.
- **Permisos:** cualquier miembro lee y envía (`RequireMemberAsync` → 404 para no miembros); el chat no distingue roles por encima de miembro.
- **Endpoints** `GET|POST /api/v1/campaigns/{id}/messages`; el POST valida el cuerpo con `InputGuard` (1..2000, campo `body`) y responde 201 con el mensaje guardado.
- **Evento `MessagePosted`** (envelope de ARCHITECTURE §7, sin `aggregateVersion` por precedente de `InventoryUpdated`) con `ChatMessageDto`, que incluye `UserName` resuelto al leer (join con `users`; no se denormaliza). Esquema en `packages/contracts/events/message-posted.event.v1.schema.json`.
- **Historial:** hasta 100 mensajes, de más reciente a más antiguo (como el de tiradas, con límite propio); el cliente lo invierte para mostrarlo en orden cronológico.
- **Sin idempotencia por `commandId`** (a diferencia de `MoveToken`/tiradas, ADR-010): un duplicado solo añadiría un mensaje; el cliente deduplica por id (la respuesta del POST y el evento traen el mismo mensaje).
- **Web:** segundo aside `ChatPanel` en `SceneView` (mismo patrón que `ActorPanel`), estado en vivo por `MessagePosted` y helper `src/chat/chat.ts` (`appendMessage` con dedupe y límite, `toCreateMessageRequest`).

**Alternativas consideradas:** mensajes como JSON por campaña (ARCHITECTURE §8 prohíbe el dominio como un único documento JSON y el chat crece sin límite); guardar en `processed_commands` como las tiradas (allí la fila existe por la idempotencia, que aquí no aplica); `sceneId` en el mensaje (el chat es de campaña, no por escena); idempotencia por `commandId` (coste de `processed_commands` para un caso de uso de baja pérdida: reintentar un chat duplicado es inocuo).  
**Consecuencias:** la tabla crece sin poda (pendiente de política de retención); el evento llega a todos los miembros de la campaña, insuficiente para mensajes privados (Épica F) que requerirán destinatarios; añade 2 endpoints y 1 evento nuevo a los contratos (Épica G).  
**Pendiente:** mensajes privados; límite de frecuencia de envío (rate limit) si el abuso llega a ser un problema; poda/archivo de historial antiguo.

## ADR-017 — Mensajes privados 1:1 dentro de campaña (Épica F)

**Estado:** Propuesta (alcance confirmado por el propietario el 2026-11-09; implementada el 2026-11-09)  
**Contexto:** el BACKLOG (Épica F) y `PROJECT_SPEC.md` §P1 incluyen "mensajes privados" sin definir alcance; ADR-016 anotó que su evento de chat (broadcast a la campaña) no sirve para mensajes privados. El propietario confirmó el alcance el 2026-11-09: **whispers 1:1 entre dos miembros de la misma campaña**, no una mensajería global de plataforma.  
**Decisión:**
- **Tabla relacional `direct_messages`** (Id, CampaignId, FromUserId, ToUserId, Body 1..2000, CreatedAt) con FK en cascade a `campaigns` y a los dos `users`, e índice `(CampaignId, FromUserId, ToUserId, CreatedAt)` (ARCHITECTURE §8). Migración `AddDirectMessages`.
- **Permisos:** los dos participantes deben ser miembros de la campaña (404 si no lo son, también al listar la conversación); no se puede enviar a uno mismo ni faltar destinatario (400, campo `toUserId`); el cuerpo se valida como en el chat (1..2000, campo `body`).
- **Endpoints** `GET /api/v1/campaigns/{id}/direct-messages/{otherUserId}` (conversación de 100 recientes, de más reciente a más antiguo) y `POST /api/v1/campaigns/{id}/direct-messages` (201 con el mensaje guardado).
- **Evento `DirectMessagePosted`** con `DirectMessageDto` (nombres de ambos participantes resueltos al leer). **No se emite al grupo de la campaña**: el servidor lo entrega solo a las conexiones de los dos participantes (`Clients.Users`), garantizando privacidad frente a terceros miembros. Esquema en `packages/contracts/events/direct-message-posted.event.v1.schema.json`.
- **Web:** el selector de conversación del `ChatPanel` alterna entre el canal de campaña (ADR-016) y las conversaciones privadas con cada miembro; el estado es `peerId → mensajes` (`chat/dm.ts`) con dedupe por id y límite de 100.

**Alternativas consideradas:** mensajería global de plataforma (decisión del propietario: fuera; el whisper vive en la campaña); eventos por grupo con subgrupos por par de usuarios (más complejo que `Clients.Users`, que ya resuelve por el `NameIdentifier` de la sesión); listar conversaciones con `GET /conversations` y paginación por cursor (innecesario para el MVP; el selector de miembros ya está); incluir los mensajes en `processed_commands` para idempotencia (igual que en ADR-016: un duplicado solo añadiría un mensaje).  
**Consecuencias:** privacidad garantizada solo a nivel de entrega del evento; los mensajes son legibles por el servidor (como todo el estado; sin cifrado extremo a extremo, fuera de alcance); la tabla crece sin poda (mismo pendiente que ADR-016); añade 2 endpoints y 1 evento a los contratos (Épica G).  
**Pendiente:** notificaciones de mensajes privados sin conversación abierta; mensajes privados a más de un destinatario (grupo); rate limit de envío.

## ADR-018 — Iniciativa y turnos por escena (Épica F)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** el BACKLOG (Épica F) y `PROJECT_SPEC.md` §P1 incluyen "combate, iniciativa, turnos y condiciones"; `ARCHITECTURE.md` §7 lista `AdvanceTurn`/`TurnAdvanced` como ejemplos de comando/evento (nombres de ejemplo, no contratos). ADR-014 dejó los efectos sin duración "hasta que existan turnos", así que esta ADR habilita ese futuro pero no modifica los efectos.  
**Decisión:**
- **Combate por escena** (el encuentro vive sobre un mapa): un único encuentro activo por escena (`scene_combats` 1:1 con la escena: `Round`, `CurrentCombatantId`, `StartedAt`).
- **Tabla `combatants`** (Id, SceneId, Name 1..100, `Initiative` −100..100 manual, `TokenId` opcional FK con `SetNull`, CreatedAt). Orden de turno por iniciativa descendente con empates por orden de alta (índice `(SceneId, Initiative, CreatedAt)`); se recalcula en cada consulta, sin columna de orden.
- **Endpoints:** `GET /api/v1/scenes/{id}/combat` y `POST .../combat/start|end|advance`, `POST .../combat/combatants`, `PATCH|DELETE .../combat/combatants/{combatantId}`. Leer es de miembro; toda modificación es de GM u Owner (es estado compartido de la mesa, como la configuración de la escena).
- **Evento `CombatUpdated` con el estado completo** (patrón de `InventoryUpdated`/`EffectsUpdated`) en lugar de `TurnAdvanced`: cada mutación reemplaza el estado, de modo que un evento perdido o fuera de orden no corrompe la UI y la reconexión es trivial. Esquema en `packages/contracts/events/combat-updated.event.v1.schema.json`.
- **Avance de turno:** pasa al siguiente por orden; al dar la vuelta incrementa la ronda; sin combatientes → 409; si se elimina al combatiente actual, el turno queda vacío (el siguiente avance va al primero).
- **El fin del combate borra** los combatientes y el estado (sin historial).
- **Iniciativa manual** tecleada por el GM (opcionalmente vinculando un token del mapa, cuyo nombre se toma por defecto); no hay tirada automática de iniciativa todavía.

**Alternativas consideradas:** tracker por campaña (mezclaría encuentros de distintos mapas); evento mínimo `TurnAdvanced` (el cliente reconstruiría el estado y un evento perdido rompería la UI); columna de orden explícito (redundante con el recálculo); tirada de iniciativa vía `IRuleset` (pendiente: requiere extender el Ruleset y decidir quién tira; ahora el GM teclea el valor); combate automático de todos los tokens de la escena (el GM debe poder excluir combatientes).  
**Consecuencias:** habilita la futura duración de efectos por turno, pero `actor_effects` **no se toca** (sigue sin duración, decisión del propietario 2026-11-06; ampliar ADR-014 requiere su confirmación); añade 7 endpoints y 1 evento a los contratos (Épica G); la web suma un tercer aside (`CombatPanel`) a la escena.  
**Pendiente:** duración de efectos decreciente al final del turno (extensión de ADR-014 por confirmar); tirada de iniciativa desde el Ruleset; condiciones automáticas por turno; historial de rondas.

## ADR-019 — Notas privadas del GM (Épica F)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** el BACKLOG (Épica F), `PROJECT_SPEC.md` §P1 y `ROADMAP.md` Fase 3 incluyen "notas privadas del GM": notas de sesión invisibles para los jugadores. Las specs no fijan alcance (campaña vs escena) ni comportamiento en tiempo real.  
**Decisión:**
- **Notas de campaña** (no por escena: son notas de preparación y sesión): tabla relacional `campaign_notes` (Id, CampaignId, Title 1..100, Body 0..5000, CreatedAt, UpdatedAt) con FK en cascade, índice `(CampaignId, CreatedAt)`. Migración `AddCampaignNotes`.
- **Permisos: TODO es de GM u Owner** (leer y escribir). Un jugador recibe 403 y un no miembro 404; `PATCH|DELETE /api/v1/notes/{noteId}` comprueba la campaña de la propia nota (sin IDOR entre campañas).
- **Endpoints** `GET|POST /api/v1/campaigns/{id}/notes` y `PATCH|DELETE /api/v1/notes/{noteId}` (validación con `InputGuard`: título obligatorio, cuerpo opcional).
- **Sin evento en vivo:** la UI vive en la página de campaña (`NotesPanel` en `ScenesView`), que no tiene conexión al hub; cada cambio se refleja con la respuesta de la API. Emitir un evento solo a GMs (vía `Clients.Users`, como ADR-017) queda como posible evolución si las notas se muestran también en la escena. Sin evento = cero riesgo de filtración a jugadores.

**Alternativas consideradas:** evento `NotesUpdated` solo a GMs desde ya (factible, pero hoy no tiene consumidores: la página de campaña no está conectada al hub); notas por escena (el GM toma notas de sesión, no del mapa); documento único por campaña tipo bloc de notas (la lista de notas independientes es más flexible y era lo que pedía "notas" en plural); markdown/adjuntos (fuera del MVP).  
**Consecuencias:** dos GMs no ven los cambios del otro en vivo (se refresca al volver a entrar); la tabla crece sin poda (mismo pendiente que chat); no se añaden eventos a los contratos (Épica G solo consume HTTP).  
**Pendiente:** evento en vivo solo a GMs si las notas entran en la escena; formato enriquecido; búsqueda.

## ADR-020 — Niebla de guerra manual por celdas (Épica F)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** el BACKLOG (Épica F), `PROJECT_SPEC.md` §P1 y `ROADMAP.md` Fase 3 incluyen "niebla de guerra manual". El mapa es solo cliente-side (object storage pendiente, ADR-007/011), así que la niebla no puede referirse a la imagen: se define sobre el grid compartido de la escena.  
**Decisión:**
- **Niebla por celdas del grid de la escena** (`GridCellSize`): `scene_fog_cells` guarda solo las celdas **reveladas** (clave compuesta `(SceneId, X, Y)`, FK en cascade); lo que no está ahí queda oculto para los jugadores. La celda de un punto del mundo es `floor(coord / GridCellSize)`.
- **Endpoints** `GET /api/v1/scenes/{id}/fog` (miembro) y `POST .../fog/reveal` / `.../fog/hide` (GM u Owner; idempotentes; coordenadas −10000..10000).
- **Evento `FogUpdated`** con el estado completo a toda la campaña: solo viajan celdas reveladas, así que no filtra nada secreto (a diferencia de otros eventos, aquí los jugadores también lo necesitan). Esquema en `packages/contracts/events/fog-updated.event.v1.schema.json`.
- **Render** (Pixi v8): overlay oscuro sobre el área del grid (±4096, la misma del grid) con un hueco por celda revelada (`Graphics.cut()`); tapa mapa **y tokens**. Los jugadores lo ven siempre; el GM solo en modo edición (checkbox "Editar niebla"), donde el clic revela/oculta la celda bajo el cursor (el arrastre sigue haciendo pan).

**Alternativas consideradas:** niebla por polígonos o pinceles (edición y triangulación mucho más complejas); rectángulos de revelado (menos granular para habitaciones irregulares); visión por personaje con radio de luz (futuro "visión y luz", fuera del MVP manual); tapar solo el mapa y no los tokens (filtraría posiciones enemigas); niebla por token individual (requeriría definir qué ve cada uno).  
**Consecuencias:** cambiar `GridCellSize` desplaza el significado de las celdas reveladas (pendiente: recalcular); la niebla solo cubre el área del grid; +3 endpoints y +1 evento (Épica G); la web añade `fog.ts` y el overlay al `SceneStage`.  
**Pendiente:** visión por personaje / radio de luz; semioscuro para el GM; recálculo de celdas al cambiar el tamaño del grid; edición por arrastre (pincel) y "revelar todo".

## ADR-021 — Historial básico de cambios (Épica F)

**Estado:** Propuesta (implementada el 2026-11-09; cierra la Épica F)  
**Contexto:** último elemento de la Épica F: "historial básico de cambios" (BACKLOG, `PROJECT_SPEC.md` §P1, `ROADMAP.md` Fase 3). Los eventos en vivo no se persisten (salvo las tiradas), así que el historial necesita su propio registro.  
**Decisión:**
- **Tabla `campaign_history` append-only** (Id, CampaignId, UserId, Kind 50, Summary 200, CreatedAt) con índice `(CampaignId, CreatedAt)`. `HistoryService.Record` solo añade al contexto: la mutación que lo llama la persiste **en su mismo `SaveChangesAsync`** (atómico, sin escrituras extra). Si la mutación falla (p. ej. 409 por versión), tampoco se registra.
- **Registrado (estado compartido):** escenas y tokens creados; fichas creadas/actualizadas; ítems añadidos/actualizados/eliminados; efectos aplicados/retirados; tiradas resueltas; combate iniciado/terminado. **NO se registran** notas del GM ni mensajes privados (privacidad; lo garantiza un test), ni mensajes de chat (ya son su propio historial).
- **Lectura:** `GET /api/v1/campaigns/{id}/history` (miembro), 100 recientes primero. Sin mutaciones por HTTP (solo lo escribe el servidor) y sin evento en vivo (la UI vive en la página de campaña, sin hub; se refresca al volver a entrar).
- **Ids v7 (monótonos por tiempo):** con timestamps iguales, el desempate del orden (`CreatedAt desc, Id desc`) sigue siendo estable.
- **UI** `HistoryPanel` en la página de campaña para todos los miembros: usuario, resumen ya redactado, hora UTC y etiqueta del tipo.

**Alternativas consideradas:** reconstruir el historial desde eventos (los eventos no se persisten y habría que garantizar outbox); log de auditoría con payloads completos (demasiado para "básico"); historial solo para GM (las acciones registradas son de estado compartido); paginación por cursor (100 recientes bastan para el MVP); idempotencia por comando (el historial registra hechos, no comandos).  
**Consecuencias:** 6 servicios tocan una línea cada uno (misma transacción); los resúmenes son texto ya redactado en español (localizarlos implica tocar los puntos de registro); la tabla crece sin poda (mismo pendiente que chat); **la Épica F queda completa**.  
**Pendiente:** filtros por tipo/usuario y paginación; registrar más acciones (niebla, cambios de grid, roles de miembros); localización de resúmenes; poda/rotación.

## ADR-022 — Logs estructurados (Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** `PROJECT_SPEC.md` exige "logs estructurados sin filtrar secretos ni datos privados innecesarios" (Épica H, P0). El backend logueaba texto plano con telemetría SQL de EF a nivel Information (ruidosa y con metadatos de sentencias).  
**Decisión:**
- **JSON por línea en consola** con el `JsonConsoleFormatter` incluido en .NET (sin dependencias nuevas): `Timestamp` UTC ISO-8601, `LogLevel`, `Category`, `EventId`, `Message`, `Exception`.
- **Niveles por configuración** (`appsettings*.json`): raíz `Information`; `Microsoft.AspNetCore` y `Microsoft.EntityFrameworkCore.Database.Command` a `Warning` (la telemetría SQL por petición es ruidosa e innecesaria en operación; los tests y el drift check ya cubren el acceso a datos).
- **Log por petición** (`RequestLoggingMiddleware`, tras la autenticación): plantilla estable `HTTP {Method} {Path} respondio {StatusCode} en {DurationMs} ms (usuario {UserId})`. Por seguridad: **sin query string** (el hub acepta `?access_token=`), **sin cabeceras, tokens ni cuerpos**; el usuario es el id (no el nombre) o `anonimo`.
- Los errores siguen viajando por `ApiException`/ProblemDetails y se loguean como excepción del pipeline, sin datos sensibles.

**Alternativas consideradas:** Serilog/Sink estructurados (dependencia nueva sin necesidad; el formatter de .NET basta para el MVP); loguear cabeceras y query para depurar (riesgo directo de filtrar tokens); telemetría EF a Information con `EnableSensitiveDataLogging` (jamás: expondría valores); logs en ficharo con rotación (operación futura; ahora consola, que es lo que ingieren Docker/cloud).  
**Consecuencias:** el log de consola deja de ser legible a simple vista (JSON); las herramientas de operación pueden ingerir campos estables; los tests unitarios del middleware garantizan que query y `Authorization` jamás se loguean.  
**Pendiente:** correlación por `traceId` en la plantilla; niveles configurables por entorno (serilog-esque); logs de auditoría de seguridad (logins fallidos) y rate limit de logs.

## ADR-023 — Grid hexagonal (Épica D)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** el BACKLOG (Épica D, P1) y `PROJECT_SPEC.md` §2 admiten "la hexagonal" como alternativa a la cuadrícula cuadrada. El grid actual es solo visual + snap; la niebla de guerra (ADR-020) define sus celdas sobre el grid de la escena.  
**Decisión:**
- **`GridShape` en la escena** ("square" por defecto, "hex"), columna en `scenes` (migración `AddSceneGridShape`, `defaultValue: "square"`) y cambio por GM+ en `PATCH /api/v1/scenes/{id}` (campo opcional: si no se envía, la forma se conserva; valor inválido → 400). El detalle y `SyncScene` la incluyen (aditivo al contrato).
- **Hexágonos flat-top con coordenadas "odd-q"** (columnas impares desplazadas media celda hacia abajo), matemática en `src/grid/hex.ts`: `size` = ancho del hexágono (igual que `GridCellSize` es el lado de la celda cuadrada); centro de celda `(1.5·R·col, √3·R·(row + col&1/2))` con `R = size/2`; celda de un punto por redondeo cúbico desde coordenadas axiales.
- **Render:** el grid hexagonal se dibuja como tres familias de líneas paralelas con la misma separación perpendicular (`√3·R/2`), no hexágono a hexágono (decenas de miles de formas en ±4096); mismo coste que el grid cuadrado.
- **Snap** (`snapPoint`) y **niebla** (`worldToGridCell`) son sensibles a la forma: las celdas de la niebla pasan a ser hexágonos odd-q (misma tabla `scene_fog_cells`: solo cambia la interpretación y el hueco del overlay, que ahora es un polígono hexagonal con `Graphics.cut()`).
- Cambiar la forma **reinterpretá las celdas de niebla ya reveladas** (misma limitación que cambiar `GridCellSize`; ver ADR-020).

**Alternativas consideradas:** hexágonos pointy-top (equivalente; flat-top es el más común en VTT); coordenadas axiales/cúbicas en la base de datos (la tabla guarda pares enteros, agnósticos a la forma); dibujar cada hexágono (prohibitivo en memoria); niebla solo para grids cuadrados (empeoraba el alcance de ADR-020 sin necesidad: la reinterpretación por forma es exacta); elegir la forma al crear la escena (el cambio por PATCH cubre ambos casos).  
**Consecuencias:** +1 campo en la escena y en `SceneState` (Épica G); la UI de la escena añade el selector "Forma" junto a "Celda"; niebla y snap reutilizan la misma matemática compartida (`grid/hex.ts`).  
**Pendiente:** medidas de hexágono (radio/distancia entre centros como alternativa a `size`); rejilla pointy-top; snap de tokens a celdas hexagonales con rotación; recálculo de niebla al cambiar forma o tamaño.

## ADR-024 — Gestión segura de configuración y secretos (Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** `PROJECT_SPEC.md` y AGENTS.md prohíben credenciales en el repositorio; existían dos: la contraseña de desarrollo en `appsettings.Development.json` y el un valor por defecto inseguro en `compose.dev.yml`.  
**Decisión:**
- **Sin credenciales en el repo:** `appsettings.Development.json` queda con `ConnectionStrings:Default` vacía; el backend toma la configuración de **variables de entorno** (`ConnectionStrings__Default`) o **`dotnet user-secrets`** en desarrollo (`UserSecretsId` en `Api.csproj`; `CreateBuilder` lo carga automáticamente en entorno Development).
- **Compose de desarrollo sin default inseguro:** `POSTGRES_PASSWORD` es obligatorio (`${POSTGRES_PASSWORD:?…}`) y se define en `infra/docker/.env` (gitignoreado; hay `.env.example` sin valores).
- **Fail-fast al arrancar** (`Shared/StartupGuard.Validate`): proveedor válido (`postgres`/`sqlite`) y cadena de conexión presente; **los mensajes jamás imprimen valores** de la configuración (podrían contener credenciales). Cubierto por tests unitarios, incluido el de no-filtrado.
- El resto de ajustes (`Auth:SessionMinutes`, CORS, rate limit) no contienen secretos; las sesiones son opacas hasheadas (ADR-009) sin secreto de firma que proteger.

**Alternativas consideradas:** vault/servicio de secretos (infraestructura fuera del MVP); cifrar la configuración (seguridad teatral: la clave viviría al lado); dejar credenciales de desarrollo "obviamente falsas" en el repo (una vez publicado el repo, cualquier credencial es sospechosa y viola la regla de AGENTS.md); dotnet user-secrets también en producción (no aplica: en producción mandan las variables de entorno del orquestador).  
**Consecuencias:** el arranque de desarrollo exige un paso más (user-secrets o `.env`); el mensaje de error guía al desarrollador; ninguna credencial queda en el historial futuro del repo (una credencial de desarrollo antigua quedó en el historial del repositorio local; esta publicación se hace como snapshot limpio sin ese historial).  
**Pendiente:** credencial de desarrollo retirada en ADR-024; validación de formato de la cadena de conexión; documentación de despliegue (Épica H, Docker self-hosted). Nota: el factory de diseño de EF (`AppDbContextDesignTimeFactory`) conserva un respaldo inerte (un valor inerte, usuario inexistente) solo para el tooling, y prefiere `ConnectionStrings__Default`.

## ADR-025 — Pruebas E2E del vertical slice (Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** la Épica H exige "pruebas E2E del vertical slice" (P0): el flujo "dos usuarios moviendo un token en una escena compartida" (BACKLOG, tareas recomendadas #5) solo se había verificado a mano con el navegador.  
**Decisión:**
- **`tests/E2E` con xUnit + `Microsoft.Playwright` 1.64** (única dependencia nueva: el driver de UI mantenido para .NET; alternativas: Selenium (más pesada y lenta) o hablar CDP a mano (mucho código propio)). Los navegadores se instalan con `Microsoft.Playwright.Program.Main(["install","chromium"])` en la primera ejecución.
- **Fixture `E2eAppFixture`:** arranca la API real (SQLite temporal, `http://127.0.0.1:5099`), la web construida (`vite preview` en `4173`, proxy hacia la API vía `VITE_API_TARGET`) y Chromium headless. Los procesos se drenan y se matan por árbol; la BD temporal se borra al terminar.
- **`VerticalSliceTests`** ejecuta el vertical slice con usuarios reales: preparación por API (usuarios, campaña, invitación, escena) y toda la interacción por UI — login, creación de token, selección por clic, arrastre del jugador, confirmación del servidor (posición persistida) y recepción en vivo por el GM (al pulsar la nueva posición se selecciona el token).
- **Diagnóstico de fallos:** captura + HTML de ambas páginas en el mensaje de error (acelera mucho el depurar).
- El E2E corre dentro del comando estándar `dotnet test backend/ProjectVTT.slnx` (1 prueba, ~4 s + arranque de servicios).

**Alternativas consideradas:** `@playwright/test` (Node) — separaría el E2E del ecosistema .NET de tests y duplicaría tooling; Selenium WebDriver — más fricción con apps React/canvas; E2E solo contra la API — no probaría la UI (el objetivo); fixtures por ensayo en lugar de por clase (con una clase basta); usar el navegador del agente (no es automatizable ni reproducible en CI).  
**Consecuencias:** el alcance E2E depende de Chromium descargado (red la primera vez); los puertos 4173/5099 son fijos (sin paralelismo entre clases E2E); `vite.config.ts` ganó `preview.proxy` y target configurable.  
**Pendiente:** más flujos E2E (chat, combate, niebla, permisos); ejecución en CI con navegador; sustituir los puertos fijos por aleatorios si crece el número de specs.

## ADR-026 — Monitorización básica (Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** la Épica H incluye "monitorización básica": aparte de los logs estructurados (ADR-022), la operación necesita salud y métricas scrapeables sin atar el MVP a un proveedor.  
**Decisión:**
- **`/health` se mantiene** con su contrato histórico (`{ status: "ok" }`, liveness).
- **`/health/ready`** — readiness con comprobación real de la base de datos (`DatabaseHealthCheck` sobre `CanConnectAsync`), con el health-check middleware incluido en ASP.NET (200 "Healthy" / 503).
- **`/metrics`** — formato de texto de Prometheus 0.0.4 **sin dependencias**: `MetricsCollector` (singleton thread-safe) registra desde `RequestLoggingMiddleware` contadores por método/código de estado y duraciones (`_count`/`_sum`), más gauges de proceso (uptime, working set, memoria del GC). Solo agregados: **nunca rutas con parámetros ni credenciales** (garantizado por test).
- Escape de etiquetas según la especificación de Prometheus; números en formato invariante a la cultura.

**Alternativas consideradas:** `prometheus-net` (dependencia nueva para ~100 líneas de exposición); OpenTelemetry completo (propio para trazas/dashboards, excesivo para el MVP); solo `/health` sin métricas (no permite alertas de saturación); métricas por ruta HTTP (filtraría ids de campaña/usuario en las etiquetas).  
**Consecuencias:** cualquier scraper Prometheus puede leer `/metrics` sin configuración; el formato es mínimo (sin histogramas por percentiles); al renombrar el middleware se mantienen los contadores (viven en el colector).  
**Pendiente:** histogramas/percentiles; métricas de dominio (tiradas, mensajes, movimientos); alertas y dashboard (fuera del repo); readiness de dependencias externas (object storage cuando exista).

## ADR-027 — Pruebas de rendimiento (Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** la Épica H incluye "pruebas de rendimiento": detectar regresiones de órdenes de magnitud y pérdidas de escritura bajo concurrencia, sin convertir el MVP en un laboratorio de benchmarking.  
**Decisión:**
- **`tests/Performance`** (xUnit, sin dependencias nuevas) con `PerfApiFixture`: arranca la API real (SQLite temporal, `127.0.0.1:5098`) y mide peticiones HTTP reales con `Stopwatch` por operación.
- **Tres escenarios**: lecturas concurrentes (200 ops / 20 en paralelo sobre 5 endpoints), escrituras concurrentes de chat (80 / 10, con conteo final del historial para demostrar que **no se pierden escrituras**) y tiradas concurrentes (30 / 10, mismo conteo).
- **Calentamiento previo** (10 ops, fuera de medición): sin él, el JIT y el primer acceso a EF contaminan el p95 (se vio: p50 4,8 ms con p95 1,2 s en frío).
- **Umbral de p95 = 1000 ms**, muy por encima de lo local (p50 de unidades de ms): detecta regresiones de órdenes de magnitud sin ser frágil entre máquinas. Los totales se mantienen bajo los límites de historial (100 mensajes / 50 tiradas) para que el conteo final detecte pérdidas.
- Los números medidos se reportan en la salida de cada test (p50/p95/max).

**Alternativas consideradas:** NBomber/k6 (dependencias y conceptos nuevos para un MVP); pruebas de carga sostenidas (el objetivo es regresión, no capacidad); medir en memoria sin HTTP (perdería el pipeline real); umbral ajustado a la máquina actual (frágil); escenarios de SignalR concurrente (el E2E ya cubre el flujo; añadirlo aquí duplicaría fixtures sin mucho valor).  
**Consecuencias:** las escrituras concurrentes muestran el single-writer de SQLite (p95 alto bajo paralelismo); con PostgreSQL los números serán mejores — las pruebas siguen siendo válidas como detección de regresión. Corren en `dotnet test` estándar (~3 s de carga).  
**Pendiente:** escenarios con SignalR (muchos clientes moviendo tokens); carga sostenida y memoria; medir también con PostgreSQL cuando haya Docker.

## ADR-028 — Persistencia y subida de mapas (Épica D/Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** `PROJECT_SPEC.md` §Escenas y mapas pide "subir/importar un mapa con validación de tipo y tamaño" y "límites configurables de subida" — funcionalidad del MVP. Hasta ahora el mapa era solo cliente-side (ADR-011 lo aplazó "por falta de object storage"), pero **el object storage (S3/MinIO) era solo el destino de despliegue propuesto** (ARCHITECTURE §"Archivos", ADR-007 aún en Propuesta), no un prerrequisito de la función.  
**Decisión:**
- **Almacén detrás de abstracción** (`IAssetStore`): la implementación por defecto es **sistema de archivos local** (`FileAssetStore`, raíz configurable `Assets:RootPath`, por defecto carpeta temporal; claves normalizadas contra path traversal). Cuando se confirme ADR-007, un backend S3/MinIO solo implementa esta interfaz.
- **Un mapa por escena** (`scene_maps`: FileName, ContentType, SizeBytes, CreatedAt, UpdatedAt; contenido bajo `scene-maps/{sceneId}`); subir otro lo reemplaza.
- **Endpoints:** `GET|PUT|DELETE /api/v1/scenes/{id}/map` — leer es de miembro (la imagen se sirve con `nosniff` y `Cache-Control: no-store`); subir y borrar es de GM+ (PUT multipart con campo `file`).
- **Validación en el servidor** (la web valida también por UX): tipo admitido (PNG/JPEG/WebP/SVG, los mismos que `validateMapFile`), tamaño entre 1 byte y `Assets:MaxBytes` (por defecto 10 MiB, **configurable**) y **firma binaria** (PNG/JPEG/WebP por magic bytes; SVG por su raíz `<svg`).
- **Evento `MapUpdated`** a la campaña con el `MapAssetDto` (o null si se borró): los clientes vuelven a pedir la imagen por HTTP autenticado y la muestran como URL de objeto (sin credenciales en la URL de la imagen).
- La web mantiene la validación previa y da vista inmediata con el archivo local; el evento sincroniza al resto.

**Alternativas consideradas:** exigir MinIO/S3 desde ya (bloquearía el MVP sin Docker/credenciales — era mi error previo de enfoque); guardar imágenes en la base de datos (ARCHITECTURE §8 separa assets); URL firmada/CDN (infraestructura de ADR-007); servir el mapa público sin auth (filtraría mapas entre campañas); aceptar cualquier binario por Content-Type (falsificable: por eso la firma).  
**Consecuencias:** la subida ya no es solo local: todos los miembros ven el mismo mapa (requisito del MVP); los límites de subida (Épica H) quedan cubiertos con validación de tipo/tamaño configurables; cambiar a S3/MinIO es un cambio de composición (`IAssetStore`).  
**Pendiente:** S3/MinIO cuando se confirme ADR-007; miniaturas; varios mapas por escena; servir SVG con `Content-Security-Policy` estricto si algún día se muestra inline; aviso de progreso en subidas grandes.

## ADR-029 — Backups y restauración (Épica H)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** la Épica H (y ROADMAP Fase 4) incluyen "backups y restauración". Con los mapas ya persistidos (ADR-028), una copia útil debe incluir base de datos **y** assets.  
**Decisión:**
- **Núcleo probado en el backend** (`BackupService`): la instantánea se toma con la **API de backup en caliente de SQLite** (`SqliteConnection.BackupDatabase`, consistente aunque haya transacciones) y se copia todo el `IAssetStore`. Cada backup es un directorio `backup-<fecha>/` con `database.db` + `assets/` bajo `Backups:RootPath` (por defecto, carpeta temporal; configurable).
- **Superficie: CLI de operación** (`tools/ProjectVTT.Tools`, comandos `backup`, `list`, `restore <id>`) — sin endpoints HTTP: backup y restauración son acciones del operador, no de los usuarios, y así no se añade superficie atacable ni secretos. Scripts de conveniencia: `scripts/ProjectVTT-backup.ps1` y `scripts/ProjectVTT-restore.ps1` (este detiene el servidor antes de restaurar).
- **La restauración es OFFLINE** (con el servidor parado): pisa la base y los assets. La prueba de restauración vacía el pool de conexiones de SQLite para simular el reinicio del proceso (con handles abiertos, la copia no surte efecto — lección aprendida y documentada).
- **Alcance SQLite**: la instantánea usa la API nativa de SQLite; con PostgreSQL el mecanismo es `pg_dump` (la herramienta lo dice explícitamente y falla con un mensaje claro). Configuración por variables de entorno, igual que la API (sin secretos).

**Alternativas consideradas:** endpoints REST de backup/restore (superficie de seguridad y privilegios ambiguos en un sistema multi-tenant); exportación lógica por SQL (más lenta y frágil que la copia nativa); backup solo de la base (perdería los mapas); copiar el fichero .db en caliente sin la API de backup (estado potencialmente inconsistente); dependencias de backup de terceros (innecesarias para el alcance).  
**Consecuencias:** operación sencilla para el despliegue actual (SQLite + disco local); el día que haya PostgreSQL/S3 (ADR-007/009) hay que documentar `pg_dump` y la copia del bucket; no hay retención automática de backups (el operador borra directorios antiguos).  
**Pendiente:** retención/rotación automática; verificación de integridad (checksum) al restaurar; soporte de PostgreSQL vía `pg_dump`; backups programados (desde el lanzador o el sistema operativo).

## ADR-030 — Cliente de escritorio: vertical slice (Épica G)

**Estado:** Propuesta (implementada el 2026-11-09)  
**Contexto:** con el alcance de paridad confirmado (ADR-006: vertical slice primero) y Godot 4.7.2 .NET instalado, procede implementar el cliente de escritorio que consuma los mismos contratos que la web.  
**Decisión:**
- **Arquitectura por capas en `apps/desktop`**, sin duplicar reglas de negocio: `Api/` (`ApiClient` HTTP con Bearer + `SceneConnection` SignalR sobre `/hubs/campaign`, DTO gemelos de `apps/web/src/api/types.ts` y `Realtime/Contracts.cs`), `State/` (réplica del `tokenStore` web con control de versiones y `GridSnap` cuadrado/hexagonal idéntico al web, ADR-023), `Ui/` (vistas **programáticas** —sin escenas `.tscn` intermedias—: Login, Campañas, Escenas y Escena con mapa y tokens arrastrables) y `Main.cs` con la navegación.
- **Semántica de movimiento idéntica a la web**: predicción local con `expectedVersion`, confirmación por `MoveTokenResult`, eventos `TokenMoved`/`TokenCreated`/`MapUpdated` aplicados solo con versiones más nuevas y resincronización (`SyncScene`) ante conflicto o reconexión.
- **Hilos**: los eventos de SignalR llegan en hilos del pool; se llevan al hilo principal por una cola drenada en cada frame (`Ui/MainThread`). 
- **Dependencia `Microsoft.AspNetCore.SignalR.Client` 8.0.x (fijada a 8.0.33)**, alineada con el TFM `net8.0` del juego: la rama 10.x mete `System.Text.Json` 10 en un juego net8.0 y produce `MissingMethodException` en runtime (sin runtime .NET 8 instalado el juego corre sobre 9/10 mediante roll-forward). El protocolo SignalR JSON es compatible con el servidor ASP.NET Core 10 (el cliente web hace lo propio).
- **Verificación con smoke end-to-end** (`--smoke`): un recorrido real contra el backend (registro, campaña, escena, token, hub, `SyncScene`, `MoveToken` + evento `TokenMoved`) más el montaje de las tres vistas, ejecutable en headless (`Godot --headless --path apps/desktop -- --smoke`) porque no hay automatización de UI para Godot.

**Alternativas consideradas:** UI con escenas `.tscn` (más fricción de edición para una app de formularios/listas); `System.Net.Http.Json` (provocó el fallo de versiones descrito; se serializa con `System.Text.Json` explícito); SignalR.Client 10.x (incompatible con el TFM del juego); duplicar la lógica de snap/estado sin portar la web (riesgo de divergencia entre clientes).  
**Consecuencias:** el escritorio cubre hoy login, campañas, escenas, mapa, tokens y movimiento en vivo; cada pieza nueva (chat, combate, niebla, historial) consume los contratos HTTP/eventos ya existentes; el arrastre de tokens y la interacción visual quedan sin automatizar (solo verificación manual).  
**Pendiente:** paridad del resto de funciones web; autoexamen visual en ventana real (el smoke es headless); empaquetado del ejecutable (export de Godot); mapas SVG en escritorio (`Image` de Godot no rasteriza SVG; la web sí los muestra).

## ADR-031 — Modelo de contenido (tipos, herencia, ámbitos y versionado)

**Estado:** Aceptada como dirección arquitectónica (2026-11-09; sujeta a validación técnica durante su implementación)  
**Contexto:** el post-MVP exige biblioteca universal, contenido homebrew y un creador de personajes que **no se apoye en catálogos provisionales** (decisión del propietario 2026-11-09). Detalle completo en `docs/architecture/CONTENT_MODEL.md`.  
**Decisión:** los **tipos de contenido son datos** (`TypeSchema` por sistema de juego: campos tipados, capacidades, relaciones permitidas); las instancias son `ContentItem` en árbol (categorías y variantes) con **herencia por campo** (`inherit`/`override`/`extend`), relaciones tipadas (`ContentRelation`), **ámbitos** (global/usuario/mundo/campaña/DM) con permisos por `grants` y flujo de publicación (borrador→propuesto→publicado), **versionado obligatorio** por revisiones inmutables con **uso fijado por personaje** (`ItemUsage` con snapshot materializado) y actualizaciones explícitas con diff (nunca silenciosas), e imágenes por referencia (`ImageAsset`, ADR-035). Las habilidades declaran estructura real (activación, efectos tipados+`manual`, duración, usos, costes, requisitos) y elecciones configurables (ChoiceGroups). La secrecía del DM se filtra **al serializar en servidor**.  
**Alternativas consideradas:** JSON libre sin esquema (flexible pero sin validación ni editor adaptativo); EAV tablas por propiedad (consultas y migraciones pesadas); una tabla por tipo de contenido (explosión de migraciones por sistema); ampliar `ActorItem` (agregado equivocado: el inventario de un personaje no es el catálogo del mundo).  
**Consecuencias:** el motor es genérico y los sistemas se añaden con datos; el creador de personajes (ADR-034) consume contenido real desde el primer día; los personajes son estables frente a ediciones de contenido gracias al snapshot; el servidor valida y versiona todo.  
**Validación ARQ (2026-11-10, puente PMB-P.1):** implementable como migración **aditiva** sobre el backend actual (tablas nuevas `ContentType/ContentItem/ContentRevision/ItemUsage/ContentRelation/ImageAsset/Grant`; nada del MVP se toca). El árbol + herencia por campo se resuelve con enlace de padre y conjuntos de campos sobreescritos (JSON validado por `TypeSchema` en servidor, mismo patrón de validación que `ImageGuard`); el versionado con snapshot de `ItemUsage` replica la semántica ya probada de versiones del MVP (tokens); `scope/grants` se apoya en la membresía/roles existentes; el filtrado de secretos al serializar sigue el patrón de «la autoridad es el servidor» ya aplicado. Riesgo controlado: `EnsureCreated` en SQLite no añade tablas a BD existentes (caveat ya documentado: recrear la BD demo al cambiar esquema).  
**Pendiente:** ~~confirmación del propietario (PMB-P.1)~~ → **confirmada por el propietario 2026-11-10** (PMB-P.1: desbloquea P.2–P.7); ADR específico del motor ampliado de efectos (Fase 5); migración de `ActorItem`/catálogo ADR-015 (Fases 5–6, con pruebas de equivalencia).

## ADR-032 — Mundos reutilizables (modelo híbrido)

**Estado:** Aceptada como dirección arquitectónica (2026-11-09; sujeta a validación técnica durante su implementación)  
**Contexto:** un mundo debe poder usarse en varias campañas sin que modificar una campaña altere a otras (plan §12; preferencia híbrida declarada por el propietario).  
**Alternativas estudiadas:**  
- **A. Copia total al crear la campaña** (cada campaña hereda una fotocopia del mundo): aislamiento perfecto y modelo simple; pero duplicación masiva, correcciones que no propagan y divergencia inevitable entre campañas hermanas.  
- **B. Referencias compartidas puras** (las campañas apuntan al contenido del mundo): una sola fuente de verdad y propagación inmediata; pero cualquier edición del mundo afecta a todas las campañas en el acto — rompe el requisito de no alterar otras campañas.  
- **C. Híbrido (elegido):** referencias + revisiones fijadas + **overrides por campaña** + **independizar (copiar) explícito** + **control explícito de actualizaciones** (mismo mecanismo de `ItemUsage`/diff del ADR-031).  
**Decisión (C):** el contenido vive en el ámbito del mundo y las campañas lo **usan con revisión fijada**; cada campaña puede declarar **overrides** (modificaciones locales que prevalecen sin tocar el original) y puede **independizar** un elemento (copia explícita al ámbito de campaña, con enlace de origen opcional). Las actualizaciones del mundo solo llegan a una campaña mediante la acción explícita «actualizar» (con diff y opción de conservar).  
**Consecuencias:** "modificar una campaña nunca altera a otras" se cumple por construcción (overrides + revisiones fijadas); la corrección de un error en el mundo se propaga solo donde el usuario lo decide; el modelo de datos gana `overrides` y la UI un gestor de actualizaciones; más complejidad que A/B pero es la única que cumple todos los requisitos simultáneamente.  
**Pendiente:** confirmación; primera implementación en Fase 7 (el modelo de ámbitos del ADR-031 ya lo anticipa).

## ADR-033 — Sistema de ventanas propio

**Estado:** Aceptada como dirección arquitectónica (2026-11-09; sujeta a validación técnica durante su implementación)  
**Contexto:** todas las herramientas (Character Hub, fichas, biblioteca, inspectores, chat, combate…) deben compartir **un solo** sistema de ventanas (plan §3), extensible sin rehacerlo, que mantenga la mesa accesible. Detalle: `docs/architecture/WINDOWS.md`.  
**Decisión:** gestor de ventanas propio en React (`WindowManager` + registro de `ToolDescriptor`): flotantes, arrastrables, redimensionables, minimizables (bandeja), z-order por foco, persistencia de posición/tamaño/distribuciones por usuario y contexto, bus de eventos de aplicación para interacción entre herramientas. Las herramientas del MVP se **adaptan** a este sistema conservando su lógica y pruebas.  
**Alternativas consideradas:** librerías de docking (dockview, react-mosaic: layouts acoplables que hoy no se necesitan, peso y dependencia añadidos, modelo de layout condicionante); un sistema por herramienta (expresamente prohibido por el plan).  
**Consecuencias:** dependencia cero, control total de persistencia e integración con la mesa; el coste es mantener nosotros arrastre/redimensionado/foco (~un componente pequeño y probado); si en el futuro se necesitaran docks complejos, se revisa esta decisión.  
**Pendiente:** confirmación; prototipo del gestor dentro de la Fase 1 (con aprobación visual).

## ADR-034 — Framework de creación de personajes por sistema

**Estado:** Confirmada (2026-11-09 como dirección; **confirmación ARQ 2026-11-10** en PMB-3.1, ver validación abajo)  
**Contexto:** el creador de personajes debe ser una experiencia visual por pasos y **no un asistente exclusivo de D&D 5e** (plan §7); las opciones proceden del contenido publicado en la campaña con permisos del jugador. Detalle: `docs/architecture/CHARACTERS.md`.  
**Decisión:** asistente **genérico** dirigido por `CharacterBlueprint` (datos por sistema: pasos, tipos de contenido, elecciones, validaciones, reglas de derivación); el `Actor` se amplía aditivamente (`kind`, retrato, biografía, `systemData`, `build` borrador) y fija el contenido usado con `ItemUsage` (ADR-031); las estadísticas derivadas y las validaciones se resuelven en **servidor** (incluida la previsualización vía endpoint de cálculo: las reglas no se reimplementan en el cliente). El Character Hub y la ficha clásica comparten estado y eventos (una sola verdad).  
**Alternativas consideradas:** asistente con lógica de 5e embebida (rápido pero rompe el principio multisistema y obligaría a rehacer); generar formularios crudos desde el esquema sin flujo (no satisface la experiencia exigida).  
**Consecuencias:** cada sistema nuevo = un paquete de datos (tipos + blueprint) sin tocar el asistente; el creador es utilizable desde la Fase 3 porque consume el modelo de contenido real (puente previo, sin catálogos provisionales).  
**Validación ARQ (2026-11-10, PMB-3.1):** el diseño es implementable sin cambios estructurales sobre lo construido: (1) la ampliación de `Actor` es aditiva sobre el endpoint/DTO existente del MVP; (2) el endpoint de previsualización sigue el patrón de autoridad del servidor ya aplicado en `RollService` (el cliente no calcula reglas); (3) `CharacterBlueprint` cabe como contenido tipado del ADR-031 (pasos = tipos + grupos de elección); (4) el borrador `build` persistente usa la misma semántica de versionado/snapshot que `ItemUsage`; (5) la Fase 3 puede empezar con un blueprint mínimo de 5e mientras exista contenido real publicado en la campaña (puente ADR-031), sin esperar al importador masivo.  
**Pendiente:** definición del contenido semilla del blueprint de D&D 5e (decisión del propietario: qué subconjunto del SRD entra primero — razas/clases/trasfondos); aprobación del mapa de pasos del creador (`docs/visual/screens/pmb-3.1-creador-flujo.md`).

## ADR-035 — Biblioteca de imágenes y miniaturas

**Estado:** Aceptada como dirección arquitectónica (2026-11-09; sujeta a validación técnica durante su implementación)  
**Contexto:** todo el contenido debe poder tener referencia visual con **miniaturas cuadradas y compactas**, reutilizando imágenes sin duplicar archivos (plan §6).  
**Decisión:** `ImageAsset` compartido (álbumes, etiquetas, búsqueda) referenciado por `imageId` desde cualquier contenido; variantes cuadradas generadas al subir (`64/128/256` + original, recorte `cover` con punto focal); almacenamiento en `IAssetStore` (ADR-028; S3/MinIO cuando se confirme ADR-007). **Generación de derivados en el cliente** (canvas) con validación en servidor de cada variante (tipo/firma/tamaño).  
**Alternativas consideradas:** generación en servidor con librería de imágenes (p. ej. SkiaSharp: potente pero añade dependencia y procesamiento al backend; ImageSharp queda descartado por su licencia Split); sin derivados, servir el original y escalar en cliente (mala experiencia en rejillas grandes y tráfico excesivo).  
**Consecuencias:** cero dependencias nuevas en el servidor y miniaturas instantáneas en la UI; el servidor sigue siendo la autoridad de validación; si aparece abuso o necesidad de derivados server-side, se implementa con SkiaSharp (revisión explícita).  
**Validación ARQ (2026-11-10, puente PMB-P.1):** coherente con lo construido — la subida de portadas del MVP ya valida tipo/firma/tamaño en servidor (`ImageGuard`) y el patrón «derivados en cliente + validación en servidor» es directo sobre él; `ImageAsset` es una tabla nueva aditiva y el almacenamiento reutiliza `IAssetStore` (ADR-028); los tamaños de miniatura ya están fijados en `DESIGN_TOKENS.md` §11 (48 px listas densas / 64 px destacadas, decisión del propietario).  
**Pendiente:** ~~confirmación del propietario (PMB-P.1)~~ → **confirmada por el propietario 2026-11-10** (PMB-P.1); especificación de tamaños ya fijada en `docs/visual/DESIGN_TOKENS.md` §11.

## ADR-036 — Paquetes de contenido con procedencia y licencias

**Estado:** Dirección respaldada por el propietario (2026-11-09: confirmación general + petición de ampliación §17); pendiente de validación técnica y del resultado de la investigación ampliada  
**Contexto:** el post-MVP incorpora contenido y assets externos (§17 del plan) y debe registrar procedencia, versión, licencia y atribución de cada conjunto, permitir importar sin duplicar y resolver conflictos de versión. Investigación completa: `docs/POST_MVP_ASSETS_RESEARCH.md`.  
**Decisión:** agregado **`ContentPack{key, name, version, system, sourceUrl, license, licenseUrl, attribution[], importedAt}`**; todo `ContentItem` e `ImageAsset` pertenece opcionalmente a un paquete y hereda su procedencia (`ImageAsset` amplía con `license/attribution/origin/author`). Formato de paquete importable **`.pvttpkg`** (zip: `manifest.json` + `content.json` + `assets/` + `LICENSES/`), identidad de elementos por `(paquete, key)` con reimportación idempotente y resolución explícita de conflictos (actualizar / conservar / copiar — nunca silenciosa, como el versionado del ADR-031). Pantalla de **Créditos y licencias** con exportación de la atribución. La importación valida estructura/tipos/tamaños (misma política que las imágenes) y **no ejecuta código**.  
**Alternativas consideradas:** licencias solo en documentación (se pierden al distribuir campañas/paquetes); identidad global de items sin paquete (imposible distinguir duplicados ni actualizar); paquetes como copias sueltas (romperían las referencias `imageId`).  
**Consecuencias:** cualquier fuente externa entra con su licencia registrada; el creador de personajes no cambia (un paquete es contenido con ámbito); la distribución de campañas puede cumplir CC BY exportando atribuciones.  
**Pendiente:** confirmación; formato definitivo del manifest al implementar (Puente 2→3).

## ADR-037 — Contenido de D&D 5e mediante el SRD (CC BY 4.0)

**Estado:** Dirección respaldada por el propietario (2026-11-09: punto 6 — contenido real oficial y legal — y confirmación general); pendiente de validación técnica  
**Contexto:** D&D 5e debe ser el primer sistema **con contenido real**, sin ejemplos inventados (§17.2), respetando licencias: no todo el contenido publicado de D&D es redistribuible. Verificado el 2026-11-09: el **SRD 5.1** y el **SRD 5.2.1** están publicados por Wizards of the Coast bajo **Creative Commons BY 4.0** y pueden convivir en el mismo producto.  
**Decisión:** el primer paquete oficial es **`dnd5e-srd-5.1`** (razas, clases con su subclase de SRD, trasfondos, hechizos reales con todos sus campos y descripción, objetos, criaturas, condiciones y reglas), importado desde fuentes machine-readable y **verificado contra el SRD oficial**, con atribución CC BY 4.0 obligatoria (pantalla de créditos y metadatos del paquete). El **`dnd5e-srd-5.2`** será un segundo paquete (arquitectura multi-paquete, sin cambios en el creador). **Política de contenido:** solo SRD/CC; **nunca** contenido de suplementos, arte de Wizards ni materiales protegidos (el arte del SRD no está en CC — los iconos de contenido proceden de fuentes propias/CC, p. ej. game-icons.net CC BY 3.0). Marcas "Dungeons & Dragons"/"D&D" solo con uso nominativo ("compatible con el SRD"). Paquetes de terceros (editores, DM Guild) entran como paquetes importables con su licencia registrada.  
**Alternativas consideradas:** contenido inventado de ejemplo (viola el requisito del propietario); datos de D&D Beyond o wikis (protegidos); reutilizar datos de proyectos OGL antiguos sin verificar (términos mixtos; preferir el material CC BY del SRD).  
**Consecuencias:** el creador de personajes y la biblioteca arrancan con contenido real y legal; la traducción al español es trabajo derivado propio (permitido, indicando cambios) — alcance pendiente; la app educa al usuario sobre lo que puede importar.  
**Validación ARQ (2026-11-10, PMB-P.8):** la arquitectura multi-paquete ya está implementada: `ContentPack` con licencia/atribución/origen, identidad de elementos por `(paquete, key)` y reimportación idempotente con resolución explícita de conflictos (Keep/Update/Copy, nunca silenciosa — `Update` publica revisión). El paquete `dnd5e-srd-5.1` puede importarse por esta vía (PMB-P.4) con su atribución CC BY 4.0 registrada; la verificación de la licencia del SRD 5.1/5.2.1 (CC BY 4.0) se hizo el 2026-11-09 en `docs/POST_MVP_ASSETS_RESEARCH.md`.  
**Pendiente:** ~~confirmación~~ → **confirmada por el propietario 2026-11-10** (SRD 5.1 primero; alcance de traducción aparte, plan §16).

## ADR-038 — Idioma e internacionalización

**Estado:** Aceptada (2026-11-09; decisión del propietario)  
**Decisión:** **español como idioma inicial**, pero la arquitectura queda **preparada para i18n desde el principio**: los componentes nuevos reciben todos sus textos por props (sin cadenas embebidas), las pantallas del rediseño usan un **catálogo de cadenas** unificado desde la Fase 2, y el contenido (nombres del SRD, textos de usuario) se separa de la chrome de la aplicación. La localización de la app (otros idiomas) queda para después del post-MVP sin cambios estructurales.  
**Consecuencias:** coste mínimo desde el inicio (evita reescribir textos después); el catálogo de cadenas es un fichero de recursos simple (sin librería de i18n hasta que haga falta).  
**Formato del catálogo (definido 2026-11-10, en la Fase 2):** `apps/web/src/i18n/es.ts` exporta un objeto tipado (`as const`) organizado por área (`shell`, `hero`, `inicio`, `campanas`, `campana`, `mesa`, `comunes`), con funciones para plurales e interpolación (`participants: (count) => …`, `manage.title: (name) => …`); `i18n/index.ts` exporta `t` (catálogo activo). Localizar = añadir otro catálogo con la misma forma y cambiar la exportación. El shell, las vistas de la Fase 2 (`InicioView`, `CampaignsView`, `CampaignManageDialog`, `CampaignSpaceView`, `ScenesView`) y la mesa (`SceneView`) ya consumen `t`; los textos visibles son idénticos a los aprobados (las pruebas los aseguran). Quedan por migrar los paneles del MVP que viven dentro de las ventanas (`ActorPanel`, `CombatPanel`, `ChatPanel`, `NotesPanel`, `HistoryPanel`) en su barrido de rediseño.  
**Pendiente:** traducción del contenido SRD como decisión aparte (plan §16).

## ADR-039 — Plataforma principal, repositorio y CI

**Estado:** Aceptada (2026-11-09; decisión del propietario)  
**Decisión:** (1) **La web es la plataforma principal** durante el rediseño post-MVP; el cliente de escritorio se adaptará **después**, reutilizando los contratos compartidos (actualiza ADR-006). (2) **El repositorio no se publica**; si hubiera **CI privada** disponible se utilizaría; mientras, la verificación sigue siendo local y automatizada (`dotnet test` 230, web 79, smoke del escritorio) con los scripts del repo.  
**Alternativas consideradas:** publicar el repo para usar la CI de GitHub (descartado por preferencia del propietario); paridad web/escritorio en paralelo (duplicaría el esfuerzo de rediseño).  
**Consecuencias:** el rediseño avanza en una sola plataforma con red de seguridad completa local; la CI privada se conecta cuando exista (el `ci.yml` actual queda como referencia).  
**Pendiente:** detectar/conectar una CI privada si el propietario la pone a disposición.

## ADR-040 — Motor ampliado de efectos como datos (Fase 5)

**Estado:** Aceptada (2026-11-16; aprobación del propietario tras aviso por WhatsApp; PMB-5.1)  
**Contexto:** ADR-015 automatizó ventaja/desventaja y modificadores con un **catálogo en código** del Ruleset (`Dnd5eRuleset.EffectCatalog`), confirmado por el propietario. Ese catálogo sirve bien para los nombres del SRD, pero **no permite homebrew**: una habilidad creada por un jugador no puede declarar sus efectos sin tocar código. El modelo de contenido ya trató los tipos como datos (ADR-031 §3) y las habilidades/efectos de `CONTENT_MODEL.md` están previstos como contenido; el creador (ADR-034) y la biblioteca necesitan una representación común. `IRuleset.PlanRoll(IReadOnlyList<string>)` hoy solo recibe nombres.  
**Decisión:**
- **La habilidad es contenido, no código:** un `ContentItem` del tipo `ability` (datos por sistema) declara en `properties`: `activation` (`action`/`bonus`/`reaction`/`free`), `effects[]`, `duration` (`instant`/`rounds:N`/`concentration`), `uses` (`max` + `reset`), `costs[]` (recurso/cantidad), `requirements[]` y `resolution` (`auto`|`manual`). El tipo valida keys y tipos en servidor (`ContentSchema`), igual que razas/clases.
- **Efecto tipado:** cada entrada es `{key, kind, value, target?}` con `kind ∈ {advantage, disadvantage, modifier, resource, condition}`. Los nombres `key` del catálogo ADR-015 siguen siendo la semántica canónica de dnd5e; un efecto con `kind` desconocido o sin regla determinista se marca **`manual`** y **nunca** se resuelve automáticamente.
- **Resolución en servidor:** un `EffectResolver` del Core traduce `effects[]` (de la habilidad) + `ActorEffect` activos (ADR-014) a la entrada de `IRuleset.PlanRoll` y aplica efectos de recurso al `Actor` con auditoría en `HistoryEntry` (`EffectApplied`/`EffectRemoved`). La aleatoriedad sigue en `RollService` (ADR-015) y `RollResolved` mantiene `schemaVersion = 1` (sin contrato nuevo).
- **Asistida vs automática:** `resolution=auto` → el motor aplica el efecto y el desglose lo explica; `resolution=manual` → la UI (web y escritorio) muestra los pasos y el jugador/DM confirma, sin que el motor inventar reglas. Los efectos del ADR-015 clasificados como `manual` por el Ruleset siguen ese mismo camino.
- **Versionado y procedencia:** al usar una habilidad se crea un `ItemUsage` con el snapshot de sus propiedades efectivas (ADR-031 §7 / PMB-4.9), de modo que cambiar una revisión no altera usos anteriores; los paquetes importados conservan licencia/atribución (ADR-036/037).
- **Equivalencia obligatoria:** el catálogo en código de ADR-015 se convierte a datos y una prueba (PMB-5.6) compara ambos: mismos nombres → mismo `RollPlan`, mismo desglose. Es la red de seguridad antes de borrar el catálogo en código; hasta que pase, el catálogo sigue siendo la autoridad de dnd5e.
- **Sin ejecución de código:** las habilidades son datos validados; no se interpretan expresiones ni scripts (misma política de importación de ADR-036).

**Alternativas consideradas:** mantener los efectos solo en código (simple, pero imposibilita el homebrew y contradice el modelo de tipos como datos — descartada); permitir scripts en las habilidades (flexible, pero ejecución arbitraria en servidor — descartada por seguridad ADR-036); resolver en el cliente (rompe ADR-002: el servidor es la autoridad — descartada).  
**Consecuencias:** `IRuleset` podrá ampliarse de forma **aditiva** con una sobrecarga que acepte efectos tipados, sin cambiar `PlanRoll(nombres)` durante la transición; el escritorio consume los mismos contratos (ADR-039). La UI deberá distinguir claramente «automático» de «asistido» y explicar cada efecto aplicado. El creador de personajes no cambia su flujo (ADR-034/037): las habilidades aparecen como otro tipo de contenido elegible.  
**Pendiente:** aprobación del propietario; después, PMB-5.2 (estructura), 5.3 (ChoiceGroups), 5.4 (resolución + hechizos del SRD), 5.5 (permisos/versionado), 5.6 (equivalencia) y 5.7 (casos de referencia).

## ADR-041 — Espacios de equipamiento configurables por sistema (Fase 6)

**Estado:** Aceptada (2026-11-17; aprobación del propietario; PMB-6.1)  
**Contexto:** el inventario MVP es una lista simple (`ActorItem{Name, Quantity}`); el plan de Fase 6 exige una **silueta con huecos** configurables por sistema (§9 del plan, `CHARACTERS.md` §equipment: «arma principal, secundaria, armadura, casco, guantes, botas, accesorio 1/2»), y `CONTENT_MODEL.md` deja `EquipmentSlot` como agregado de Fase 6. Hoy no hay columna de hueco ni de objeto de contenido referenciado, y las reglas de peso/efecto de equipamiento llegan con PMB-6.5 (hook `ApplyEquipment`).  
**Decisión:**
- **Los huecos son datos del sistema, no código:** cada blueprint (`Characters/Blueprints/<sistema>.json`) declara `equipmentSlots[]`: `{ id, label, icon?, accepts: [typeKey…], optional?, multiple? }`. Se cargan al arrancar con el blueprint (sin migración ni tabla nueva); el servidor valida que el hueco existe, acepta el tipo del objeto y que no se duplica salvo `multiple`.
- **`ActorItem` se amplía de forma aditiva** (migración): `SlotId` (nullable: `null` = mochila), `ContentItemId` (nullable, referencia al `ContentItem` del catálogo) y `Weight`/`Volume` (numéricos opcionales heredados del objeto de contenido; el sistema decide qué usa). Un objeto equipado se identifica por `SlotId` no nulo; desequipar lo devuelve a la mochila.
- **Autoridad en servidor:** equipar/desequipar validan membresía, propiedad del actor (o GM), existencia del hueco y compatibilidad de tipo; el evento `InventoryUpdated` (ADR-010) sigue informando a la campaña. Peso/efectos agregados llegan en PMB-6.5 (`ApplyEquipment`) reutilizando `EffectResolver`/ADR-040.
- **UI de la silueta** (PMB-6.4) consume el mismo DTO ampliado (`slotId`, miniaturas `thumb-sm` de la biblioteca de imágenes) y se aprueba con prototipo visual antes de integrar; la web no calcula reglas.

**Alternativas consideradas:** tabla `EquipmentSlots` por sistema (más consultable, pero duplica un dato que ya vive en la configuración de blueprint y exige migración/sincronización — descartada); huecos hardcodeados en el cliente (viola el requisito §9 y ADR-002 — descartada); modelar huecos como `ContentItem` (serían contenido publicable; innecesario porque no tienen versionado propio — descartada).  
**Consecuencias:** el creador de personajes y la ficha pueden mostrar «Equipo» sin reglas nuevas (ADR-034 se mantiene); el inventario MVP existente sigue funcionando (`SlotId` opcional); la migración es aditiva y reversible.  
**Pendiente:** aprobación del propietario; después PMB-6.2 (objetos como contenido), 6.3 (prototipo visual del inventario), 6.4 (UI de silueta/huecos) y 6.5 (`ApplyEquipment`).

## ADR-042 — Diseño de mundos: overrides, copias y actualizaciones (PMB-7.1)

**Estado:** Aceptada (2026-11-17; aprobación del propietario; PMB-7.1; confirma y detalla ADR-032)  
**Contexto:** ADR-032 eligió el modelo híbrido (referencias + revisiones fijadas + overrides por campaña + independizar explícito + actualizaciones controladas) pero está «sujeta a validación técnica durante su implementación»; el modelo ya anticipa `ContentScope.World` y `ContentItem.WorldId`, pero no hay agregado `World`, relación campaña↔mundo ni almacenamiento de overrides. Fase 7 debe convertir esa dirección en contratos concretos sin romper el aislamiento entre campañas (ADR-031 §6/§7).  
**Decisión:**
- **Agregados aditivos:** `World{id, system, name, description, ownerUserId, createdAt}` (migración), `Campaign.WorldId` opcional y `WorldContentOverride{campaignId, contentItemId, baseRevision, patchJson}` con índice único `(campaignId, contentItemId)`. El contenido del mundo sigue siendo `ContentItem` con `scope=world`, `worldId` y revisión publicada.
- **Uso con revisión fijada:** al usar contenido del mundo en una campaña se registra esa revisión como `baseRevision` del `WorldContentOverride` de la campaña; los cambios posteriores del mundo **no** alteran ese uso. Los usos de personaje (`ItemUsage`, ADR-031 §7) siguen fijando su propia revisión.
- **Overrides por campaña:** un `WorldContentOverride` guarda solo las claves modificadas (`patchJson`) frente a `baseRevision`; el servidor compone `contenido base (revisión fijada) + override` al servirlo a esa campaña. Un override nunca escribe el item original.
- **Independizar (copiar):** `POST /content/items/{id}/independentize {campaignId}` crea un `ContentItem` con `scope=campaign`, `campaignId` y metadato `sourceWorldItemId`/`sourceRevision`. La copia es autónoma: deja de recibir cambios del mundo. Se permite porque el contenido ya soporta procedencia (ADR-036).
- **Actualización explícita:** `POST /content/items/{id}/update-in-campaign {campaignId, revision, mode: apply|keep}` calcula un diff (PMB-6.5/5.5) entre la revisión fijada y la nueva del mundo; `apply` crea un override con el nuevo diff o actualiza la revisión base, `keep` conserva la actual. Nunca se aplica en silencio (misma regla que versionado/importación).
- **Aislamiento y permisos:** el servidor valida que la campaña pertenezca al mundo, que el usuario sea miembro con rol suficiente y que ninguna mutación de una campaña modifique el item global del mundo ni el contenido de otra campaña. Los secretos del DM siguen aplicándose (ADR-031 §6).
- **Sin ejecución de código ni duplicación de reglas:** overrides son datos JSON validados por el esquema del tipo; el motor no interpreta scripts (ADR-036/040).

**Alternativas consideradas:** copia total (ADR-032-A: aislamiento pero sin propagación de correcciones — descartada); referencias puras (ADR-032-B: una edición del mundo altera todas las campañas — descartada por el requisito); tabla de items por campaña (duplicación masiva — descartada).  
**Consecuencias:** Fase 7 añade migraciones aditivas y un par de endpoints nuevos; la UI necesita un gestor de actualizaciones (diff + conservar/aplicar) y un editor de overrides; PMB-7.6 debe probar dos campañas compartiendo mundo sin contaminación. El creador de personajes no cambia: consume el contenido efectivo de la campaña (ADR-034).  
**Pendiente:** aprobación del propietario; después PMB-7.2 (implementación), 7.3 (lore), 7.4 (import/export `.pvttpkg` y SRD 5.2), 7.5 (herramientas de mapa) y 7.6 (aislamiento).

## ADR-043 — Anotaciones de escena compartidas (EXP-31)

**Estado:** Aceptada (2026-11-25; el propietario autorizó ejecutar el plan completo de deuda pendiente, incluida esta decisión de contrato, con «dale con todo» tras ver el bloqueo).
**Contexto:** `MapToolsView` calcula medición, dibujo y pines solo en el cliente (`MapToolsView.tsx:5-9`); el pendiente `POST_MVP_BACKLOG.md:146` y la salvedad §13 nº 3 (`POST_MVP_ACCEPTANCE.md:19`) exigen persistencia compartida con `commandId`. `PMB-7.6` se cerró sin esta parte.

**Decisión:**
- Comando `SetSceneAnnotation` (`commandId`, `sceneId`, `annotationId`, `expectedVersion`, `kind: measure|draw|note`, `points[]`, `text?`, `clear?`) y evento `SceneAnnotationsUpdated` con el estado completo de la escena, con el mismo patrón que `MoveToken`/`TokenMoved` y `FogUpdated`: idempotencia en `processed_commands`, validación en servidor y persistencia **antes** de emitir.
- Migración aditiva `AddSceneAnnotations` (tabla por escena con versión, autor, puntos, texto y timestamps). Los miembros leen; **solo GM/Owner escriben** (el servidor valida, `RequireRole`).
- La **medición** sigue siendo ayuda local (no tiene valor compartido); se sincronizan dibujo y anotaciones con texto.
- Contratos v1 en `packages/contracts/events/`, anunciados en su README antes de tocar clientes (AGENTS §6).

**Alternativas consideradas:** usar notas del GM (privadas, por campaña y sin evento en vivo: descartado); blob JSON en `scenes` (sin versionado por anotación ni idempotencia: descartado); compartir también la medición (ruido innecesario en el tablero: descartado).

**Consecuencias:** nuevos contratos HTTP/tiempo real y una tabla; la web necesita store de anotaciones con replay y dos contextos E2E. Sin duplicar reglas en web/escritorio: el servidor sigue siendo autoridad.

## ADR-044 — Operador de contenido para mutaciones globales (P1.2)

**Estado:** Aceptada (2026-11-25; el propietario autorizó en esta sesión el paquete de deuda P1.2/P1.3 con «Autorizado»). La implementación corre a cargo de otro agente en `backend/Api/Identity/`, `backend/Api/Content/` y `backend/Api/Program.cs`; no forma parte de este cambio.

**Contexto:** `POST /api/v1/content/types` y `POST /api/v1/content/packs/import` mutan el catálogo global compartido por todas las campañas. El simple hecho de tener sesión (o un rol de campaña) no basta para modificar metadatos que otros usuarios consumen. No hay sistema de roles globales y el MVP no incorpora uno nuevo.

**Decisión:**
- La autorización se rige por una **allowlist de nombres de usuario configurada** (`Content:OperatorUsers`, lista separada por comas, case-insensitive), sin roles globales ni base de datos adicional.
- El handler de sesión añade el claim `content-operator` solo a esos usuarios; la política `ContentOperator` (`RequireClaim`) protege las mutaciones globales del catálogo en el servidor. El claim se deriva de la sesión validada en cada petición, no de un valor enviado por el cliente.
- **Política por defecto denegada:** si la allowlist está vacía o no contiene al usuario, no hay claim y la petición se rechaza (403). No se añaden cuentas por defecto ni secretos al repositorio.
- El resto de operaciones de contenido (ítems personales, propuestas, grants de campaña) mantienen sus reglas actuales: el permiso se sigue validando en el servidor por recurso.

**Alternativas consideradas:** confiar en rol de GM/Owner de campaña (no representa autoridad sobre el catálogo global); guardar un flag `IsAdmin` en la tabla de usuarios (nueva migración y roles globales para un MVP); delegar la decisión en el cliente (inseguro, AGENTS §3).

**Consecuencias y riesgos:** el despliegue debe configurar la allowlist; sin ella las dos mutaciones globales quedan inaccesibles por diseño. Cambiar la lista exige reiniciar/releer configuración. Como el registro de usuarios es libre, las cuentas operador deben **pre-registrarse** antes de poner sus nombres en la allowlist (si no, otro usuario podría reclamarlos primero); el claim se deriva de la sesión validada, nunca de un valor enviado por el cliente. La web y el escritorio no necesitan conocer el claim: el servidor devuelve 403 y los clientes muestran el error.

## ADR-045 — Evento `SceneDeleted` (EXP-37)

**Estado:** Aceptada (2026-11-25; aprobación explícita del propietario en esta sesión con «Autorizado»). Sustituye la sobrecarga semántica de `MapUpdated { map: null }` como señal de borrado de escena.

**Contexto:** `DELETE /api/v1/scenes/{sceneId}` persistía el borrado y emitía `MapUpdated` con `Map=null` para avisar a los clientes (ADR-028). Ese evento significa «el mapa ya no está», no «la escena desapareció», y obligaba a los consumidores a interpretar un `null` como borrado de entidad. No había contrato propio para la eliminación.

**Decisión:**
- Nuevo evento `SceneDeleted` v1 con el envelope estándar de `ARCHITECTURE.md` §7 — `eventId`, `campaignId`, `actorUserId`, `occurredAt`, `schemaVersion` (constante 1) y `sceneId` — **sin `aggregateVersion`**, igual que `FogUpdated` y `SceneAnnotationsUpdated`. Esquema en `packages/contracts/events/scene-deleted.event.v1.schema.json` (`$id` `projectvtt/events/scene-deleted.v1`).
- `SceneService.DeleteSceneAsync` persiste (incluidos los blobs y el historial) y **después** emite al grupo de campaña tanto el `MapUpdated(null)` existente **por compatibilidad con clientes antiguos** como el nuevo `SceneDeleted`. No se retira el evento antiguo en esta versión.
- El cliente web consume `SceneDeleted`: `SceneView` vuelve al espacio de campaña si era la escena abierta; `CampaignSpaceView` recarga la lista de escenas. La HTTP sigue siendo la fuente de verdad para la carga inicial, pero tras una reconexión del hub la lista no se recarga automáticamente (limitación preexistente de `CampaignRealtime`, anotada en el código): un borrado mientras el cliente estaba offline puede exigir recargar la página.
- Contrato y nombres de evento anunciados en `packages/contracts/README.md` antes de tocar los clientes (AGENTS §6). No cambia el contrato HTTP, por lo que no se regenera OpenAPI.

**Alternativas consideradas:** seguir con `MapUpdated(null)` (semántica confusa y acoplamiento entre borrado de mapa y de escena: descartado); dejar de emitir `MapUpdated` en el borrado (rompería clientes antiguos: descartado); reutilizar `SceneAnnotationsUpdated` con estado vacío (no describe la eliminación: descartado).

**Consecuencias y riesgos:** los clientes que ignoran `SceneDeleted` siguen funcionando gracias a `MapUpdated(null)`; los nuevos deben evitar tratar ese `null` como borrado de escena. El cliente de escritorio no consume `SceneDeleted` todavía (fuera de alcance): mantiene su comportamiento actual. Durante la transición se emiten dos eventos por borrado; es aditivo y no altera el estado persistido.

## ADR-046 — Alcance del cliente de escritorio (EXP-41)

**Estado:** Sustituida por ADR-048 (2026-11-26). Fue Aceptada el 2026-11-25 con «Autorizo todo» y sirvió para fijar el vertical slice; el propietario autorizó después avanzar por rebanadas, con lo que el congelamiento ya no está vigente.

**Contexto:** ADR-030 define el cliente Godot como vertical slice (login, campañas, escenas, mapa, tokens y movimiento en vivo), pero la revisión conjunta de 2026-11-25 lo señala como riesgo nº 1 de divergencia respecto a la web: «paridad pendiente» no dice qué se hará ni en qué orden. El escritorio ya consume los contratos HTTP/eventos compartidos y no debe convertirse en una segunda implementación de reglas de negocio.

**Decisión:**
- Se **congela como vertical slice** en este ciclo: login, campañas, escenas, mapa, tokens y movimiento en vivo. **No se persigue paridad total** con la web.
- Si se amplía, será por **rebanadas explícitas** y en este orden: (1) identidad visual «Tinta y ámbar» y portadas/retratos reutilizando los mismos contratos HTTP; (2) biblioteca de contenido de solo lectura; (3) inspector de escena y anotaciones; (4) resto.
- Cada rebanada exige smoke de Godot (`godot --headless -- --smoke`) y **no duplicar reglas de negocio**: el escritorio consume los DTOs y eventos compartidos (`packages/contracts/`), nunca una segunda implementación.

**Alternativas consideradas:** perseguir paridad total en este ciclo (descartado: coste y riesgo de divergencia); ampliar funciones sin orden ni alcance acordado (descartado: repetiría la ambigüedad actual); retirar el cliente de escritorio (descartado: el vertical slice ya funciona y ADR-003 lo mantiene como cliente sobre los mismos contratos).

**Consecuencias y riesgos:** el escritorio no incorpora en este ciclo funcionalidad nueva de la web (incluido `SceneDeleted`, ADR-045) y cualquier avance necesita aprobar una rebanada. Riesgo documentado: los DTOs C# del escritorio, escritos a mano, pueden divergir de los esquemas compartidos; mitigación = `tests/Unit/ContractParityTests.cs` (EXP-42: comprueba que los 16 esquemas tienen tipo C# equivalente con las propiedades `required`), paridad TypeScript/Godot aún manual, y regenerar OpenAPI cuando cambie el contrato HTTP. El smoke es headless, por lo que la interacción visual sigue sin automatizar (ADR-030).

## ADR-047 — Licencia del código y nombre comercial (EXP-45)

**Estado:** Aceptada (2026-11-26; el propietario eligió «MIT · ProjectVTT» al autorizarse el cierre para publicación).
**Contexto:** `README.md` mantenía como pendientes el nombre comercial, la identidad visual y la licencia. La identidad «Tinta y ámbar» ya estaba aprobada e integrada; faltaban nombre y licencia para poder publicar.

**Decisión:**
- El producto se llama **ProjectVTT** (se conserva el nombre actual, sin renombrar rutas ni marcas).
- El **código** se publica bajo **MIT** (`LICENSE` en la raíz). Es una decisión permissiva y irreversible en la práctica: no se cambiará sin acuerdo explícito del propietario.
- Los recursos no son MIT: arte CC0/CC BY con atribución en `art/LICENSES.md`, `icons/LICENSES.md` y Créditos; SRD bajo CC BY 4.0 en los manifiestos del seed. `LICENSE` lo declara expresamente para evitar ambigüedad.

**Alternativas consideradas:** AGPL-3.0 (copyleft de red, descartada por elección del propietario); licencia privativa (descartada); renombrar el producto (descartado: coste de marca sin beneficio funcional).

**Consecuencias y riesgos:** MIT permite redistribución cerrada de derivados; la atribución de assets y del SRD sigue siendo obligatoria según sus propias licencias y se expone en *Créditos* y `/api/v1/credits/export`.

## ADR-048 — Rebanadas de paridad del escritorio (EXP-45)

**Estado:** Aceptada (2026-11-26; el propietario autorizó reabrir ADR-046 y avanzar por rebanadas). **Sustituye el congelamiento** de ADR-046, que pasa a «Sustituida».
**Contexto:** ADR-046 fijó el escritorio como vertical slice para evitar divergencias. El propietario pidió completar lo que faltaba, incluida la paridad.

**Decisión:** se mantiene el vertical slice como base funcional y se amplía **por rebanadas ordenadas**, sin duplicar reglas de negocio:
1. identidad visual «Tinta y ámbar» y portadas de campaña (cerrada en EXP-45; el retrato queda para la rebanada 3, al no existir aún superficie de ficha en el escritorio);
2. biblioteca de contenido de solo lectura;
3. inspector de escena y anotaciones compartidas (ADR-043);
4. resto de superficies, previa aprobación.

Cada rebanada conserva `godot --headless -- --smoke` y consume los mismos DTOs y eventos (`packages/contracts/`). No se persigue una copia de la web en un solo paso.

**Alternativas consideradas:** paridad total inmediata (descartada: semana+ y riesgo de divergencia); mantener el congelamiento (descartado por decisión del propietario).

**Consecuencias y riesgos:** los DTOs C# del escritorio siguen escritos a mano; `ContractParityTests` cubre los esquemas contra tipos de la API, no contra los tipos Godot. Cada rebanada debe ampliar esa comprobación o documentar la excepción.

## ADR-049 — Audio opcional con controles explícitos (EXP-45)

**Estado:** Aceptada (2026-11-26; propietario autorizó «Audio»).
**Contexto:** `AUDIO_EVALUATION.md` difería el audio porque no había infraestructura ni pantalla de ajustes. El cierre para publicación incluye ahora esa superficie.

**Decisión:** los efectos sonoros se **generan con WebAudio en el cliente** (tonos breves para tiradas, eventos y confirmaciones), sin descargar bundles ni añadir assets atribuibles. Se activan solo tras gesto del usuario, con una pantalla de *Ajustes* que expone activación, volumen y mute. La información sigue siendo visual: el audio nunca es la única vía para un evento (accesibilidad AA). Si se incorporan samples en el futuro, se registrarán fuente/licencia/URL según ADR-036.

**Alternativas consideradas:** bundles Sonniss/Kenney/freesound (descartados por volumen y obligaciones de atribución innecesarias para tonos simples); audio automático al cargar (descartado: requiere gesto y sería intrusivo).

**Consecuencias y riesgos:** WebAudio puede ser bloqueado por el navegador hasta el primer gesto; la UI debe degradarse silenciosamente a «sin sonido» sin afectar al flujo. `AUDIO_EVALUATION.md` queda como histórico de la decisión anterior, no como estado vigente.

## Cómo registrar una decisión

Para cada nueva ADR:
- ID y título.
- Estado: Propuesta / Aceptada / Rechazada / Sustituida.
- Contexto.
- Decisión.
- Alternativas consideradas.
- Consecuencias y riesgos.
- Fecha y responsable, cuando se conozcan.
