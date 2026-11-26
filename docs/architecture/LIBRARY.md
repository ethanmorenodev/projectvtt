# ProjectVTT — Biblioteca universal (LIBRARY.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación.  
**Relación:** producto/UI de la Biblioteca sobre el modelo `CONTENT_MODEL.md`. Las jerarquías del plan (§9.2) son **ejemplos**: cada sistema define sus tipos y relaciones.

---

## 1. Qué es la Biblioteca

El espacio para **consultar, crear, editar, organizar y reutilizar** todo el contenido: oficial, homebrew, de mundo y de campaña. No es una colección de listas fijas de D&D 5e: es un explorador sobre un modelo de tipos, jerarquías, variantes, relaciones y herencia.

**Dónde aparece:**
- **Biblioteca global** (área propia): contenido del usuario, de sus mundos y lo global.
- **Biblioteca de campaña** (sección del espacio de campaña): contenido de esa campaña + lo heredado de su mundo + lo global permitido.
- **Dentro de la mesa**: como herramienta del sistema de ventanas (arrastrar contenido al tablero/character hub sin salir de la sesión).

## 2. Explorador (arquetipo "Explorador")

Composición de tres zonas (patrón de `PROJECT_VISUAL_GUIDELINES.md` §3):

- **Izquierda — árbol de tipos y categorías**: tipos del sistema (`Razas`, `Clases`, `Objetos`, `Criaturas`…) con sus categorías/subcategorías; crear categoría/variante desde el propio árbol. Al añadir contenido dentro de una categoría, **queda organizado automáticamente en ese contexto** (requisito §9.2).
- **Centro — rejilla de miniaturas compactas** (cuadradas, `thumb-md`; regla visual fundamental): filtros por etiquetas, ámbito, autor, estado de publicación; orden (nombre, recientes, uso).
- **Derecha — inspector**: vista rápida del elemento seleccionado (resumen, propiedades, relaciones, imagen, permisos, versiones) con acciones (editar, duplicar, publicar, mover, ver usos).

## 3. Editor universal

Un **único editor** cuyo formulario se adapta al `TypeSchema` del tipo seleccionado (§3 del modelo). Secciones estándar (las que el tipo active):

- Identidad: nombre, resumen, descripción, imagen principal (desde la biblioteca de imágenes), etiquetas.
- Organización: categoría/variante (padre), relaciones (tipadas), referencias adicionales.
- Propiedades: campos tipados del tipo (estadísticas, bonificaciones, rareza, peso…).
- Capacidades: pasivas/habilidades (`hasAbilities`), equipo (`hasEquipment`), contenedores (`isContainer`).
- Requisitos y restricciones (para el creador de personajes).
- Opciones elegibles (ChoiceGroups).
- Visibilidad, permisos y privacidad (ámbito, `grants`, "solo DM").
- Versionado: historial de revisiones, diff y publicación (§7 del modelo).

Acciones del editor: **Guardar borrador** · **Publicar** (crea revisión) · **Proponer** (flujo de aprobación del DM) · **Duplicar** · **Ver usos** (qué personajes/campañas lo usan — requisito §11).

## 4. Biblioteca de imágenes (§6 del plan)

- Toda la biblioteca de imágenes es **compartida y reutilizable**: subir, organizar (álbumes, etiquetas), buscar/filtrar, y **asignar** a cualquier contenido (razas, variantes, clases, habilidades, armas, armaduras, objetos, criaturas, NPC…).
- **Sin duplicar archivos**: los elementos referencian `imageId` (modelo §10; ADR-035).
- El selector de imágenes (desde cualquier editor) abre esta biblioteca con búsqueda + subida rápida; la miniatura resultante es siempre **cuadrada y compacta** (los tamaños/cortes en `DESIGN_TOKENS.md` §11).
- Ampliación de imagen solo bajo petición (lightbox), nunca como representación habitual.

## 5. Búsqueda, filtros y reutilización

- Búsqueda por nombre, etiquetas, tipo, ámbito y texto (posterior: descripción).
- Reutilización entre campañas según ámbitos y permisos (modelo §6); la **copia explícita** ("independizar") existe para cuando se quiere un derivado propio (mundos, §12 del plan y ADR-032).
- Importación/exportación (Fase 7): el modelo con revisiones y relaciones ya la contempla (paquetes portables de contenido).

## 6. Homebrew y permisos (flujo del DM, plan §10/§11)

El contenido homebrew **funciona de verdad** (estructurado, resoluble por el motor cuando aplique — modelo §9):

- El DM crea → publica para la campaña → concede edición/creación a jugadores si quiere → los jugadores **proponen** → el DM **aprueba/rechaza** → la información privada sigue siendo privada.
- El **versionado protege** lo ya usado: ver usos, diffs, actualización explícita o conservación de la versión anterior (nunca silencioso).
- Lo que el motor no resuelve se marca `manual` y se resuelve asistido (sin fingir automatización).

## 7. Contenido por sistema

La Biblioteca muestra los tipos del sistema de juego de la campaña (paquete de tipos del ruleset). Cambiar/añadir tipos es añadir datos (TypeSchema), no código: la interfaz se adapta. Ejemplos del plan (Razas→Humanos→variantes; Objetos→Armas→Espadas; Criaturas→No muertos) viven como **datos de ejemplo sembrados**, no como estructuras fijas.

## 8. Encaje con el MVP y fases

| Fase | Qué de la Biblioteca |
|---|---|
| Puente Fase 2→3 | Modelo + API mínimos (tipos race/class/background/ability/item, revisiones, usos, imágenes básico) — suficiente para el creador |
| Fase 4 | Explorador completo, editor universal, jerarquías/variantes, búsqueda, relaciones, imágenes completas |
| Fase 5 | Capacidades de efectos/elecciones activas (homebrew funcional) |
| Fase 7 | Import/export, mundos ampliados, lore/narrativa |

Nada de esto rompe el MVP: las funcionalidades actuales (mapas, tokens, combate, chat…) siguen usándose igual mientras la Biblioteca crece.
