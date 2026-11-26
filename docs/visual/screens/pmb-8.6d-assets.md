# PMB-8.6d — Ampliación de la biblioteca de assets

**Fecha:** 2026-11-17 · **Estado:** ✅ integrado.

## Problema

Tras PMB-8.6b, el mapa era **solo por tipo**: las 9 razas del SRD mostraban el mismo icono de elfo, las 12 clases el mismo pícaro, etc. El propietario señaló que «todas utilizan la misma».

## Solución

- **26 SVG nuevos** de game-icons.net (CC BY 3.0), descargados en variante blanco/transparente y registrados en `apps/web/src/ui/prototype/fase8/icons/LICENSES.md` (autor por archivo). Total: **47 iconos** en `apps/web/public/icons/`.
- `ui/contentIcons.ts` ahora resuelve el icono **primero por palabra clave del nombre** (en español e inglés: *dragonborn→dragon-head*, *dwarf/enano→dwarf-face*, *elf/elfo→woman-elf-face*, *wizard/mago→wizard-face*, *ranger/explorador→bow-arrow*…) y **después por tipo**, con la inicial del nombre como último fallback.
- La Biblioteca pasa `item.name` al resolver. Sin cambios de contrato: es una pista visual de categoría, **no arte literal** del objeto.

## Atribución

La línea ya presente en Créditos y en `GET /api/v1/credits/export` cubre todos los iconos: «Iconos de game-icons.net, por Lorc, Delapouite y colaboradores, CC BY 3.0». No hace falta añadir entradas por archivo (ADR-036).

## Verificación

- `ui/contentIcons.test.ts`: 4 pruebas (por nombre, por clase, fallback por tipo, sin tipo). Web **230/230**, build limpio.
- Captura real: `pmb-8.6d-biblioteca-variada-real-2026-11-17.jpeg` — razas y clases del SRD con iconos distintos.

**Salvedad:** algunas coincidencias son aproximadas (p. ej. *halfling* usa una cabeza vikinga por falta de icono específico); se prefirió variedad honesta antes que inventar un icono que no existe en la fuente.
