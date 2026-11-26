# PMB-4.10 — Sustitución de recursos conservando referencias

**Fecha:** 2026-11-16 · **Estado:** ✅ **aprobado («Aprobado — construir») e integrado 2026-11-16** (backend, vista de imágenes y verificación en verde).
**Ruta dev:** `#prototipo-sustituir` (`#prototipo-sustituir-vacio`).

## 1. Referencias

- `POST_MVP_ASSETS_RESEARCH.md` §3.5 / ADR-035: el mismo archivo sirve a N elementos mediante `imageId`; **sustituir conserva todas las referencias**.
- Ibíd. §4 y ADR-036: los assets tienen paquete/licencia/atribución; los filtros por procedencia y licencia son parte de la biblioteca de la Fase 4.
- `ImageAsset.VariantsJson` ya existe en el modelo: el prototipo muestra `thumb-sm/md/lg` como resultado de la sustitución.

## 2. Composición

- **Filtros:** paquete (`dnd5e-srd-5.1` / Propio) y licencia (CC BY 3.0 / CC0 / Propietario), con estado vacío honesto.
- **Rejilla:** recursos con licencia y paquete visibles; selección de un recurso.
- **Inspector:** `imageId` mostrado explícitamente, lista de **referencias** («Usado por 3 elemento(s)»: Dwarf, Hill Dwarf, Mountain Dwarf) y badges de variantes.
- **Diálogo «Sustituir»:** nuevo archivo, aviso «se sobrescribe el mismo almacenamiento; el id y las N referencias no cambian», Cancelar/Sustituir.
- Tras sustituir: aviso «imageId inmutable; N referencias intactas», nombre/tamaño actualizados y variantes regeneradas.

## 3. Datos honestos

Todo de MUESTRA (`replaceData.ts`): 3 assets, referencias y variantes inventadas para revisar la interacción.

## 4. Contrato (aprobado e implementado 2026-11-16)

- `POST /api/v1/images/{id}/replace` (multipart): `file` obligatorio y opcionalmente `thumb-sm`, `thumb-md`, `thumb-lg`; validación de formato/tamaño **en servidor**, sin ejecución de código. Sobrescribe el mismo `StorageKey` y actualiza `FileName`/`ContentType`/`SizeBytes`/`VariantsJson` **sin cambiar `Id`**.
- `GET /api/v1/images?packKey=…&license=…` (nuevos filtros; combinables con los existentes).
- Aditivos al DTO: `referenceCount` (calculado en servidor desde `ContentItem.ImageId`) y `variants` (claves derivadas de `VariantsJson`).
- El servidor garantiza que cualquier `ContentItem` que referencia el `imageId` sigue apuntando al mismo recurso; la UI solo informa.

## 5. Capturas

| Estado | Captura |
|---|---|
| Rejilla filtrable + inspector con referencias | `pmb-4.10-sustituir-prototipo-2026-11-16.jpeg` |
| Diálogo de sustitución | `pmb-4.10-sustituir-dialog-prototipo-2026-11-16.jpeg` |
| **App real integrada** (referencias + filtro por paquete) | `pmb-4.10-sustituir-real-2026-11-16.jpeg` |
| **App real integrada** (diálogo de sustitución) | `pmb-4.10-sustituir-dialog-real-2026-11-16.jpeg` |

## 6. Decisiones pendientes del propietario

1. **Composición:** ¿se aprueba el diálogo de sustitución en el inspector y los filtros en la biblioteca de imágenes?
2. **Variantes:** ¿se aprueba que el cliente envíe las variantes (ADR-035: generadas en cliente, validadas en servidor), o se generan en servidor más adelante?
3. **Historial:** ¿la sustitución debe conservar el archivo anterior como versión recuperable, o basta con sobrescribir (como propone el prototipo)?

**Respuesta esperada:** «Aprobado — construir» (con alcance) o los cambios concretos. Hasta entonces no se modifica la app real ni el contrato.
