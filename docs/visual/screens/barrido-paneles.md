# Barrido de paneles MVP — aplicación real (aprobada)

**Fecha:** 2026-11-10 · **Estado:** ✅ **aprobada por el propietario** sobre el prototipo `pmb-ficha-prototipo.md` («Aprobado — aplicar a todos los paneles»).
**Alcance:** Ficha clásica, Chat, Combate, Notas del GM e Historial — el mismo lenguaje visual del Character Hub (títulos de sección en versalitas, listas en filas limpias, atributos en tarjeta, cabecera con retrato).

## 1. Qué se aplicó

- **`apps/web/src/views/panels.css`** (nuevo): lenguaje del Hub sobre las **clases existentes** de los 5 paneles (`scene-sheet`, `sheet-card`, `ability`, `item-list`, `roll-list`, `badge`, `sheet-form`) — sin cambiar selectores ni lógica, así los E2E y pruebas web siguen intactos.
- **Ficha (`ActorPanel.tsx`)**: cabecera con **retrato** (inicial), nombre (con elipsis en largos), identidad «Ficha · dnd5e» y **«Editar ficha» como acción principal** de la cabecera (antes suelto en el cuerpo). Modo edición con campos numéricos en las tarjetas de atributos y PG, igual que el prototipo.
- Import de `panels.css` en `ActorPanel`, `ChatPanel`, `CombatPanel`, `NotesPanel` y `HistoryPanel`.

## 2. Capturas reales (Mesa/espacio con datos de la campaña)

| Captura | Contenido |
|---|---|
| `barrido-ficha-2026-11-10.jpeg` | Ficha rediseñada con Elenar: cabecera retrato + PG 9/9 + tarjetas de atributos |
| `barrido-combate-2026-11-10.jpeg` | Combate: estado vacío honesto («No hay combate en curso») + acción |
| `barrido-chat-2026-11-10.jpeg` | Chat: selector de conversación + estado vacío + envío |
| `barrido-hub-mesa-2026-11-10.jpeg` | **Character Hub real en la mesa** (pendiente desde PMB-3.7) con Elenar: Perfil/Equipo/Relaciones/Historia |
| `barrido-notas-2026-11-10.jpeg` | Notas del GM: alta de nota + estado vacío |
| `barrido-historial-2026-11-10.jpeg` | Historial: cronología real de la campaña |

## 3. Verificación

- Suite web **146/146** (34 archivos) y `npm run build` limpio tras los cambios.
- **E2E 2/2** (vertical slice) tras el rediseño — ningún selector afectado.
- Modificadores verificados por DOM en Ficha y Hub (ambos «+0» en Inteligencia 10; la «−0» de la captura era artefacto JPEG).
- Función conservada al 100 %: creación, selección, edición (nombre/atributos/PG), inventario (alta/edición/borrado), efectos, tiradas, notas e historial.
