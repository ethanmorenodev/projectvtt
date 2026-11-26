# PMB-4.6 — Relaciones tipadas (edición y navegación)

**Fecha:** 2026-11-16 · **Estado:** ✅ **aprobado («Aprobado — construir») e integrado 2026-11-16** (UI real + endpoints aditivos; verificación en verde).
**Base:** explorador aprobado en PMB-4.1/4.5 y editor PMB-4.4. **Rutas dev:** `#prototipo-biblioteca-relaciones` y `#prototipo-biblioteca-relaciones-vacio`.

## 1. Principios y referencias

- `CONTENT_MODEL.md` §8: `ContentRelation{fromItemId, kind, toItemId, label?, metadata?}`; el `kind` es dato definido por el sistema (`grants`, `requires`, `excludes`, `memberOf`, `locatedIn`, `leadsTo`, `owns`) y se valida contra las relaciones permitidas por tipo. Sirve para requisitos/elecciones del creador, grafo del Character Hub y navegación de lore.
- `COMPONENT_RULES.md` §9: el editor de relaciones es un modal porque concentra una decisión (qué enlace crear) sin sacar al usuario del explorador; no es destructivo, así que **Quitar** no pide confirmación adicional en el prototipo.
- Arquetipo «Explorador» de `LIBRARY.md` §2: el frame de tres zonas (árbol · rejilla · inspector) **no cambia**; las relaciones viven en el inspector, como «Ver usos» en PMB-4.5.

## 2. Composición

- **Inspector — sección «Relaciones»** (entre Herencia y Procedencia):
  - Lista de relaciones **salientes** (`kind → destino`) y **entrantes** (`kind ← origen`), con la etiqueta opcional detrás.
  - Cada destino/origen es un **enlace de navegación**: al pulsarlo se selecciona ese elemento y se cambia el tipo activo en el árbol (si procede), manteniendo el contexto.
  - Botón **«Editar relaciones»** y nota honesta: «Relaciones de muestra: los tipos permitidos los valida el servidor por sistema».
  - Estado vacío explícito: «Sin relaciones registradas para este elemento» (variante `-relaciones-vacio`).
- **Modal «Editar relaciones de «X»»**:
  - `Tipo` (select con los 7 kinds del modelo), `Destino` (select de items, excluido el propio), `Etiqueta (opcional)`.
  - Lista de relaciones salientes actuales con **Quitar**; acciones **Cerrar** y **Añadir relación**.
  - El alta/baja es **local al prototipo** y avisa por `role=status`.

## 3. Datos honestos

- **Reales:** los 18 razas / 12 clases / 13 trasfondos del seed SRD y su procedencia CC BY 4.0.
- **MUESTRA:** 5 relaciones (`RELATIONS` en `bibliotecaData.ts`) porque el seed **no incluye ninguna**; están marcadas como muestra en código y en la UI.

## 4. Contrato (aprobado e implementado 2026-11-16)

Sujeto a aprobación; sería **aditivo** (no rompe clientes actuales):

- `GET /api/v1/content/items/{id}/relations` → `[{id, fromItemId, kind, toItemId, label, metadata}]`, con filtro opcional `direction=in|out`.
- `POST /api/v1/content/items/{id}/relations` `{kind, toItemId, label?}` → 201 con la relación creada.
- `DELETE /api/v1/content/items/{id}/relations/{relationId}` → 204.
- Reglas del servidor (no del cliente): el `kind` debe estar permitido para el sistema/tipo de origen; el actor debe poder **ver ambos extremos** y **editar el origen**; la tabla ya existe (`content_relations`, índice único `(FromItemId, Kind, ToItemId)`).

## 5. Capturas

| Estado | Captura |
|---|---|
| Inspector con relaciones saliente/entrante y navegación | `pmb-4.6-relaciones-prototipo-2026-11-16.jpeg` |
| Modal de edición (tipo, destino, etiqueta, quitar) | `pmb-4.6-relaciones-editor-prototipo-2026-11-16.jpeg` |
| **App real integrada** (item personal con `grants → Dwarf`) | `pmb-4.6-relaciones-real-2026-11-16.jpeg` |

## 6. Decisiones pendientes del propietario

1. **Composición:** ¿se aprueba la sección en el inspector + modal, o se prefiere otro sitio (p. ej. pestaña dedicada)?
2. **Contrato:** ¿se aprueban los tres endpoints propuestos tal cual (aditivos)?
3. **Kinds:** ¿lista abierta de 7 kinds definidos en `CONTENT_MODEL.md` §8, o restringida por tipo de contenido desde el inicio?

**Respuesta esperada:** «Aprobado — construir» (con alcance) o los cambios concretos. Hasta entonces no se modifica la app real ni el contrato.
