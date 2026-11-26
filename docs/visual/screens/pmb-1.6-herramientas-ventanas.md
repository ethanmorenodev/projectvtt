# PMB-1.6 — Herramientas del MVP en el sistema de ventanas

**Fecha:** 2026-11-09 · **Estado:** implementado — capturas para revisión (el comportamiento visual ya pasó por el proceso: PMB-1.3/1.5).  
**Alcance:** chat, combate, ficha, notas e historial dejan de ser asides/paneles fijos y se gestionan como ventanas del sistema (ADR-033). **Su lógica, props y stores no cambian** (los componentes `ChatPanel`, `CombatPanel`, `ActorPanel`, `NotesPanel`, `HistoryPanel` están intactos); solo cambia el contenedor.

## Capturas (app real)

1. **`pmb-1.6-mesa-herramientas-ventanas.jpeg`** — mesa de juego con Ficha, Combate y Chat como ventanas flotantes (arrastre, minimizar/cerrar, redimensionado, z-order), el tablero a pantalla completa y el lanzador de herramientas en el HUD («Ficha Combate Chat»). El contenido real de los paneles se ve dentro de cada ventana (chat con conversación, mensaje y «Enviar»).
2. **`pmb-1.6-campana-notas-historial.jpeg`** — espacio de campaña con «Notas del GM» e «Historial» como ventanas (el historial muestra eventos reales de la campaña), dentro del área de herramientas de la pantalla de gestión.

## Comportamiento

- **Primera visita**: las herramientas se abren dispuestas en cascada; después, **el layout del usuario manda** (persistencia por usuario y contexto — mesa y campaña por separado —, PMB-1.5).
- El lanzador del HUD abre/focaliza cada herramienta (estado «abierta» resaltado); las minimizadas van a la bandeja.
- Un usuario sin permisos de GM no ve «Notas del GM» (misma regla que antes).

## Verificación

- **E2E del vertical slice en verde** con las herramientas como ventanas (criterio de la tarea).
- Suite web 112/112 + `npm run build` sin errores (los paneles no se tocaron; sin pruebas nuevas: su lógica ya está cubierta).
- Comprobado en la app real con datos: creación de campaña y escena, ventanas con contenido, historial con eventos.
