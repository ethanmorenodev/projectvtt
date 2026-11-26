# ProjectVTT — Sistema de ventanas (WINDOWS.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación. Decide junto al ADR-033.  
**Requisito (plan §3):** un **único** sistema compartido para todas las herramientas (Character Hub, fichas de objeto, biblioteca, inspector, chat, combate, editor de contenido…), que permita incorporar herramientas nuevas **sin rehacer el sistema**, y que mantenga la mesa de juego accesible mientras se consulta o edita contenido.

---

## 1. Qué es

Un **gestor de ventanas en la aplicación** (React), presente en los arquetipos *workspace* (mesa) y *editor*. Cada herramienta es un **descriptor** registrado; el gestor la dibuja, posiciona, redimensiona, enfoca, minimiza y persiste.

Es infraestructura de la **Fase 1** (arquitectura de experiencia): todo lo que viene después (Biblioteca, Character Hub, inspectores) se construye como herramienta de este sistema.

## 2. Capacidades (requisitos del plan §3)

- Abrir **varias herramientas simultáneamente** sobre el tablero o el área de trabajo.
- **Arrastrar** (por la cabecera), **redimensionar** (bordes/esquinas), **minimizar** (bandeja con etiquetas) y **cerrar**.
- **Recordar posición y tamaño** por usuario y contexto (mesa/biblioteca/editor); distribuciones **guardables/restaurables** (p. ej. «Mesa de combate», «Preparación»).
- **Gestión de capas y orden**: z-order gestionado por foco (click enfoca y eleva), sin solapamientos caóticos (orden estable al restaurar).
- **Interacción entre herramientas**: eventos de aplicación (selección, apertura, cambios) que las ventanas pueden observar (§5).
- **La mesa sigue accesible**: el tablero ocupa todo el fondo; las ventanas flotan sobre él; ninguna herramienta "secuestra" la pantalla.

## 3. Arquitectura

```
WindowManager (contexto React, único por área)
├── WindowHost            // área de trabajo: dibuja ventanas, gestiona z-order/foco
├── Tray (minimizadas)    // bandeja inferior con iconos+etiquetas
└── ToolRegistry          // registro de herramientas disponibles
```

**ToolDescriptor** (el contrato de extensión — añadir herramientas no toca el gestor):

```
ToolDescriptor {
  id, title, icon,
  context: "table" | "campaign" | "library" | "any",
  minSize, defaultSize,          // px, alineados a la retícula de 4
  singleton | multiInstance,     // p. ej. un Hub por personaje (multi)
  component,                     // contenido React de la herramienta
  openWith?                      // parámetros de apertura (p. ej. actorId, itemId)
}
```

**Persistencia:** `layout = { [context]: [{toolId, x, y, w, h, z, minimized}] }` por usuario. Local (`localStorage`) en la primera entrega; persistencia en servidor cuando exista la Gestión de cuenta (sin cambiar la API del gestor).

**Interacción entre herramientas (§5):** bus de eventos tipado de aplicación (`selection:changed`, `content:open`, `character:updated`…). Ej.: el explorador de Biblioteca emite `selection:changed{itemId}` → el inspector se actualiza; `content:open{itemId}` → abre/trae al frente la ficha; el Character Hub escucha `character:updated` para refrescar. El bus **no** duplica la sincronización en vivo del MVP (esa sigue por SignalR/estado): es coordinación de UI.

## 4. Comportamiento definido

| Aspecto | Regla |
|---|---|
| Arrastre | Por cabecera (32 px); anclado a retícula de 4 px; sin salir del área útil |
| Redimensionado | Bordes/esquinas (6 px de área); respetando `minSize`; proporción libre salvo herramientas que la fijen (p. ej. Hub) |
| Foco | Click enfoca y eleva; atajo para circular entre ventanas; la ventana activa lleva borde de acento |
| Minimizar | A la bandeja con etiqueta; restaurar devuelve a su sitio |
| Cerrar | Confirma solo si hay cambios sin guardar de la herramienta |
| Persistencia | Al cerrar sesión/área; restauración al volver; «Restablecer distribución» siempre disponible |
| Multi-instancia | Herramientas marcadas `multiInstance` abren una ventana por elemento (p. ej. fichas de varios personajes) |

## 5. Relación con las pantallas

- **Mesa de juego:** el tablero a sangre completa + ventana de herramientas (chat, combate, notas, historial, ficha/Hub, biblioteca). El HUD discreto (barra superior de la mesa) contiene la apertura de herramientas y el comando de distribución.
- **Espacio de campaña / Biblioteca:** las herramientas de edición (editor universal, inspector de objeto) usan el mismo gestor dentro de su área.
- Las herramientas del MVP (chat, combate, ficha, notas, historial) se **adaptan** a este sistema (conservando su lógica y pruebas): dejan de ser asides fijos para ser herramientas registradas. Es la única "reescritura" de UI del MVP y está justificada por el requisito §3 (un solo sistema) — la lógica y los stores no cambian.

## 6. Por qué no una librería de docking (decisión ADR-033)

Se evaluó una librería de docks (p. ej. dockview/react-mosaic): sus layouts acoplables avanzados no son necesarios hoy, añaden peso y su modelo de layout condicionaría la persistencia y la integración con herramientas de juego. Un gestor propio y pequeño cubre los requisitos (flotantes, minimizar, persistir, registrar herramientas) y deja la puerta abierta a acoplamiento futuro. Si los requisitos crecieran hacia docks complejos, la decisión se revisa.
