# ProjectVTT

Mesa virtual para campañas de rol con cliente web, cliente de escritorio y servidor autoritativo. **Código publicado bajo licencia MIT** (ADR-047).

**Enlaces:** [Caso de estudio y capturas](https://ethanmorenodev.github.io/portfolio/#projectvtt) · [Documentación de producto y arquitectura](https://github.com/ethanmorenodev/projectvtt-docs)

Este repositorio está pensado para que una persona y agentes de IA puedan trabajar de forma coordinada, con decisiones explícitas y tareas verificables.

## Estado

**MVP funcionalmente completo (2026-11-09)** según `PROJECT_SPEC.md` §2: P0 16/16 y P1 8/8.

- **Fase 0** (2026-11-03): monorepo, convenciones, CI, compose de desarrollo y decisiones ADR-001..008.
- **Épicas B–D**: API autoritativa, identidad, campañas/invitaciones/roles, escenas, tiempo real (`MoveToken`/`TokenMoved`), cliente web con mapa (zoom/pan), tokens y grid cuadrado/hexagonal (ADR-023).
- **Épica E**: actores D&D 5e con atributos/recursos, inventario, efectos/condiciones y automatización del Ruleset (ventaja/desventaja 2d20, modificadores y catálogo de efectos **confirmado**, ADR-015).
- **Épica F**: chat, mensajes privados 1:1, iniciativa/turnos, notas del GM, niebla de guerra e historial (ADR-016..021).
- **Épica G**: cliente de escritorio Godot 4.7.2 .NET con el vertical slice completo (login, campañas, escenas, mapa, tokens y movimiento en vivo; ADR-030).
- **Épica H**: logs estructurados, secretos fuera del repo, límites de subida, pruebas E2E con dos usuarios reales, monitorización (`/health`, `/health/ready`, `/metrics`), pruebas de rendimiento, mapas persistidos (ADR-028), backups/restauración (ADR-029) y **despliegue self-hosted documentado** con Docker Compose (`infra/docker/compose.selfhosted.yml` + `docs/DEPLOY.md`; compose ejecutado y verificado el 2026-11-25).
- **Verificación (matriz PMB-8.1 + EXP-22..32):** matriz integral en verde tras el cierre de Fase 8 — backend 61 unitarias + 293 integración + 14 E2E (Playwright) + 5 de rendimiento, web 262 (Vitest); `dotnet build backend/ProjectVTT.slnx` y `npm run build` sin errores; sin drift de migraciones (`dotnet ef migrations has-pending-model-changes`); `dotnet build apps/desktop/ProjectVTT.Desktop.csproj` sin errores y smoke end-to-end del escritorio `SMOKE OK` (exit 0); `git diff --check` limpio.

**Estado de publicación:** nombre **ProjectVTT** y código bajo licencia **MIT** (`LICENSE`, ADR-047); los assets y el SRD conservan sus licencias CC registradas en Créditos. La identidad visual «Tinta y ámbar» está aprobada e integrada. El compose self-hosted está ejecutado y verificado (`docs/DEPLOY.md` §5); el job `deploy-smoke` de CI queda a la espera de su primera ejecución en GitHub Actions.

**Pendiente declarado:** paridad del escritorio por rebanadas (ADR-048; rebanadas 1-3 cerradas: tema, biblioteca de solo lectura e inspector/anotaciones), proveedor cloud y object storage S3/MinIO (ADR-007; self-hosted con PostgreSQL 17 ya verificado). El tour guiado y el audio opcional ya están incluidos (EXP-45). Para importar paquetes o crear tipos globales hay que configurar `Content__OperatorUsers` con cuentas pre-registradas (ADR-044).

**Post-MVP (en curso):** plan del propietario consolidado en `docs/POST_MVP_PLAN.md` (v2), con arquitectura en `docs/architecture/`, sistema visual en `docs/visual/` y backlog en `docs/POST_MVP_BACKLOG.md`. ADR-031..042 aceptadas; Fases 4 (Biblioteca), 5 (Motor de reglas), 6 (Inventario y equipamiento) y 7 (Mundo y herramientas avanzadas) cerradas con PMB-4.1..4.10, PMB-5.1..5.7, PMB-6.1..6.5 y PMB-7.1..7.6 (ADR-042); **Fase 8 (Calidad y lanzamiento) cerrada** con PMB-8.1..8.8 (matriz integral, rendimiento, seguridad, accesibilidad AA, consistencia visual, documentación, criterios §13 verificados y audio evaluado/diferido). **El estado exacto y el «Siguiente» viven en `docs/POST_MVP_BACKLOG.md`** (fuente de verdad); los cambios en contratos compartidos se anuncian y aprueban antes de integrar.

> Importante: esta documentación define una propuesta inicial. Las decisiones marcadas como **PENDIENTE** deben confirmarse antes de tratarlas como definitivas.

## Inicio rápido

Prerrequisitos: .NET SDK 10, Node.js 26+ y (opcional) Docker para PostgreSQL y Godot 4.7.2 .NET para el escritorio.

```bash
# Backend: build + pruebas (integración usan SQLite; las E2E arrancan
# API + web + Chromium con Playwright, la primera vez descarga el navegador)
dotnet build backend/ProjectVTT.slnx
dotnet test backend/ProjectVTT.slnx

# Web: build + pruebas
cd apps/web
npm ci
npm run build
npm test
cd ../..

# Escritorio: compilar C# (ejecutar requiere Godot 4.7.2 .NET)
dotnet build apps/desktop/ProjectVTT.Desktop.csproj
# Godot 4.7.2 .NET está instalado vía WinGet; acceso directo en el
# escritorio ("ProjectVTT Escritorio (Godot)") para abrir apps/desktop.
# Prueba de humo end-to-end del vertical slice (backend en marcha):
#   <Godot> --headless --path apps/desktop -- --smoke

# Base de datos de desarrollo (requiere Docker): define tu contraseña
# FUERA del repositorio (ADR-024; .env está en gitignore)
cp infra/docker/.env.example infra/docker/.env   # y edita POSTGRES_PASSWORD
docker compose -f infra/docker/compose.dev.yml up -d

# Migraciones (herramienta local) y arranque de la API: la cadena de
# conexión viene de user-secrets (desarrollo) o ConnectionStrings__Default
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=projectvtt;Username=projectvtt;Password=<tu-password>" --project backend/Api
dotnet ef database update --project backend/Api --startup-project backend/Api
dotnet run --project backend/Api
```

La API queda en `http://localhost:5000` (puerto por defecto de `dotnet run`), con `/health` (liveness), `/health/ready` (readiness con base de datos), `/metrics` (Prometheus, ADR-026), `/openapi/v1.json` y el contrato bajo `/api/v1`:

- `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me` (Bearer).
- `POST|GET /api/v1/campaigns`, `GET|PATCH /api/v1/campaigns/{id}`, `POST /api/v1/campaigns/{id}/archive`.
- `GET|PATCH|DELETE /api/v1/campaigns/{id}/members[/{userId}]`.
- `GET|POST /api/v1/campaigns/{id}/invites`, `DELETE /api/v1/campaigns/{id}/invites/{inviteId}`, `POST /api/v1/invites/{code}/redeem`.
- `POST|GET /api/v1/campaigns/{id}/scenes`, `GET|PATCH /api/v1/scenes/{id}` (PATCH: `gridCellSize` y `gridShape`, GM+), `POST|GET /api/v1/scenes/{id}/tokens`; `GET|PUT|DELETE /api/v1/scenes/{id}/map` (mapa persistido y compartido: lectura para miembros, subida/borrado GM+, ADR-028).
- `POST|GET /api/v1/campaigns/{id}/actors`, `GET|PATCH /api/v1/actors/{id}`; `GET|POST /api/v1/actors/{id}/items`, `PATCH|DELETE /api/v1/actors/{id}/items/{itemId}`; `GET|POST /api/v1/actors/{id}/effects`, `DELETE /api/v1/actors/{id}/effects/{effectId}`; `GET /api/v1/rulesets`; `POST|GET /api/v1/campaigns/{id}/rolls`; `GET|POST /api/v1/campaigns/{id}/messages`; `GET|POST /api/v1/campaigns/{id}/direct-messages` (la conversación se lista con `GET .../direct-messages/{otherUserId}`); `GET|POST /api/v1/scenes/{id}/combat` con `start`, `end`, `advance` y CRUD de `combatants`; `GET|POST /api/v1/scenes/{id}/fog` con `reveal` y `hide`; `GET|POST /api/v1/campaigns/{id}/notes` y `PATCH|DELETE /api/v1/notes/{id}` (notas del GM, solo GM+, sin evento); `GET /api/v1/campaigns/{id}/history` (historial de cambios, solo lectura). `PATCH /api/v1/scenes/{id}` configura el grid (`gridCellSize` y forma `square`/`hex`, ADR-023).
- Tiempo real (SignalR, Bearer o `?access_token=`): hub `/hubs/campaign` con `JoinCampaign`, `LeaveCampaign`, `SyncScene` y `MoveToken`; eventos `TokenMoved`, `TokenCreated`, `ActorCreated`, `ActorUpdated`, `InventoryUpdated`, `EffectsUpdated`, `RollResolved`, `MessagePosted`, `DirectMessagePosted` (este solo a los dos participantes), `CombatUpdated`, `FogUpdated`, `MapUpdated` y `SceneAnnotationsUpdated` (ADR-043; contratos en `packages/contracts/events/`). `GET|PUT /api/v1/scenes/{id}/annotations` persiste y comparte dibujo/anotaciones por escena (lectura para miembros, escritura GM+, idempotente por `commandId`).
- **Post-MVP (Fases 4–7):** biblioteca de contenido (`/api/v1/content/types`, `/content/items`, `/content/items/{id}/revisions`, `/content/proposals`, `/content/items/{id}/grants`), imágenes y créditos (`/api/v1/images`, `/images/{id}/file`, `/images/{id}/replace`, `/credits/export`), mundos reutilizables (`/api/v1/worlds`, `/worlds/{id}/campaigns/link`, `/worlds/{id}/use|override|independentize`), lore (`/content/lore/graph`, `/content/lore/timeline`), reglas homebrew (`/content/abilities`, `/content/items/{id}/choice-groups`, `/rules/rolls`) y paquetes `.pvttpkg` (`/content/packs`, `/content/packs/{key}/export`, `/content/packs/import`). El servidor sigue siendo autoridad: permisos, secretos del DM y resolución de efectos se validan siempre en backend.

Cliente web (`apps/web`): con el backend en marcha, `npm run dev` sirve la app en `http://localhost:5173` y hace proxy de `/api` y `/hubs` hacia `http://localhost:5000` (fijo en `vite.config.ts`, configurable con `VITE_API_TARGET`). Para tenerla a mano, `scripts/ProjectVTT-inicio.*` arranca backend + web y abre la aplicación (accesos en el escritorio: "ProjectVTT (iniciar)" y "ProjectVTT Web"). `scripts/ProjectVTT-acceso-directo.ps1` (re)crea el acceso "ProjectVTT (iniciar)" con el logo de la app (`apps/web/public/favicon.ico`) y la opción **Ejecutar como administrador** activada; el acceso web usa el mismo icono. Tras cambiar la ruta del proyecto, vuelve a ejecutarlo. La iconografía de las tarjetas de Biblioteca son SVG de **game-icons.net (CC BY 3.0)** en `apps/web/public/icons/`, con atribución en *Créditos* y en `/api/v1/credits/export` (ficha `docs/visual/screens/pmb-8.6b-iconografia.md`).

Cliente de escritorio (`apps/desktop`, Godot 4.7.2 .NET): vertical slice con login, campañas, escenas, mapa, tokens y movimiento en vivo sobre los mismos contratos que la web (ADR-006/ADR-030). Se abre con el acceso "ProjectVTT Escritorio (Godot)" o `scripts/ProjectVTT-escritorio.bat`; su verificación automática es el smoke headless (`-- --smoke`) que recorre el flujo real contra el backend. Si cambia el esquema, el launcher (`ProjectVTT-inicio.ps1`) activa `PROJECTVTT_RESET_DEMO_DB_ON_MISMATCH=1`: el backend detecta columnas/tablas faltantes con SQLite (`SqliteSchemaGuard`), guarda la BD demo como `%TEMP%\projectvtt-demo.db.bak-<fecha>` y la regenera con el esquema actual antes de sembrar. Sin ese permiso (despliegues reales) falla con un mensaje accionable, nunca destruye datos. El launcher espera a `/health` (no solo al puerto) y no abre el navegador si la API murió, evitando el 502.

Backups: `scripts/ProjectVTT-backup.ps1` crea una copia (base + mapas) y `scripts/ProjectVTT-restore.ps1 <backupId>` la restaura con el servidor parado (núcleo y CLI en `tools/ProjectVTT.Tools`, ADR-029).

Convenciones, versiones y flujos de trabajo: [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md).

## Guía de primeros pasos (tutorial inicial)

Recorrido mínimo de extremo a extremo, con el backend ya arrancado (`ProjectVTT (iniciar)` o `scripts/ProjectVTT-inicio.ps1`; ver [Inicio rápido](#inicio-rápido)):

1. **Entrar.** Abre `http://localhost:5173/`, pulsa *Regístrate* y crea tu usuario. La primera cuenta de una campaña es su **Owner** (equivalente a DM/admin de esa campaña).
2. **Crear una campaña.** Desde *Inicio* → *Crear campaña*, o en *Campañas*. El raíl izquierdo muestra el logo y seis destinos globales: Inicio, Campañas, Biblioteca, Biblioteca de imágenes, Herramientas y Gestión. *Espacio de campaña* y *Mesa de juego* ya no están en el raíl: se acceden desde cada campaña.
3. **Preparar la mesa.** En el espacio de campaña → *Escenas y mapas* → crea una escena. Entra en *Mesa de juego*: rejilla, zoom con rueda, arrastrar fondo y botón *Añadir token* (vincula un actor o crea uno nuevo). Puedes subir un mapa desde *Importar mapa* (PNG/JPEG/WebP/SVG, ≤10 MiB, validado en servidor).
4. **Crear un personaje.** En el espacio de campaña → *Personajes* → *Crear personaje*. El asistente usa contenido publicado del SRD 5.1; cada elección muestra sus efectos antes de guardar. Puedes *Guardar y salir* para continuar después.
5. **Contenido y mundo.** En *Biblioteca* explora razas/clases/trasfondos del paquete SRD y crea contenido propio con el editor universal; en *Biblioteca de imágenes* sube recursos con licencia/atribución (visible en *Créditos*). En *Historia y lore* del espacio de campaña se abre el grafo de facciones, localizaciones y cronología; los secretos del DM solo se muestran a GM/Owner.
6. **Jugar.** En la mesa abre las herramientas desde el raíl: *Ficha*, *Combate*, *Chat*, *Character Hub* y *Herramientas de mapa* (medir en cuadrados de 5 ft, dibujar, anotar, capas). Invita a otra persona desde *Participantes y permisos* → *Invitación* y comprueba el movimiento en vivo en dos navegadores.

> Los secretos del DM, los permisos y las reglas se resuelven siempre en el servidor: la interfaz solo muestra lo que el backend autoriza. No hay «Admin global»: el rol Owner/GM se concede por campaña.

## Documentos

Verificación uno a uno de los criterios post-MVP: [`docs/POST_MVP_ACCEPTANCE.md`](docs/POST_MVP_ACCEPTANCE.md). Índice canónico con estado, tamaño y cuándo leer cada documento: [`docs/INDEX.md`](docs/INDEX.md) (incluye `PROJECT_SPEC.md`, `AGENTS.md`, `ARCHITECTURE.md`, roadmaps/backlogs, ADR, despliegue, plan/backlog/auditoría post-MVP, `docs/visual/` y `docs/architecture/`).

## Principios

1. Priorizar un producto pequeño que funcione de extremo a extremo.
2. El servidor es la autoridad sobre el estado compartido de la partida.
3. Los clientes web y escritorio comparten contratos y reglas de negocio.
4. No introducir complejidad distribuida antes de necesitarla.
5. Ninguna tarea se considera terminada solo porque compile: debe cumplir sus criterios y tener pruebas apropiadas.
6. No inventar decisiones del propietario del proyecto. Preguntar o registrar como pendiente.
