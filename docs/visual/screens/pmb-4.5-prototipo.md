# PMB-4.5 — Biblioteca: buscar, filtrar, etiquetas, duplicar y ver usos

**Fecha:** 2026-11-12 · **Estado:** ✅ **aprobado e integrado 2026-11-13** (prototipo aprobado «Aprobado — construir»; backend + `LibraryView` implementados y en verde).
**Base:** explorador aprobado en PMB-4.1 (`pmb-4.1-prototipo.md`) + editor PMB-4.4. **Ruta dev:** `#prototipo-biblioteca-acciones`.
**Alcance aprobado por el propietario (2026-11-12):** «Completo» — duplicar + ver usos + filtros de etiqueta/estado en la rejilla + orden por «Uso»; *mover* queda cubierto por el editor (sección Organización, PMB-4.4).

## 1. Referencias y principios extraídos

- **Linear** (`REFERENCE_LIBRARY.md` §2): filtros como chips compactos en la misma fila que la búsqueda; estado pulsado visible sin abrir menús.
- **Notion / Airtable** (§4): las etiquetas son el primer filtro después del ámbito; el orden por «uso» (popularidad real) evita que la rejilla sea alfabética para siempre.
- **Unreal Engine** (§3): «ver usos» como panel contextual bajo el inspector en lugar de otra pantalla: el árbol y la rejilla no se pierden.
- `COMPONENT_RULES.md` §9: ConfirmDialog solo para decisiones que bloquean — *Duplicar* confirma porque crea un elemento nuevo en la biblioteca personal (ámbito `user`), no porque sea destructivo.

## 2. Composición (variante `acciones` sobre el explorador aprobado)

El frame de tres zonas de PMB-4.1 **no cambia**; se añade lo siguiente:

- **Toolbar (centro)** — además de búsqueda + ámbito + orden:
  - **Estado:** chips `Todos · Publicado · Borrador` (el estado real de `ContentItemDto.status`).
  - **Etiqueta:** chip `Todas` + una chip por etiqueta existente (derivada de los items; el seed SRD real no tiene etiquetas → en la variante salen las del homebrew de muestra).
  - **Orden:** `Nombre · Recientes · Uso` («Uso» ordena descendente por conteo de usos, desempate por nombre).
- **Inspector (derecha)** — bloque de acciones **vivo**:
  - `Editar` (primaria, abre el editor de PMB-4.4) · `Duplicar` (confirmación) · `Mover` (aviso: vive en el editor, sección Organización) · `Ver usos` (panel inline con `aria-expanded`) · `Publicar` **deshabilitado** (se publica desde el editor).
  - Chips de **etiquetas del item** junto a tipo/ámbito/estado.
  - **Panel «Ver usos»**: filas `{personaje · campaña · rev. · fecha}` — exactamente la forma del contrato aprobado, **sin snapshots**; estado vacío honesto y nota de visibilidad (solo campañas donde el solicitante es miembro).

## 3. Datos honestos y de muestra

- **Reales (seed SRD):** los 18 razas / 12 clases / 13 trasfondos, sus claves, herencia y procedencia; los filtros operan sobre ellos.
- **MUESTRA (etiquetas y usos):** el seed y la demo API tienen `tags = []` y no exponen `usageCount`, así que la variante añade **3 items homebrew ficticios de campaña** (Guardia de Scaldar · Veterana · Pirata de la Escarlata) con etiquetas y conteos de uso ficticios, y **5 filas de uso ficticias** (`USAGE_SAMPLE`). Están etiquetados como muestra en el código y en la propia UI («datos de muestra, PMB-4.5»).
- **Contratos (aprobados e implementados 2026-11-13):** `GET /content/items/{id}/usages` → `{actorName, campaignName, revisionNumber, createdAt}` sin snapshots, visible solo en campañas donde el solicitante es miembro; `usageCount` aditivo en `ContentItemDto`. **Duplicar no usa endpoint nuevo:** en cliente se copia con `POST /content/items` (solo campos visibles → ámbito `user`).
  - ⚠️ **Anuncio de aditivo de contrato (aprobado por el propietario):** los dos cambios anteriores modifican `ContentItemDto` (campo `usageCount`) y añaden un endpoint. Son **aditivos** — no rompen a clientes existentes.

## 4. Capturas (Chromium 1440×900, dev server)

| Estado | Captura |
|---|---|
| Rejilla + filtros + acciones vivas (Guardia de Scaldar seleccionada) | `pmb-4.5-biblioteca-acciones-2026-11-12.jpeg` |
| Panel «Ver usos» con 4 filas de muestra | `pmb-4.5-biblioteca-usos-2026-11-12.jpeg` |
| Diálogo «Duplicar» (copia personal, sin usos ni historial) | `pmb-4.5-biblioteca-duplicar-2026-11-12.jpeg` |
| Filtros aplicados (Borrador · guardia → 1 resultado) | `pmb-4.5-biblioteca-filtros-2026-11-12.jpeg` |
| «Ver usos» vacío (Pirata de la Escarlata, 0 usos) | `pmb-4.5-biblioteca-usos-vacio-2026-11-12.jpeg` |

Proceso: las rutas de prototipo existen solo en DEV (`import.meta.env.DEV`), así que se capturó con Playwright headless (mismo motor que el E2E) contra `http://localhost:5173`, mediante un **test temporal borrado tras capturar** (patrón de PMB-4.4). Verificado visualmente: las 5 capturas muestran los estados descritos.

**Incidencia de entorno:** el dev server llevaba un watcher obsoleto y servía una versión antigua de `App.tsx` (el regex de prototipo no incluía `acciones`); se reinició `npm run dev` y se recapturó. No afecta al código.

## 5. Verificación (fase de prototipo)

- `BibliotecaPrototype.test.tsx`: **10 pruebas** (5 previas de PMB-4.1 + 5 nuevas): filtros de estado/etiqueta con vacío y limpieza; orden por «Uso» con conteos de muestra; duplicar con confirmación y aviso de copia personal; ver usos con 4 filas y estado vacío; Publicar deshabilitado + Mover con aviso.
- Suite web **174/174**, `npx tsc --noEmit` limpio, `npm run build` limpio.
- E2E vuelve a compilar sin errores tras borrar los tests temporales de captura.

## 6. Integración (hecha 2026-11-13)

1. **Backend ✅:** `GET /content/items/{id}/usages` (`ContentEndpoints.cs`) con `ListUsagesForItemAsync` — join `ItemUsages → Actors → CampaignMembers(userId)`, solo campañas donde el solicitante es miembro, sin snapshots; `usageCount` aditivo en `ContentItemDto` (`ContentContracts.cs`) calculado con `UsageCountsAsync` (conteo agrupado en una consulta, sin N+1) al mapear a DTO (5 call sites de `ToDtoAsync` con `viewerUserId`). **3 pruebas nuevas** en `tests/Integration/ContentItemUsagesTests.cs` (lista visible para miembro, vacío para no miembro, conteo en el DTO).
2. **Frontend real ✅:** `LibraryView.tsx` — filtros Estado/Etiqueta en la toolbar, orden «Uso», `availableTags` derivado de los items, inspector con acciones vivas (`Editar · Duplicar · Publicar deshabilitado · Ver usos` con `aria-expanded`), panel `biblio-usos` con estados carga/vacío/error/lista, `ConfirmDialog` de duplicar (POST a `/content/items` copiando solo lo visible, ámbito `user`, sin parentId/campaignId/privateFields) y aviso `role="status"`. `i18n/es.ts` con todas las claves nuevas; `types.ts` con `usageCount` e `ItemUsageSummaryDto`.
3. **Verificación ✅:** web **178/178** (10 en `LibraryView.test.tsx`, 4 nuevas), `tsc --noEmit` 0, `npm run build` limpio; **Unit 53/53 · Integration 232/232 · E2E 2/2**. API demo `:5000` reiniciada con el build nuevo: `GET /api/v1/content/items` devuelve `usageCount` en los 43 items y `GET /api/v1/content/items/{id}/usages` responde 200 (vacío en la demo, que no tiene usos registrados — los casos poblados los cubren las pruebas de integración).
4. **Pendiente:** captura/verificación visual de la vista real con datos de la API (la demo no tiene etiquetas ni usos que mostrar) y E2E específico del flujo de duplicar/usos.
