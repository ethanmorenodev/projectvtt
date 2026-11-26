# PMB-2.8 — Prototipo de la Mesa de juego

**Fecha:** 2026-11-10 · **Estado:** prototipo implementado — **pendiente de aprobación visual del propietario** (capturas bloqueadas temporalmente por el capturador: requiere la ventana de OpenCode visible).  
**Alcance:** distribución flexible del plan §7.1 con las herramientas vivas en el **sistema de ventanas real (PMB-1.5)**. Rutas dev: `#prototipo-mesa[-vacio|-extremo]`, montado en el shell real. Datos estáticos; no toca la mesa real del MVP.

## 1. Análisis de función

La mesa debe dar **protagonismo al tablero** y mantener las herramientas al alcance sin tapar el juego. Composición propuesta (§7.1, con las cuatro zonas):

- **Raíl izquierdo (232 px):** Escenas (lista con activa), Capas (incluida «Secreta (GM)»), Herramientas (seleccionar, dibujar, medir, niebla, anotar, tokens) y acceso a la Biblioteca.
- **Central:** tablero a sangre con cuadrícula de 48 px, bloque de mapa y **tokens como chips** clicables (nombre + color) — clic = selección.
- **Derecha (280 px):** **Inspector contextual**: propiedades del token seleccionado (nombre, badge «Con actor/Sin actor», posición+grid, versión/autor) y acciones (vincular, eliminar); sin selección → estado vacío «Nada seleccionado».
- **Inferior:** herramientas frecuentes (Añadir token, Ajustar al grid, Editar niebla, Importar mapa, celda/forma, Guardar) + **dock de herramientas vivas** (Chat/Combate/Ficha) que abren **ventanas reales del sistema PMB-1.5** flotando sobre la mesa (arrastrables, minimizables, z-order).

## 2. Decisiones de composición a revisar

- Las herramientas de panel (chat/combate/ficha) NO son asides fijos: usan el sistema de ventanas aprobado (coherente con PMB-1.6).
- El raíl izquierdo agrupa navegación + herramientas; el inspector es contextual (estable, siempre presente).
- Tokens como chips de color con nombre corto; la forma final (miniatura vs chip) se decidirá con la integración real.
- Variante **vacio**: escena sin tokens + inspector vacío; variante **extremo**: 14 tokens con nombre largo (valorar densidad y solapes).

## 3. Verificación

3 pruebas nuevas (zonas de la distribución, selección→inspector, variantes vacía/extrema) — suite web **126/126** · `npm run build` limpio. Capturas 2026-11-10 archivadas: `pmb-2.8-mesa-{normal,vacio,extremo}-2026-11-10.jpeg`.

**Bug corregido al capturar (2026-11-10):** el host del sistema de ventanas (`.pv-window-host`) pintaba un fondo `--pv-surface-0` que tapaba todo el tablero (mapa y tokens invisibles). Afectaba tambien a la vista de escena real del MVP (mismo patron en `.tools-host`, PMB-1.6). Correccion: el host de ventanas es una capa flotante **transparente** (`src/windows/windows.css`); cada area pinta su propio fondo. Las pruebas funcionales pasaban con el tablero completamente negro: buena muestra del valor del proceso visual.

## 4. Decisión pendiente

**Aprobación visual** de la distribución (zonas, raíl, inspector, dock) junto con el prototipo del Espacio de campaña (PMB-2.6). Tras la aprobación se integra la mesa real (PMB-2.8b) conservando el E2E (mapa, tokens, movimiento en vivo intactos).

**→ Aprobado por el propietario (2026-11-10) e integrado el mismo día (PMB-2.8b):** `SceneView` redistribuido al §7.1 sin tocar su lógica (raíl con escenas/capas reservadas/herramientas/biblioteca, tablero a sangre, inspector contextual, barra inferior + contador de ventanas). Las herramientas quedan **cerradas por defecto** (composición aprobada: tablero protagonista, «0 ventana(s)» hasta abrirlas): además evita que las ventanas cubran el centro del tablero donde nacen los tokens. E2E en verde. Capturas de la integración archivadas: `pmb-2.8b-mesa-real-2026-11-10.jpeg` y `pmb-2.8b-mesa-real-seleccion-2026-11-10.jpeg` (esta última con el inspector contextual activo sobre el token «Elenar»).
