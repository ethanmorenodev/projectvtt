# Decisión — Paquete `dnd5e-srd-5.2` (SRD 5.2.1)

## Fuente y licencia

- **Dataset usado:** `https://www.dnd5eapi.co/api/2024` (API 2024, SRD 5.2.1). Datos de
  [`github.com/5e-bits/5e-database`](https://github.com/5e-bits/5e-database), **CC BY 4.0**.
  Índices verificados el 2026-11-25: 339 hechizos, 341 criaturas, 182 objetos de equipo.
- **Referencia oficial:** PDF
  [`SRD_CC_v5.2.1.pdf`](https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf),
  publicado por Wizards of the Coast bajo **Creative Commons Attribution 4.0 International**.
- **Atribución literal exigida por WotC:** «Wizards of the Coast, System Reference Document
  5.2.1, licensed under Creative Commons Attribution 4.0 International (CC BY 4.0).»
  Registrada tal cual en `attribution[0]` del manifest; `attribution[1]` añade la procedencia
  del dataset (campo ya existente, sin inventar campos nuevos). `sourceUrl` apunta al dataset
  utilizado para la extracción.
- **Fecha de extracción:** 2026-11-25.
- ADR-037 permite la convivencia de SRD 5.1 y 5.2.1 (ambos CC BY 4.0). No se incluye contenido
  de suplementos ni arte de Wizards.

## Decisión: subconjunto reproducible

Importar un **subconjunto** y no el catálogo completo: la biblioteca ya carga el paquete 5.1
y un volumen mayor no aporta valor a esta fase. El criterio es determinista: ordenar cada
índice por `index` (comparación de cadenas, sin `localeCompare`) y tomar los primeros N.

| Tipo (`typeKey`) | Prefijo `itemKey` | Tags | Límite | Importados |
|---|---|---|---:|---:|
| `ability` (hechizos) | `ability/` | `spell`, `srd`, `5.2.1` | `LIMIT_SPELLS=40` | 40 |
| `creature` | `creature/` | `creature`, `srd`, `5.2.1` | `LIMIT_MONSTERS=30` | 30 |
| `item` (equipo) | `item/` | `item`, `srd`, `5.2.1` | `LIMIT_EQUIPMENT=30` | 30 |
| **Total** | | | | **100** |

- **Regenerar:** `node tools/seed/transform-srd-52.mjs`. Para ampliar, fijar las variables de
  entorno, p. ej. `LIMIT_SPELLS=339 LIMIT_MONSTERS=341 LIMIT_EQUIPMENT=182 node tools/seed/transform-srd-52.mjs`.
  El script consulta la API real, reintenta con backoff ante HTTP 429/5xx, usa concurrencia
  acotada (3) y ordena el resultado por `itemKey`. No escribe campos volátiles (`updated_at`):
  dos ejecuciones con la misma entrada produjeron el mismo SHA-256.
- **Asociación al pack:** `ContentSeedLoader` toma `pack.key` del contenido del JSON, **no del
  nombre de archivo**. Por eso `dnd5e-srd-5.2-content.json` declara `pack.key: "dnd5e-srd-5.2"`,
  igual que `dnd5e-srd-5.2.json`; la reimportación es idempotente por `(packKey, itemKey)`.
- **Mapeo:** en la API 2024 el campo es `description` (string en hechizos, lista en equipo)
  donde 5.1 usaba `desc`; `casting_time: "Action"` se normaliza a `activation: "action"`.
  Las descripciones se copian literalmente. Las criaturas 5.2 no tienen `description` en la
  API: su descripción se compone solo de textos literales de `special_abilities` y `actions`
  (`nombre — descripción`, hasta 6 entradas), sin prosa inventada.

## Cotejo de muestra contra la API (2026-11-25)

| ítem | Comprobación | Resultado |
|---|---|---|
| `ability/acid-arrow` | `name` idéntico; `description` idéntica a la API (327 caracteres); `casting_time: "Action"` → `activation: "action"` | ✅ |
| `item/acid` | `name` idéntico; `description` idéntica a la API (303 caracteres); `weight: 1` | ✅ |
| `creature/aboleth` | Sin `description` en la API; los 6 segmentos coinciden literalmente con `special_abilities` + `actions`; CA 17, PV 150, CR 10 | ✅ |

Los 100 `itemKey` son únicos y los 100 ítems llevan `srd` y `5.2.1` en `tags`.

Esta decisión no activa ni cierra tareas del backlog.
