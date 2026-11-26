# ProjectVTT Desktop (Godot 4 + C#)

Cliente de escritorio incluido en el MVP (decisión ADR-006 confirmada).
Estado actual: **vertical slice** (ADR-030) ampliado por las rebanadas 1, 2 y
3 de [ADR-048](../DECISIONS.md): identidad visual «Tinta y ámbar» y portadas
de campaña; biblioteca de contenido de solo lectura; y hub de personajes con
retrato y anotaciones de escena compartidas. Funciones: login, campañas,
escenas, mapa, tokens y movimiento en vivo sobre los mismos contratos que la
web. El escritorio solo consume DTOs/eventos: no duplica reglas de negocio
ni validaciones de permisos (las decide el servidor).

## Requisitos

- Godot **4.7.2** con soporte **.NET** (la misma versión que `ProjectVTT.Desktop.csproj` usa en `Godot.NET.Sdk`).
- .NET SDK 8.0 o superior para compilar el proyecto C#.

> Si cambias la versión de Godot, actualiza `Godot.NET.Sdk` en el `.csproj` y la
> línea `config/features` de `project.godot` en el mismo cambio.

## Cómo abrirlo

1. Instala Godot 4.7.2 (.NET) desde https://godotengine.org/download
   (en este equipo ya está vía WinGet; `scripts/ProjectVTT-escritorio.bat`
   o el acceso "ProjectVTT Escritorio (Godot)" abren el proyecto).
2. Abre el proyecto con "Importar" y selecciona `apps/desktop/project.godot`.
3. Godot generará `ProjectVTT.Desktop.sln` la primera vez que abras/guardes el proyecto.
4. Con el backend en marcha (`scripts/ProjectVTT-inicio.ps1`), pulsa F5.
   La URL del servidor es `http://localhost:5000` (variable `PROJECTVTT_API`).

## Estructura

- `Api/` — contratos DTO (gemelos de `apps/web/src/api/types.ts` y
  `Realtime/Contracts.cs`), `ApiClient` (HTTP + Bearer: campañas, escenas,
  mapa, imágenes, contenido, actores y anotaciones) y `SceneConnection`
  (hub SignalR `/hubs/campaign`: `JoinCampaign`, `SyncScene`, `MoveToken` y
  los eventos `TokenMoved`/`TokenCreated`/`MapUpdated`/
  `SceneAnnotationsUpdated`).
- `State/` — `TokenStore` (versión autoritativa por token, misma semántica
  que `apps/web/src/realtime/tokenStore.ts`) y `GridSnap` (snap cuadrado y
  hexagonal flat-top odd-q, ADR-023).
- `Ui/` — vistas programáticas (Login, Campañas, Escenas, Escena con mapa,
  tokens arrastrables y anotaciones, Biblioteca y Personajes), `MainThread`
  (cola de trabajo al hilo principal),
  `DesktopTheme` (tema «Tinta y ámbar», colores de `apps/web/src/theme/tokens.css`,
  sin copiar archivos), `CampaignCard` (portada o inicial) e `ImageBytes`
  (decodificación png/jpeg/webp compartida con el mapa).
- `Main.cs` — navegación y ciclo de vida de la sesión/conexión; aplica el tema
  a la raíz de las vistas.
- `Smoke.cs` — recorrido end-to-end con `--smoke`.

## Verificación

- `dotnet build apps/desktop/ProjectVTT.Desktop.csproj` compila el código C#.
- **Smoke end-to-end** (backend en marcha):

  ```
  <Godot> --headless --path apps/desktop -- --smoke
  ```

  registra un usuario nuevo, crea campaña/escena/token, se conecta al hub,
  sincroniza, mueve un token (`MoveToken`), comprueba el evento
  `TokenMoved` y monta las vistas reales. Sale con código 0 si todo cuadra.
  La rebanada 1 añade además comprobación de portada: primero el 404 (la
  tarjeta usa la inicial) y después un PNG subido con el contrato existente,
  descargado y decodificado.
- Las rebanadas 2 y 3 añaden comprobación de `GET /content/types`,
  `GET /content/items` (con filtros), `GET /campaigns/{id}/actors`,
  `GET /images/{id}/file` (404 → null), creación de una anotación con su
  evento `SceneAnnotationsUpdated` y su posterior lectura por HTTP. Al final
  monta también Biblioteca y Personajes.
- La interacción visual (arrastre de tokens, selección de ítems y retratos)
  se verifica manualmente en ventana; no hay automatización de UI para Godot.

## Rebanada 1: identidad y portadas

- El tema aplica tinta/pergamino/ámbar y esquinas/botones coherentes a
  todas las vistas. Los títulos usan serif del sistema (`SystemFont`:
  Georgia → Times New Roman → serif) y el resto Inter/Segoe UI → sans-serif.
  **No hay archivos `.ttf`/`.otf` en el repositorio**: no se han inventado
  assets; si el sistema no tiene esas familias, Godot usa su fuente por
  defecto (la compilación y el smoke no comprueban la apariencia tipográfica).
- La lista de campañas pide `GET /api/v1/campaigns/{id}/cover` con Bearer,
  mantiene el binario en memoria y lo convierte en `ImageTexture`. Sin
  portada, o si el archivo es SVG (Godot no rasteriza SVG), cae a la inicial
  sobre pergamino ámbar. La lista sigue siendo funcional si falla la carga.
## Rebanada 2: biblioteca de contenido

- Entrada desde la lista de campañas («Biblioteca»). Consume
  `GET /api/v1/content/items` y `GET /api/v1/content/types` con los filtros
  opcionales `system` y `typeKey`; muestra nombre, tipo resuelto, ámbito y
  resumen, y un detalle con descripción, etiquetas, estado/revisión y las
  primeras propiedades. **Solo lectura**: no hay acciones de crear, editar ni
  publicar. Los permisos, grants y la secretaría del DM los aplica el
  servidor antes de responder.

## Rebanada 3: personajes y anotaciones

- Entrada «Personajes» desde las escenas de una campaña. Muestra los actores
  de `GET /api/v1/campaigns/{id}/actors` con retrato de
  `GET /api/v1/images/{id}/file` (`ApiClient.GetImageFileAsync`) e inicial
  como alternativa si no hay retrato, el archivo no se puede decodificar o
  falla la descarga (un retrato roto no impide ver la ficha).
- `SceneView` carga `GET /api/v1/scenes/{id}/annotations` al abrir la escena
  y refresca la lista con el evento `SceneAnnotationsUpdated` del hub
  (soporte real del cliente SignalR de Godot; **no se simula**). La HTTP
  sigue siendo la fuente de verdad para la carga inicial y la reconexión.
- Límites honestos: el smoke comprueba el 404 de imágenes y la ruta de
  decodificación con la portada, pero **no sube un retrato de actor real**;
  el renderizado visual no está automatizado. Tampoco se consume
  `SceneDeleted` (ADR-045): queda fuera de estas rebanadas.
- Excepción documentada de contratos (ADR-048): los DTOs de `Api/Contracts.cs`
  siguen escritos a mano como espejo de los esquemas compartidos;
  `tests/Unit/ContractParityTests.cs` comprueba esos esquemas contra los tipos
  de la API, no contra los tipos de Godot. El smoke verifica los nuevos DTOs
  contra respuestas reales del servidor.

## Dependencias

- `Microsoft.AspNetCore.SignalR.Client` **8.0.x** (fijada): alineada con el
  TFM `net8.0` del juego. La rama 10.x mezcla versiones de ensamblados del
  framework en runtime (ver ADR-030).
