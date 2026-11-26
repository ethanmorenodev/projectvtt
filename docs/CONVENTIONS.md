# Convenciones del proyecto

Complementa a [`AGENTS.md`](../AGENTS.md) (reglas de trabajo) y [`ARCHITECTURE.md`](../ARCHITECTURE.md) (límites entre componentes).

## Estructura del monorepo

```text
ProjectVTT/
├── apps/
│   ├── web/          # Cliente web (React + TypeScript + PixiJS)
│   └── desktop/      # Cliente de escritorio (Godot 4 + C#)
├── backend/          # Solución .NET: API y módulos de dominio
│   └── ProjectVTT.slnx
├── packages/
│   └── contracts/    # Contratos compartidos (OpenAPI + esquemas de eventos)
├── tests/
│   └── Integration/  # Pruebas de integración (xUnit)
├── infra/docker/     # Compose de desarrollo
├── docs/             # Documentación de trabajo
└── .github/workflows # CI
```

No crear directorios vacíos por anticipación: `tests/Unit`, `tests/E2E`, `packages/sdk-*`, etc. se crean cuando haya algo real dentro.

## Versiones verificadas (Fase 0, 2026-11-03)

| Componente | Versión |
|---|---|
| .NET SDK | 10.0.400 |
| Backend (TFM) | `net10.0` |
| Node.js | 26.x |
| React | 19.3 |
| Vite | 8.x |
| TypeScript | 7.x |
| Vitest | 5.x |
| Godot + `Godot.NET.Sdk` | 4.7.2 (.NET) |
| PostgreSQL (compose dev) | 17-alpine |

Cambiar una versión de esta tabla exige actualizar también CI, `compose.dev.yml` y documentar el cambio (ver ADR-008).

### Paquetes del backend (Épica B, 2026-11-03)

| Paquete | Versión | Uso |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Proveedor PostgreSQL |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.14 | Pruebas de integración (sin Docker) |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.14 | Herramientas de migración (`PrivateAssets=all`) |
| `Microsoft.AspNetCore.OpenApi` | 10.0.14 | Documentación OpenAPI |
| `Microsoft.AspNetCore.SignalR.Client` (solo pruebas) | 10.0.14 | Cliente de las pruebas de tiempo real |
| `dotnet-ef` (herramienta local, `dotnet-tools.json`) | 10.0.14 | Comandos de migración |

`Microsoft.Extensions.Identity.Core` no se referencia como paquete: ya está en el framework compartido de ASP.NET Core (evita el warning NU1510).

## Base de datos y migraciones

- Proveedor por defecto: `postgres` (`Database:Provider`); `sqlite` se usa solo en pruebas de integración.
- Migraciones **explícitas** con la herramienta local; no hay auto-migrate al arrancar (ver ADR-009):

```bash
dotnet tool restore                                              # instala dotnet-ef
dotnet ef migrations add <Nombre> --project backend/Api --startup-project backend/Api --output-dir Data/Migrations
dotnet ef database update --project backend/Api --startup-project backend/Api   # requiere PostgreSQL en marcha
dotnet ef migrations has-pending-model-changes --project backend/Api --startup-project backend/Api  # drift (lo ejecuta CI)
dotnet ef migrations script --idempotent --project backend/Api --startup-project backend/Api        # SQL sin servidor
```

- El modelo EF (`backend/Api/Data/AppDbContext.cs`) debe ser portable entre PostgreSQL y SQLite: sin tipos propios de un proveedor.
- Las pruebas de integración crean el esquema con `EnsureCreated` sobre SQLite en un archivo temporal; no ejecutan migraciones.

## Cómo ejecutar y verificar

Referencia canónica: [`README.md`](../README.md) § *Inicio rápido* (build y pruebas de backend/web/escritorio, migraciones y arranque de la API). Para la base de desarrollo (requiere Docker):

```bash
docker compose -f infra/docker/compose.dev.yml up -d
docker compose -f infra/docker/compose.dev.yml down        # detener
docker compose -f infra/docker/compose.dev.yml down -v     # detener y borrar datos
```

## Git

- Rama principal: `main`.
- Ramas de trabajo: `feature/<cambio>`, `fix/<cambio>`, `test/<cambio>`, `docs/<cambio>`.
- Commits pequeños y revisables, con prefijo convencional: `feat:`, `fix:`, `docs:`, `test:`, `chore:`, `refactor:`.
- No hacer push, merge, cherry-pick ni operaciones destructivas sin autorización del propietario (`AGENTS.md` §6).

## Código

### C# / backend

- `net10.0`, `Nullable=enable`, `ImplicitUsings=enable` en todos los proyectos nuevos.
- Convención .NET: `PascalCase` para tipos, métodos y miembros públicos; `_camelCase` para campos privados.
- La API es la autoridad: toda validación de permisos y reglas vive en el servidor.
- Los módulos de dominio (Identity, Campaigns, Scenes…) se crean dentro de `backend/` siguiendo `ARCHITECTURE.md` §5, en el momento en que se implementen; no se crean proyectos vacíos por adelantado.

### TypeScript / React / web

- `strict: true` en TypeScript; evitar `any` salvo justificación documentada.
- Componentes funcionales con hooks; estado de sesión en `AuthContext` (React Context + localStorage) y estado de la escena en `tokenStore` (funciones puras, versión por token).
- PixiJS v8 (`pixi.js`) se encapsula en `src/pixi/SceneStage.ts`; las vistas no tocan Pixi directamente. Cliente SignalR oficial: `@microsoft/signalr`.
- Pruebas de lógica pura con Vitest junto al código (`src/**/*.test.ts`); el entorno es `jsdom` (sin canvas real: no se prueban renders de Pixi).

### Pruebas

- Unitarias → `tests/Unit/` (se crea cuando haya lógica que testear de forma aislada).
- Integración → `tests/Integration/` (xUnit + `WebApplicationFactory`).
- Pruebas de SignalR → cliente `Microsoft.AspNetCore.SignalR.Client` sobre el TestServer en memoria con transporte LongPolling (el handshake WebSocket requiere un servidor TCP real y queda fuera de las pruebas automatizadas; ver ADR-010).
- E2E → `tests/E2E/` con Playwright (Épica H).
- Una tarea no está terminada hasta ejecutar sus pruebas y poder afirmar el resultado real (`AGENTS.md` §5).

## Contratos

Ver [`packages/contracts/README.md`](../packages/contracts/README.md). Regla principal: los clientes no inventan modelos paralelos; los cambios incompatibles se versionan y coordinan.

## Configuración y secretos

- Nunca commitear `.env`, claves, tokens ni credenciales.
- Las credenciales por defecto del compose son de desarrollo local y no deben usarse en ningún despliegue real.
