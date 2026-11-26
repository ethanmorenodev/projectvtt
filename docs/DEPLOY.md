# Despliegue self-hosted de ProjectVTT

Instalación del stack completo (PostgreSQL + API + cliente web) con Docker
Compose. Es la forma de alojamiento "propia" del producto (ADR-007,
ADR-004); para desarrollo local sin Docker, ver `README.md` (SQLite).

> **Estado de verificación (2026-11-25): EJECUTADO Y VERIFICADO.** Docker
> Desktop 29.9.3 + Compose v5.7.1 en este entorno. Los tres servicios
> arrancan, las migraciones se aplican en PostgreSQL, y se han comprobado
> `health`/`ready`/`metrics`, registro/login, campaña y escena, creación de
> lectura de anotaciones, subida de mapa e imágenes, borrado de escena y el SPA a través
> de nginx. Durante la ejecución se corrigió un hueco real: nginx no proxía
> `/health` ni `/metrics` y servía el SPA en su lugar (ya arreglado en
> `nginx.selfhosted.conf`). Ver el detalle en la sección 5.

## 1. Requisitos

- Docker con Docker Compose v2.
- Un host con al menos 2 GB de RAM libres.
- Acceso a internet para construir las imágenes (SDK .NET 10, Node 26, nginx).

## 2. Configuración (sin secretos en el repo)

```bash
cp infra/docker/.env.example infra/docker/.env
# Edita .env y define POSTGRES_PASSWORD (obligatorio; ADR-024).
```

Variables (todas con valores por defecto salvo la contraseña):

| Variable | Por defecto | Uso |
|---|---|---|
| `POSTGRES_USER` | `projectvtt` | Usuario de la base de datos |
| `POSTGRES_PASSWORD` | *(obligatoria)* | Contraseña de la base de datos |
| `POSTGRES_DB` | `projectvtt` | Nombre de la base de datos |
| `POSTGRES_PORT` | `5432` | Puerto de PostgreSQL (solo desarrollo) |
| `WEB_PORT` | `80` | Puerto público del cliente web |
| `Content__OperatorUsers` | *(vacía)* | Usuarios permitidos para `POST /content/types` y `POST /content/packs/import` (lista separada por comas, case-insensitive). Vacía = nadie; el seed interno no usa HTTP. **Pre-registra esas cuentas antes de ponerlas en la lista**: el registro es libre y otro usuario podría reclamar el nombre primero (ADR-044). |

La API se configura por variables de entorno (igual que en desarrollo):
`Database__Provider=postgres`, `ConnectionStrings__Default`,
`Assets__RootPath=/data/assets` (mapas, en un volumen) y
`Backups__RootPath=/data/backups`. Si necesitas más ajustes (p. ej.
`Assets:MaxBytes`), añádelos en la sección `environment` del servicio `api`
de `infra/docker/compose.selfhosted.yml`.

## 3. Esquema de la base de datos (migraciones)

Con PostgreSQL **no hay auto-migrate al arrancar** (decisión ADR-009): el
esquema se aplica con migraciones explícitas de EF. Antes del primer
arranque (y tras cada actualización):

- **Opción recomendada — bundle de migraciones** (no requiere SDK en el
  servidor): genera el bundle una sola vez desde cualquier máquina con el
  repositorio y .NET SDK 10:

  ```bash
  dotnet ef migrations bundle --project backend/Api --startup-project backend/Api \
      --configuration Release --self-contained -r linux-x64 -o migrate
  ```

  cópialo al servidor y ejecútalo con la cadena de conexión de producción:

  ```bash
  ConnectionStrings__Default='Host=...;Database=...;Username=...;Password=...' ./migrate
  ```

- **Opción alternativa — desde una máquina con SDK:** `dotnet ef database update
  --project backend/Api --startup-project backend/Api` con
  `Database__Provider=postgres` y la `ConnectionStrings__Default` del servidor.

**Aplicado en la verificación de 2026-11-25:** se generó un script idempotente
y se aplicó dentro del contenedor de PostgreSQL (no hace falta publicar el
puerto 5432 ni instalar el SDK en el servidor):

```bash
dotnet ef migrations script --project backend/Api --startup-project backend/Api \
    --idempotent --output .deploy-migrations.sql
Get-Content .deploy-migrations.sql -Raw |
    docker compose -f infra/docker/compose.selfhosted.yml exec -T postgres \
    psql -U projectvtt -d projectvtt -v ON_ERROR_STOP=1 -f -
```

**Importante:** la API **no arranca** si las migraciones no están aplicadas
(`relation "content_packs" does not exist` y reinicio en bucle). Aplica siempre
el esquema **antes** de iniciar el servicio `api`.

## 4. Arranque

```bash
docker compose -f infra/docker/compose.selfhosted.yml up -d --build
```

Servicios: `postgres` (datos en el volumen `projectvtt-pgdata`), `api`
(mapas en el volumen `projectvtt-assets`) y `web` (nginx con proxy de
`/api`, `/hubs`, `/health` y `/metrics` hacia la API; WebSocket configurado
para el tiempo real). El proxy de monitorización se añadió durante la
verificación: sin él, `curl /health` devolvía el `index.html` del SPA.

## 5. Verificación

```bash
curl http://localhost/health          # {"status":"ok"} (liveness vía proxy)
curl http://localhost/health/ready    # estado de la base de datos
curl http://localhost/metrics         # métricas Prometheus (sin datos sensibles)
```

Este recorrido (más el registro y el seed de SRD) está automatizado en CI desde una BD vacía: job `deploy-smoke` de `.github/workflows/ci.yml`.

Y un recorrido manual: crear cuenta, crear campaña y escena, subir un mapa,
crear un token y moverlo desde dos navegadores. La cobertura post-MVP añade:
crear contenido en la Biblioteca con el editor universal, subir una imagen con
licencia/atribución y verla en *Créditos*, abrir el grafo de lore (secretos del
DM visibles solo para GM/Owner), exportar e importar un `.pvttpkg`, y usar las
herramientas de mapa (medición, anotaciones y capas).

### Resultado real de la verificación (2026-11-25)

| Comprobación | Resultado |
|---|---|
| `up -d --build` (postgres, api, web) | tres contenedores creados; postgres *healthy* |
| Migraciones aplicadas con el script idempotente | API pasó de reinicio en bucle a **healthy** |
| `curl /health` y `/health/ready` vía nginx | `{"status":"ok"}` y `"Healthy"` |
| `curl /metrics` vía nginx | métricas Prometheus (sin secretos) |
| Registro y login vía nginx | 201 / token Bearer |
| Crear campaña y escena | 201 / 201; listado de escenas = 1 |
| Contenido sembrado | **236** ítems visibles de D&D 5e |
| Paquetes | **2** (`dnd5e-srd-5.1`, `dnd5e-srd-5.2`) |
| `GET /scenes/{id}/annotations` | 200, `version = 0` |
| Subir mapa (PNG real) y descargarlo | 200 PUT / 200 GET |
| Subir imagen y descargarla | 201 POST / 200 GET |
| `DELETE /scenes/{id}` y GET posterior | 200 / 404 |
| `GET /` en el puerto 80 | 200, SPA servida por nginx |

**Comprobado también con dos navegadores reales sobre el stack Docker**
(capturas en `docs/visual/screens/docker-deploy/`): dos usuarios (Owner y
Player invitado) entraron en la misma escena, el Player arrastró un token y el
servidor persistió `x=248` (esperado `120+128`). El Owner, **sin recargar**,
seleccionó el token en la nueva posición y el inspector mostró
`Posición: (248, 120) · v1`: el evento `TokenMoved` llegó por el hub a través
del proxy WebSocket de nginx. El recorrido visual cubrió mesa, Campañas,
Biblioteca (236 ítems), Biblioteca de imágenes y Créditos, todos servidos
desde el puerto 80.

## 6. Copias de seguridad

- **Base de datos:** `pg_dump` (la herramienta `tools/ProjectVTT.Tools` es
  solo para SQLite, ADR-029):

  ```bash
  docker compose -f infra/docker/compose.selfhosted.yml exec postgres \
      pg_dump -U projectvtt projectvtt > backup-$(date +%F).sql
  ```

- **Mapas:** el volumen `projectvtt-assets` (copiar con
  `docker run --rm -v projectvtt-assets:/data -v "$PWD:/backup" alpine
  tar czf /backup/assets.tar.gz -C /data .`).
- Restauración: importar el `pg_dump` con `psql` y descomprimir los assets
  en el volumen, con los servicios parados.

## 7. Actualización

1. Copia de seguridad (sección 6).
2. `git pull`.
3. **Aplica las migraciones nuevas antes de arrancar la API** (sección 3): si no,
   el contenedor `api` entra en reinicio en bucle.
4. `docker compose -f infra/docker/compose.selfhosted.yml up -d --build`.
5. Comprueba `/health/ready`.

## 8. Seguridad

- Ninguna credencial vive en el repositorio: todo pasa por `.env`
  (ignorado por git) o por el orquestador (ADR-024).
- La API no se publica al exterior: solo `web` expone el puerto; `/api` y
  `/hubs` llegan a través del proxy de nginx.
- `/metrics` queda accesible desde el mismo origen: si no quieres exponerlo,
  añade una regla de bloqueo en `infra/docker/nginx.selfhosted.conf`.
- Los límites de subida (tipo/tamaño/firma, 10 MiB por defecto) se aplican
  en el servidor; nginx limita el cuerpo a 12 MiB para dejarlos pasar.
