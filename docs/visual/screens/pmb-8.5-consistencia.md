# PMB-8.5 — Consistencia visual final

**Fecha:** 2026-11-17 · **Estado:** ✅ cerrado.

## 1. Alcance

Recorrido real (Playwright, 1280×900, usuario autenticado, datos semilla) por las 11 rutas de la app — `/inicio`, `/campaigns`, `/campaigns/{id}`, `/biblioteca`, `/imagenes`, `/lore`, `/dm`, `/inventario/{actorId}`, `/creditos`, `/campaigns/{id}/characters/new` y la mesa — tras el cambio de tokens de PMB-8.4. Capturas: `pmb-8.5-*-real-2026-11-17.jpeg`.

## 2. Resultado

**Coherente:** el shell (logo, raíl, buscador, sesión), la paleta (morado apagado ya con AA), la tipografía, los estados vacíos y los paneles son los mismos en todas las rutas. No hay pantallas con estilos huérfanos ni textos en inglés.

## 3. Hallazgos corregidos

| Hallazgo | Evidencia | Corrección |
|---|---|---|
| `/lore` estaba **huérfano**: sin enlace en la UI y con texto «llega en la Fase 7» en el espacio de campaña | `pmb-8.5-lore-real…`, `pmb-8.5-campana-real…` | «Historia y lore», «Facciones» y «Localizaciones» (PMB-7.3) muestran ahora una acción **«Abrir lore del mundo»** que lleva a la vista real. Textos obsoletos eliminados. |
| La mesa decía «Capas: próximamente (plan §6)» aunque PMB-7.5 ya implementó capas | `pmb-8.5-mesa-real…` | Texto sustituido por «Controla la visibilidad y la opacidad desde Herramientas de mapa». |

Verificado: web **222/222** (1 prueba nueva en `CampaignSpaceView.test.tsx`) y `npm run build` limpio.

## 4. Limitaciones / deuda menor (sin cambios)

- El control de subida en `/imagenes` usa el widget nativo del navegador («Choose File») en lugar de un zona de arrastre estilizada; es funcional y accesible (`aria-label` presente), pero visualmente más tosco que el resto. Requiere prototipo aprobado si se quiere rediseñar.
- La navegación a `/lore` desde el espacio de campaña se hace mediante un botón dentro de la sección; no se añadió una entrada nueva al raíl principal para no alterar la composición aprobada en PMB-2.8b.
- Las capturas son de escritorio (1280×900); la verificación responsive sigue siendo solo visual/manual.
