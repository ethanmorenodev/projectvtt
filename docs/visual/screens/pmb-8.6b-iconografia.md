# PMB-8.6b — Prototipo de mejora visual (iconografía)

**Fecha:** 2026-11-17 · **Estado:** ✅ **aprobado e integrado 2026-11-17** (decisión del propietario: dirección visual Biblioteca + raíl, licencia CC BY 3.0 con atribución).
**Ruta dev:** `#prototipo-mejora-visual`.

## Integración realizada

- 16 SVG movidos a `apps/web/public/icons/`; mapa tipo→icono en `apps/web/src/ui/contentIcons.ts` (inicial como fallback).
- `LibraryView`: tarjetas con icono SVG sobre degradado de acento (`pmb-8.6b-biblioteca-real-2026-11-17.jpeg`).
- **El raíl no se cambió**: `AppShell` ya usaba iconos SVG propios (`ui/Icons.tsx`), así que ya cumplía el criterio aprobado; sustituirlos habría sido una regresión innecesaria.
- Atribución CC BY 3.0 en `CreditsView` (`pmb-8.6b-creditos-real-2026-11-17.jpeg`) y en `GET /api/v1/credits/export`, con aserción en `ImageAssetTests`.
- Web **224/224**, Integration **7/7** en la suite de imágenes, builds limpios.

**Auditoría final de emoji (misma aprobación):** se sustituyeron también los últimos emoji coloridos de la app real: capas de mapa 👁️/🚫 → `gaze.svg`/`sight-disabled.svg` (vista real y prototipo), silueta de inventario 🧍 → `rogue.svg`, y engranaje de campañas ⚙ → `cog.svg`. Los tres nuevos SVG (Lorc / Skoll, CC BY 3.0) están registrados en `LICENSES.md`. Quedan únicamente glifos tipográficos (`✕`, `✓`) y los emoji de datos de muestra de los **prototipos**, que son solo desarrollo. Web **224/224**.

**Extensión final (misma aprobación):** se sustituyeron los emoji de las herramientas de mapa por SVG: chinchetas 📍 → `position-marker.svg` (en la vista real y el prototipo) y 🌫️ → `fog.svg` (en el prototipo), ambos con `aria-hidden`. `fog.svg` (Delapouite, CC BY 3.0) registrado en `LICENSES.md`. Captura `pmb-8.6b-mapa-iconos-real-2026-11-17.jpeg`. Web **224/224**.

**Extensión posterior (misma aprobación):** se añadieron iconos a las 5 herramientas de la mesa (`ToolLauncher` y cabecera de ventana, vía `ToolDescriptor.icon`) y a los estados vacíos guiados de Inicio, Campañas, Espacio de campaña, Biblioteca y Biblioteca de imágenes, mediante el componente `ui/Icon`. Se descargó además `magnifying-glass.svg` (Lorc, CC BY 3.0, registrado en `LICENSES.md`). Capturas: `pmb-8.6b-mesa-iconos-real-2026-11-17.jpeg` y `pmb-8.6b-estado-vacio-real-2026-11-17.jpeg`. Web **224/224**, build limpio.

**Salvedad honesta:** el icono se elige por **tipo** de contenido, no por ítem; al filtrar por una sola tipo (p. ej. 18 razas) todas las tarjetas muestran el mismo icono y se distingue por el nombre. Es el alcance aprobado; variar por ítem exigiría una semántica por nombre que no se ha inventado.

## 1. Investigación de assets

| Fuente | Licencia | Uso valorado | Decisión |
|---|---|---|---|
| **game-icons.net** (4.180 SVG) | **CC BY 3.0** (atribución obligatoria) | Iconos de contenido (razas, clases, criaturas, facciones, localizaciones, eventos), raíl y herramientas | **Elegida** para el prototipo: vectorial, teñible, coherente con el tema oscuro |
| **Kenney** (game icons / RPG UI, CC0) | CC0 (sin atribución obligatoria) | Iconos genéricos de UI y bordes fantasy | Reservada como alternativa si se quiere evitar atribución |
| Sonniss GDC / freesound | royalty-free / CC0 | Audio | Evaluado en `docs/AUDIO_EVALUATION.md`: diferido |

Se descargaron **16 SVG** (variante blanco sobre transparente) a `apps/web/src/ui/prototype/fase8/icons/`, con ficha `LICENSES.md` que registra fuente, autor y licencia de cada uno.

## 2. Qué cambia el prototipo

- **Antes:** las tarjetas de la Biblioteca muestran una letra grande (inicial) sobre fondo liso — captura `pmb-8.5-biblioteca-real-2026-11-17.jpeg`.
- **Después:** cada tipo de contenido tiene un **icono vectorial** sobre un degradado suave del color de acento, manteniendo nombre y tipo. El raíl incorpora iconos junto a las etiquetas.
- **Catálogo:** los 16 iconos disponibles con su nombre, para valorar cobertura.

Referencias: `docs/visual/PROJECT_VISUAL_GUIDELINES.md` y `COMPONENT_RULES.md` (iconos de herramientas con `aria-label`, una acción primaria por superficie, contraste AA verificado en PMB-8.4).

## 3. Alcance de una futura integración (tras aprobación)

1. Mover los SVG a `apps/web/public/icons/` y servirlos como assets estáticos.
2. `LibraryView`: mapear `type.key` → icono (razas/clases/objetos/criaturas/facción/localización/evento/habilidad), conservando la inicial como **fallback** cuando no haya icono o imagen del usuario.
3. `AppShell`: iconos en el raíl (manteniendo `aria-label` y foco visible).
4. **Atribución:** añadir la línea «Iconos de game-icons.net, por Lorc, Delapouite y colaboradores, CC BY 3.0» a `CreditsView` y a `GET /api/v1/credits/export` (ADR-036). Sin esto, **no se integra**.
5. Verificación: web + E2E + captura de la vista real y auditoría de contraste del nuevo degradado.

## 4. Capturas

| Estado | Captura |
|---|---|
| Prototipo completo (comparación + raíl + catálogo) | `pmb-8.6b-iconografia-prototipo-2026-11-17.jpeg` |
| Comparación antes/después | `pmb-8.6b-comparacion-prototipo-2026-11-17.jpeg` |

## 5. Decisiones del propietario (resueltas)

1. **Dirección visual:** aprobada — iconos SVG con degradado de acento en tarjetas de Biblioteca.
2. **Licencia:** aprobada **CC BY 3.0 con atribución** en Créditos y en la exportación (se descartó Kenney CC0).
3. **Alcance:** Biblioteca + raíl. El raíl ya tenía iconos SVG propios, por lo que no requirió cambios.
