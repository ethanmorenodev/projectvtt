# ProjectVTT — Guías visuales (PROJECT_VISUAL_GUIDELINES)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación del propietario.  
**Ámbito:** dirección visual de toda la aplicación post-MVP (web primero; el escritorio sigue la misma dirección).  
**Documentos hermanos:** `DESIGN_TOKENS.md` (valores), `COMPONENT_RULES.md` (componentes), `REFERENCE_LIBRARY.md` (referencias y principios extraídos).

---

## 1. Qué es ProjectVTT visualmente

Una **herramienta creativa profesional**: la interfaz debe sentirse como un instrumento de trabajo para construir universos y dirigir partidas, no como una web de gestión ni como un juego.

Dos exigencias simultáneas:

1. **Complejidad funcional alta** (muchas herramientas, datos densos, edición profunda).
2. **Complejidad visual controlada** (jerarquía clara, superficies tranquilas, decoración mínima).

La interfaz debe **permanecer neutral** frente al estilo artístico de cada universo: fantasía, ciencia ficción, terror, anime o lo que el usuario cree. ProjectVTT **no impone estética** sobre los contenidos del usuario: los marcos son neutros, las miniaturas y portadas son las que el usuario elige.

## 2. Identidad visual (dirección aprobada por el propietario)

- Tema oscuro de **carbón** (nunca negro puro como fondo general).
- Acento **morado apagado y moderado**: marca y foco, nunca decoración masiva.
- Superficies diferenciadas por **tonalidades** (no por bordes gruesos ni tarjetas elevadas por doquier).
- Tipografía **legible** por encima de lo expresivo.
- **Iconografía coherente** (un solo set, mismo grosor de trazo).
- **Espaciado y jerarquía consistentes** (retícula de 4 px; densidad compacta).
- **Decoración moderada**: nada de saturación, animaciones constantes, estética SaaS genérica ni aspecto de panel administrativo.

Valores concretos: `DESIGN_TOKENS.md`. Reglas de uso: `COMPONENT_RULES.md`.

## 3. Arquetipos de pantalla

Cada pantalla se diseña según su función; **no se aplica una plantilla única**:

| Arquetipo | Pantallas | Composición |
|---|---|---|
| **Dashboard** | Inicio | Escaneable: bloques compactos (recientes, invitaciones, actividad), una acción primaria, mucho aire |
| **Espacio de gestión** | Espacio de campaña, Gestión de cuenta | Navegación lateral + contenido; tablas y listas densas pero legibles |
| **Explorador** | Biblioteca, Escenas y mapas, Imágenes | Árbol/categorías a la izquierda, rejilla de miniaturas compactas al centro, inspector a la derecha |
| **Editor** | Editor universal de contenido, Creador de personajes | Formulario por secciones + previsualización; asistente por pasos cuando el flujo es largo |
| **Workspace (mesa)** | Mesa de juego | Tablero a sangre completa; herramientas en ventanas/paneles flotantes; HUD discreto |
| **Hub de personaje** | Character Hub | Composición tipo menú de RPG: perfil + equipo + relaciones + historia, dentro de una ventana |

## 4. Proceso visual obligatorio (por pantalla)

Antes de implementar cualquier pantalla importante:

1. Analizar su función y las necesidades reales del flujo.
2. Buscar referencias visuales concretas y extraer principios (registrarlas en `REFERENCE_LIBRARY.md`).
3. Proponer la distribución (boceto estructural).
4. Construir un **prototipo visual limitado** (sin lógica de negocio).
5. Presentar **capturas reales** para revisión (app en marcha o prototipo navegable).
6. **Esperar la aprobación del propietario** antes de integrar.
7. Tras integrar, corregir jerarquía/comodidad según la captura real.
8. Registrar la decisión y la captura aprobada en `docs/visual/screens/<pantalla>.md`.

Reglas del proceso:

- **No se implementa ninguna pantalla completa a partir solo de una descripción textual.**
- El prototipo es limitado a propósito: composición, jerarquía y estados principales (no datos reales ni lógica).
- Cada revisión documenta: fecha, captura, cambios pedidos, aprobación.

## 5. Composición de herramientas simultáneas

- La mesa de juego **nunca desaparece** al consultar o editar contenido: las herramientas se abren como **ventanas** sobre el tablero (sistema único, `docs/architecture/WINDOWS.md`).
- Revelación progresiva: lo esencial siempre visible; lo contextual en el inspector; lo profundo en ventanas/biblioteca.
- Los paneles pueden colapsar; el espacio de juego es el que manda.

## 6. Hacer / Evitar

**Hacer:** superficies por tonalidades; jerarquía tipográfica clara; botones con jerarquía (una acción primaria por superficie); miniaturas cuadradas compactas para el contenido; estados vacíos con guía; contrastes cómodos para sesiones largas; foco visible y navegación por teclado.

**Evitar:** negro puro; saturación; decoración excesiva; paneles sin función; botones todos iguales; animaciones constantes; estética SaaS/administrativa; imágenes grandes como representación habitual del contenido; imponer estética a los universos de los usuarios.

## 7. Accesibilidad

- Contraste mínimo 4.5:1 en texto (3:1 en texto grande y componentes).
- Foco visible siempre (anillo de acento, 2 px, desplazamiento 2 px).
- Toda acción alcanzable por teclado; iconos siempre con etiqueta accesible.
- Respetar `prefers-reduced-motion`.
- Objetivo táctil/clic mínimo 28 px en densidad compacta.

## 8. Prototipos y capturas (cómo trabajamos)

- Los prototipos se revisan con capturas reales: app en marcha (`scripts/ProjectVTT-inicio.ps1`) o prototipo navegable aislado.
- Las capturas aprobadas se archivan junto a la decisión (`docs/visual/screens/`).
- Las pantallas del MVP existente se rediseñan respetando sus flujos: la funcionalidad no se pierde (regla del plan: conservar el MVP).
