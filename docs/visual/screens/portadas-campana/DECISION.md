# Portada de campaña desde la tarjeta (EXP-23, 2026-11-25)

Continuación de EXP-22 (retratos de personaje). El propietario pidió poder poner imagen también a las campañas.

## Contrato usado (no uno nuevo)
El backend **ya tenía** el contrato completo y no se ha tocado:
- `PUT /api/v1/campaigns/{id}/cover` (multipart `file`), `GET` y `DELETE`.
- `CampaignService.UploadCoverAsync` aplica `RequireRole(Owner)` e `ImageGuard.ValidateAsync` (PNG/JPEG/WebP/SVG, firma binaria, ≤5 MB).
- `CampaignSummary.hasCover` ya existe.

Mi propuesta anterior citaba por error `/images` con `role=campaign-cover` y `/scenes/{id}/cover`; la comprobación del código mostró que lo correcto es el endpoint de campaña, que además guarda bajo `campaign-covers/{id}` y no crea un `ImageAsset`. **No se han añadido contratos nuevos ni duplicado reglas.**

## Cambio en la web
- Las tarjetas de `Campañas` muestran la portada subida (ya lo hacían vía `useCampaignCover`) y ahora tienen un botón de lápiz **a la izquierda de la cubierta**, hermano del botón de apertura —nunca un control interactivo dentro de otro—, visible solo para `role === 'Owner'` (coherente con la autoridad del servidor).
- `uploadCover` se reutiliza directamente; tras el PUT se muestra el toast «Portada actualizada.» y se recarga la lista.
- **Corrección latente:** `useCampaignCover` guardaba el objectURL en estado, así que al **reemplazar** una portada (`hasCover` sigue en `true`) se seguía viendo la imagen antigua. `covers.ts` mantiene ahora una revisión por campaña con `useSyncExternalStore`: `invalidateCover` incrementa la revisión y avisa a los hooks suscritos, que vuelven a descargar el blob. Aprovecha también a Inicio, Espacio de campaña y el diálogo de gestión.

## Reglas respetadas
- El servidor valida permisos, tipo, firma y tamaño; el cliente solo ofrece el control y muestra el blob autenticado.
- Sin arte protegido: la prueba usa un icono CC0 ya incluido en `public/art/fantasy/`.
- Error explícito: `t.campanas.coverError` aparece bajo el lápiz si falla la subida.

## Verificación
- Web: build limpio · **253/253**.
- E2E: **9/9**, con `CampaignCoverFlowTests`: sube una portada desde la tarjeta, comprueba `GET /cover`, verifica el `blob:` en el `img`, **la reemplaza** y comprueba que el `src` cambia (caso que antes fallaba por caché).
- Captura: `tarjetas-con-portada.jpeg` (se ve la portada ya reemplazada y los dos toasts).

## EXP-24: mismo affordance en Inicio
La fila «Continuar jugando» mostraba la portada pero no permitía cambiarla porque su tarjeta era un único `<button>` (no se puede meter un `input` dentro). Se ha reestructurado en un `<div class="ie-campaign">` contenedor con dos hermanos:
- `<button class="ie-campaign__main">`: apertura de la campaña, con todo el arte y el texto.
- `<label class="ie-campaign__cover-change">`: lápiz para subir la portada, solo para `Owner`.

El aspecto no cambia (el botón ocupa el 100 % del contenedor y hereda tipografía/color), y se mantiene el foco visible en el botón de apertura. Reutiliza el mismo `uploadCover` y la corrección de caché de EXP-23.

## Verificación (EXP-24)
- Web: build limpio · **253/253**.
- E2E: **10/10**. `CampaignCoverFlowTests` añade `CambiarPortadaDesdeInicio_PersisteYSeMuestra`: navega a `/inicio`, sube la portada, comprueba `GET /cover` y el `blob:` en la tarjeta.
- Captura: `inicio-con-portada.jpeg`.

## EXP-25: «Quitar portada» en la misma tarjeta
Se unifican cambiar y quitar en un componente compartido `src/ui/CampaignCoverActions.tsx` (+ CSS), que sustituye al lápiz duplicado de ambas vistas. Devuelve `null` si `role !== 'Owner'` (visibilidad coherente con `RequireRole(Owner)` del servidor, que sigue siendo la autoridad), muestra ✎ y, solo si `hasCover`, un «×» debajo. Bloquea ambos controles con `disabled`/`aria-busy` durante la petición para evitar una carrera de doble clic, y muestra el mensaje concreto del servidor si falla. El toast correcto (actualizado/eliminado) lo emite el propio componente, así que las vistas no repiten esa decisión.

Sin contrato nuevo: se reutilizan `uploadCover`/`deleteCover` y `DELETE /campaigns/{id}/cover` (GET devuelve 404 sin portada).

### Correcciones derivadas de la revisión arquitectónica
- `deleteCover` no comprobaba `response.ok`: un 403/500 se trataba como éxito y `invalidateCover` se ejecutaba igual. Ahora propaga el error (`src/api/covers.ts`). Es cambio de comportamiento en un módulo compartido por cuatro consumidores; `CampaignManageDialog.removeCover` añadió el `catch` que le faltaba.
- Se actualizan los selectores de los dos tests E2E existentes, que apuntaban a las clases del lápiz antiguo.

### Coordinación
Trabajo repartido entre cuatro subagentes con archivos dueños y sin solapamiento: componente compartido (A), cableado en Campañas (B), cableado en Inicio (C) y pruebas E2E (D). Revisión independiente del `architect` antes y después de la integración; el coordinador integró, validó y documentó.

## Verificación final
- Web: build limpio · **253/253** (dos ejecuciones consecutivas limpias; una primera ejecución tuvo un fallo aislado de jsdom que no se reprodujo).
- E2E: **13/13**. `CampaignCoverRemoveFlowTests` cubre borrado desde Campañas (404 + arte de respaldo + toast), borrado desde Inicio con comprobación de caché compartida al navegar a Campañas, y Player sin controles.
- Capturas: `03-quitar-portada-campanas.jpeg` y `04-quitar-portada-inicio.jpeg` (ambos controles visibles sobre la portada).

## EXP-26: confirmación antes de borrar + carrera en el almacén
El botón «×» abre ahora un `ConfirmDialog` reutilizado (`¿Quitar la portada?`, botón destructivo y `Cancelar`). Se renderiza con `createPortal` a `document.body` porque las tarjetas tienen `overflow: hidden` y `clip-path`: sin portal el modal quedaría recortado. No se pide frase de confirmación porque borrar es reversible (se puede volver a subir).

### Bug de backend encontrado por QA al probar el nuevo flujo
El `DELETE` devolvía **500 de forma intermitente** en Windows: el `GET` anterior aún tenía abierto el stream de `FileAssetStore.OpenReadAsync`, que usaba `FileShare.Read` y bloqueaba `File.Delete`. No era un fallo del test. Arreglo en `backend/Api/Shared/FileAssetStore.cs`:
- `OpenReadAsync`: `FileShare.ReadWrite | FileShare.Delete` y apertura directa con `try/catch` de `FileNotFoundException`/`DirectoryNotFoundException` (elimina el TOCTOU de `File.Exists` + apertura).
- `SaveAsync`: escribe en un `.tmp` del mismo directorio y aplica `File.Replace(temp, path, null)` (atómico para lectores existentes; `File.Move(overwrite: true)` falló realmente en Windows con `UnauthorizedAccessException`), con limpieza en `finally` y fallback a `File.Move` si el destino desaparece.
- Afecta a un módulo compartido (mapas, imágenes, portadas y backups): revisado por el `architect`, que exigió el reemplazo atómico para evitar contenido mixto o copias parciales en backup en caliente. Nueva prueba unitaria que verifica que el lector abierto sigue viendo el contenido íntegro durante el reemplazo.

### Coordinación
Dos subagentes en paralelo (confirmación+i18n / E2E), un tercero para el fix del almacén y sus pruebas, y revisión del `architect` antes y después. Un hallazgo medio del revisor (truncado en sitio) se corrigió antes del commit.

## Verificación final (EXP-26)
- Web: build limpio · **253/253**.
- Unit **61/61**, Integration **283/283**, E2E **14/14** (nuevo caso de cancelar conserva la portada).
- Captura: `05-confirmar-quitar-portada.jpeg` (modal completo sobre la página, no recortado).

## Pendiente
Riesgo menor documentado: un backup en caliente simultáneo podría enumerar el fichero `.tmp` antes de su limpieza (asset huérfano inofensivo en la instantánea). Fuera de alcance de EXP-26. El `ConfirmDialog` compartido no atrapa Tab ni Escape (preexistente, no introducido aquí).
