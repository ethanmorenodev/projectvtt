# PMB-8.6c — Prototipo de rediseño visual moderno

**Fecha:** 2026-11-17 · **Estado:** ✅ **Fases A, B y C integradas — rediseño completo** (decisión del propietario: **mantener el sistema de ventanas ADR-033**).
**Ruta dev:** `#prototipo-rediseno-moderno`.

## Integración de Fase A (realizada)

- `SceneView`: nueva cabecera `.table-header` con navegación horizontal de escenas (pestaña activa en acento) y estado «En vivo»; raíl `.table-rail` reducido a **56 px con iconos** (retroceso, 5 herramientas y biblioteca), con `aria-label`/`title` en cada botón. Se eliminaron del raíl el nombre de escena, la lista de escenas y el texto obsoleto de capas (ahora en la cabecera/herramientas).
- `mesa.css`: grid `56px 1fr 280px` con áreas `header/board/bottom`, estilos de pestañas de escena, active state con `--pv-accent` y foco visible (PMB-8.4).
- **Sin cambios** en ventanas flotantes, lógica, permisos ni contratos.
- Captura real: `pmb-8.6c-mesa-fase-a-real-2026-11-17.jpeg` (1440×900). Web **226/226**, build limpio.

## Integración de Fase B (realizada)

- **Dock de acciones rápidas** en la barra inferior (`TableDock`): *Tirar* (abre la ficha, donde se hacen las tiradas), *Anotar* y *Medir* (abren Herramientas de mapa), *Niebla* (alterna el modo de edición existente) y acción primaria *Añadir token* (se quitó el botón duplicado de la barra antigua).
- **Inspector con pestañas** *Detalles / Capas / Historial*: Detalles conserva el token seleccionado; Capas muestra escena/mapa/niebla; Historial consume `GET /api/v1/campaigns/{id}/history` con carga diferida y estado vacío honesto.
- Las ventanas flotantes siguen intactas: el dock **abre o enfoca** ventanas existentes, no las sustituye. Sin cambios de lógica ni contratos.
- 5 claves i18n nuevas, foco visible en pestañas y dock (PMB-8.4). Captura real: `pmb-8.6c-mesa-fase-b-real-2026-11-17.jpeg`. Web **226/226**, build limpio.

## Integración de Fase C (realizada)

- Cabecera de página compartida para **Inicio** y **Campañas**: título `.f2-page-title` (más grande, `letter-spacing`), subtítulo `.f2-page-subtitle`, separador inferior y **regla de acento de 56 px**; acciones alineadas a la derecha con espaciado más contenido.
- Ritmo vertical más compacto (`.f2-area`, `.f2-columns`) y 2 claves i18n nuevas.
- Sin cambios de lógica ni contratos; mismos tokens `--pv-*` y foco visible. Capturas: `pmb-8.6c-{inicio,campaigns}-fase-c-real-2026-11-17.jpeg`. Web **226/226**, build limpio.

## 1. Investigación de competidores (2026-11-17)

| Referencia | Patrón observado | Aplicación a ProjectVTT |
|---|---|---|
| **Foundry VTT v13** (notas de release 13.340) | Todas las apps migradas a `ApplicationV2`/`ThemeV2`, tema claro/oscuro coherente, controles compactos a la izquierda, pestañas a la derecha (chat, combate, actores…), barra inferior de macros, navegación horizontal de escenas | Rail izquierdo compacto, panel derecho con pestañas, barra inferior de acciones, navegación horizontal de escenas |
| **Carolingian UI** (módulo de Foundry, MIT) | «Sleek minimalist»: menos desorden, color de acento aplicado de forma uniforme, estilos oscuros uniformes en paneles, controles compactos que se revelan al hover, pestañas horizontales | Superficies uniformes con `--pv-accent*, --pv-surface-*`, densidad controlada, foco y hover consistentes (ya reforzado en PMB-8.4/8.6b) |
| **Owlbear Rodeo** | Minimalismo extremo: el tablero es protagonista, casi sin cromo | Ampliar el área del tablero y llevar los controles a rail/dock discretos |
| **Roll20** (crítica generalizada 2025) | UI anticuada, «menús dentro de menús», difícil navegación | Evitar submenús anidados: acciones visibles en rail + dock |

Patrón común: **tablero protagonista, cromo discreto, navegación corta y superficies uniformes**.

## 2. Qué propone el prototipo

- **Cabecera:** marca compacta + **navegación horizontal de escenas** (pestaña activa en acento) + estado «En vivo · usuario» con punto verde.
- **Rail izquierdo (56 px):** 6 iconos SVG (los ya aprobados en PMB-8.6b), active state con acento; nombres en tooltip/`aria-label`.
- **Tablero:** rejilla 48 px visible con viñeta, tokens con color por participante, anotación de medición flotante («35 ft · 7 cuadrados») y etiqueta de escena.
- **Panel derecho (272 px):** pestañas *Detalles / Capas / Historial*, secciones con jerarquía clara y estado vacío guiado.
- **Dock inferior:** accesos rápidos (Tirar, Anotar, Medir, Niebla) + acción primaria «Añadir token».

> Datos de MUESTRA (3 tokens, 3 escenas). No conectado al servidor ni a la mesa real.

## 3. Correspondencia con la app actual

| Hoy (SceneView/AppShell) | Prototipo | Riesgo de integración |
|---|---|---|
| Rail de texto con escenas, capas, herramientas y biblioteca | Rail de iconos + navegación superior de escenas | Medio: hay que reubicar acciones sin perder accesibilidad ni `aria-label` |
| Ventanas flotantes (ADR-033) | Panel derecho con pestañas + ventanas | Alto: **no se propone sustituir** el sistema de ventanas; solo ordenar el panel lateral |
| Barra inferior de controles de mesa | Dock de acciones rápidas | Bajo-medio: reutiliza botones existentes |

## 4. Alcance sugerido por fases (si se aprueba)

1. **Fase A (bajo riesgo):** cabecera con navegación de escenas + rail compacto de iconos en la mesa, sin tocar ventanas ni lógica.
2. **Fase B:** dock inferior de acciones rápidas y panel derecho con pestañas (manteniendo las ventanas ADR-033).
3. **Fase C:** extender lenguaje visual al resto de áreas (Inicio, Campañas, Biblioteca) con el mismo patrón.

Cada fase lleva prototipo/capturas propios y su propia aprobación; no se integra todo de una vez.

## 5. Captura

`pmb-8.6c-rediseno-prototipo-2026-11-17.jpeg` (1440×900).

## 6. Decisiones del propietario (resueltas)

1. **Dirección visual:** aprobada (más ordenada, densa y moderna, tablero protagonista).
2. **Alcance:** **Fases A, B y C** integradas. El patrón se ha aplicado a la mesa, Inicio y Campañas; Biblioteca e Imágenes ya usaban el mismo lenguaje (PMB-8.6b).
3. **Ventanas flotantes:** **se mantienen** (ADR-033); el panel derecho del prototipo es complementario, no sustituto.
