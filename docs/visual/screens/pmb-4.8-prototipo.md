# PMB-4.8 — Flujo del DM: publicar, permisos, propuestas, aprobar/rechazar

**Fecha:** 2026-11-16 · **Estado:** ✅ **aprobado («Aprobado — construir») e integrado 2026-11-16** (backend, editor y vista `/dm`; verificación en verde).
**Ruta dev:** `#prototipo-dm` (`#prototipo-dm-vacio` para el estado sin propuestas).

## 1. Referencias

- `CONTENT_MODEL.md` §7: `workflowState` = `draft` (autor/DM), `proposed` (espera revisión), `published` (visible según ámbito). El paso a publicado **crea una revisión inmutable** (ADR-031); las usaciones conservan la revisión que usaron (PMB-4.9).
- Ibíd. §6 y ADR-031 §6: los `ContentGrant` (usuario/rol → `view`/`edit`) **se suman** al ámbito; la autoridad final es el servidor. La contención de audiencia impide que un hijo sea más visible que sus ancestros.
- `COMPONENT_RULES.md` §9: ConfirmDialog para publicar/rechazar (cambian el estado compartido y afectan a otros jugadores); el diff se muestra en línea, sin sacar al DM de la bandeja.

## 2. Composición

- **Selector de perspectiva** (Autor / DM) sobre el mismo conjunto de datos de muestra, para revisar ambos lados del flujo.
- **Vista autor:** contenidos propios con estado (Borrador/Propuesta/Publicado) y acción **Proponer** en borradores. El autor no publica directamente.
- **Bandeja del DM:** propuestas pendientes con autor, revisión actual, **Ver cambios** (diff legible frente a la última revisión publicada: `velocidad: 25 → 30`, `+ rasgo`, etc.) y acciones **Aprobar y publicar** / **Rechazar** (devuelve a borrador con nota).
- **Permisos explícitos:** lista `usuario/rol → view/edit` con alta y baja, y nota «el servidor decide la autoridad final».
- **Estados honestos:** contador de pendientes y mensaje «No hay propuestas pendientes de revisión».

## 3. Datos honestos

Todo es de MUESTRA (`dmFlowData.ts`): 2 propuestas, 1 borrador, 2 grants y diffs inventados para la revisión visual. La app real no se toca en esta fase.

## 4. Contrato (aprobado e implementado 2026-11-16)

- `POST /api/v1/content/items/{id}/propose` → `workflowState=proposed` (solo autor o con grant `edit`).
- `GET /api/v1/content/proposals?campaignId=…` → propuestas visibles con autor, estado y **diff** calculado en servidor frente a la última revisión publicada.
- `POST /api/v1/content/items/{id}/approve` → publica (reutiliza `EnsureChainContainedAsync` y crea revisión); **solo GM/Owner** de la campaña.
- `POST /api/v1/content/items/{id}/reject` `{reason?}` → vuelve a `draft`; **solo GM/Owner**.
- `GET/POST/DELETE /api/v1/content/items/{id}/grants` → permisos explícitos; solo quien puede editar el item.
- Reglas del servidor: nadie puede proponer un item global importado ni eludir la contención de audiencia; aprobar publica una revisión nueva sin alterar usos previos.

## 5. Capturas

| Estado | Captura |
|---|---|
| Bandeja del DM + permisos | `pmb-4.8-flujo-dm-prototipo-2026-11-16.jpeg` |
| Diff de una propuesta abierto | `pmb-4.8-flujo-dm-diff-prototipo-2026-11-16.jpeg` |
| **App real integrada** (propuesta con diff en `/dm`) | `pmb-4.8-flujo-dm-real-2026-11-16.jpeg` |

## 6. Decisiones pendientes del propietario

1. **Composición:** ¿se aprueba el selector de perspectiva + bandeja con diff en línea + panel de permisos, o se prefiere integrarlo en el editor/biblioteca?
2. **Contrato:** ¿se aprueban los endpoints anteriores tal cual (aditivos)?
3. **Rechazo:** ¿ debe guardarse el motivo del rechazo como metadato visible para el autor, o basta con devolver a borrador y avisar?

**Respuesta esperada:** «Aprobado — construir» (con alcance) o los cambios concretos. Hasta entonces no se modifica la app real ni el contrato.
