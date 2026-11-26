# PMB-7.5 — Prototipo de herramientas de mapa

**Fecha:** 2026-11-17 · **Estado:** ⏳ **prototipo listo, pendiente de aprobación del propietario** (no se ha tocado la app real).
**Ruta dev:** `#prototipo-mapa-herramientas` (`#prototipo-mapa-herramientas-vacio`).

## 1. Referencias

- `docs/visual/PROJECT_VISUAL_GUIDELINES.md` y `COMPONENT_RULES.md`: herramientas discretas sobre el tablero, sin saturar la mesa.
- `ARCHITECTURE.md` §6 / ADR-010: la mesa es tiempo real; la UI solo pide mediciones al servidor, nunca calcula reglas autoritativas.
- D&D 5e: **1 cuadrado = 5 pies**, mostrado en el subtítulo y aplicado a la medición.

## 2. Composición

- **Raíl izquierdo:** `Seleccionar · Medir · Dibujar · Anotar` con estado pulsado y campo de texto para la anotación.
- **Centro — tablero:** rejilla 20×12 con capa SVG superpuesta; medición entre dos clics (línea discontinua + resultado en pies/cuadrados), trazo libre (polilínea) y chinchetas de anotación.
- **Derecha — capas:** Mapa base, Niebla de guerra y Anotaciones con botón de visibilidad (👁️/🚫) y control de opacidad; bloque «Medición» con el resultado.
- Estado vacío honesto: sin anotaciones y medición a 0.

## 3. Datos honestos

Todo de MUESTRA (`mapToolsData.ts`): rejilla, dos anotaciones y tres capas. No se ha conectado al mapa real ni emitido eventos.

## 4. Próximo paso tras aprobación

Integración en `SceneView` (PMB-7.5 real): las mediciones y anotaciones se envían al servidor como comandos con `commandId` (idempotencia ADR-010); la niebla existente (ADR-020) y el grid (ADR-023) se reutilizan sin reescribir PixiJS. El escritorio consume los mismos contratos (ADR-039).

## 5. Capturas

| Estado | Captura |
|---|---|
| Herramientas, capas y tablero | `pmb-7.5-herramientas-prototipo-2026-11-17.jpeg` |
| Medición entre dos puntos (35 ft / 7 cuadrados) | `pmb-7.5-herramientas-medicion-prototipo-2026-11-17.jpeg` |

## 6. Decisiones pendientes del propietario

1. **Composición:** ¿se aprueba el raíl de herramientas + capas a la derecha, o se prefiere integrarlo en la barra inferior de la mesa?
2. **Medición:** ¿basta con clic-clic y resultado en pies/cuadrados, o hace falta arrastre continuo?
3. **Capas:** ¿se aprueba visibilidad + opacidad por capa, o debe añadirse orden/bloqueo desde el inicio?

**Respuesta esperada:** «Aprobado — construir» (con alcance) o los cambios concretos. Hasta entonces no se modifica la app real.
