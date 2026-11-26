# PMB-8.4 — Auditoría de accesibilidad

**Fecha:** 2026-11-17 · **Estado:** ✅ cerrado (decisión del propietario aplicada).

## 1. Criterios (COMPONENT_RULES §95 / WCAG 2.1 AA)

Contraste ≥4,5:1 para texto normal (≥3:1 en componentes y texto grande); foco visible en todos los controles; roles ARIA correctos; `aria-live` en toasts y estado de conexión; movimiento reducido respetado; objetivos ≥28 px.

## 2. Hallazgos y correcciones

La auditoría automatizada (`apps/web/src/theme/accessibility.test.js`) calcula el contraste WCAG de los pares reales de tokens y detectó **4 pares por debajo de AA**:

| Par | Antes | Después | Acción(aprobada por el propietario) |
|---|---|---|---|
| `accent-on` sobre `accent` (botón primario) | 3,67:1 | **5,43:1** | `--pv-accent`: `#7E72C9` → `#6259A3` |
| `accent-on` sobre `accent-hover` | 2,92:1 | **4,69:1** | `--pv-accent-hover`: `#8F84D8` → `#6D63AB` |
| `accent-on` sobre `accent-active` | 4,71:1 | **6,61:1** | `--pv-accent-active`: `#6C61B4` → `#564D8F` |
| `text-muted` sobre `surface-2/3/4` | 4,46 / 4,02 / 3,48:1 | **6,02 / 5,43 / 4,70:1** | `--pv-text-muted`: `#8B8797` → `#A39FAD` |

Se actualizaron las tres fuentes de verdad para evitar dos paletas: `theme/tokens.css`, `theme/tokens.ts` y `docs/visual/DESIGN_TOKENS.md`, además del espejo legacy `src/styles.css` (usado por la pantalla de login). La auditoría congela los 18 pares y verifica que el espejo legacy no vuelva a divergir.

Ya estaban cubiertos y se han verificado: foco `:focus-visible` (anillo de 2 px con offset 2 px), `@media (prefers-reduced-motion: reduce)` que anula duraciones, control mínimo de 28 px, roles ARIA en listas/tabs/ventanas y navegación por teclado de la paleta de comandos (flechas/Enter).

## 3. Verificación

- Web **221/221** (nueva auditoría: 6 pruebas) y `npm run build` limpio.
- E2E con navegador real: `Tab` mueve el foco al primer control y se capta el estado final (`pmb-8.4-accesibilidad-real-2026-11-17.jpeg`).

## 4. Limitaciones (honestas)

- Los contrastes se miden sobre los valores de los tokens, no sobre cada combinación dinámica renderizada (por ejemplo, superimágenes o colores de tokens del mapa).
- La interacción visual completa con lector de pantalla no está automatizada: no hay automatización de UI para Godot y la auditoría DOM no sustituye una prueba manual con NVDA/VoiceOver.
- El contraste de texto sobre imágenes de mapa no se mide: depende del contenido subido por el usuario.
