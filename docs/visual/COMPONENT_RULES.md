# ProjectVTT — Reglas de componentes (COMPONENT_RULES.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación del propietario.  
**Uso:** cómo se comporta y se compone cada componente de la biblioteca visual compartida (Fase 1). Los valores vienen de `DESIGN_TOKENS.md`; la dirección, de `PROJECT_VISUAL_GUIDELINES.md`.

---

## 1. Principios de composición

1. **Una acción primaria por superficie** (panel, ventana, tarjeta): el resto son secundarias o fantasma.
2. **Densidad compacta, aire donde importa**: tablas y listas densas; formularios y dashboards con respiración.
3. **El contenido manda**: los marcos son neutros; las miniaturas del usuario aportan el color.
4. **Todo estado es visible**: hover, foco, activo, deshabilitado, cargando, vacío, error.
5. **Consistencia antes que originalidad**: cada patrón se define una vez aquí y se reutiliza.

## 2. Botones

| Variante | Uso | Aspecto |
|---|---|---|
| **Primario** | La acción principal de la superficie (máx. 1 visible) | Fondo `accent`, texto `accent-on` |
| **Secundario** | Acciones frecuentes alternativas | Fondo `surface-3`, borde `border-strong` |
| **Fantasma** | Acciones terciarias, barras densas | Transparente, texto `text-secondary` |
| **Peligro** | Acciones destructivas | Borde/texto `danger`; fondo `danger` solo en la confirmación final |
| **Icono** | Herramientas del tablero y ventanas | 28×28 px, tooltip obligatorio y `aria-label` |

Reglas: jerarquía visual clara (el primario destaca sin gritar); etiquetas en verbo ("Crear campaña", no "Campañas"); estado de carga con spinner y ancho fijo; deshabilitado con motivo (tooltip) cuando no es obvio.

## 3. Formularios

- Etiqueta **encima** del campo (12 px, 500); ayuda debajo en `text-xs` (`text-muted`); error debajo con icono + texto `danger`.
- Campos: alto 28 px (32 px en formularios largos del creador de personajes), fondo `surface-3`, foco con anillo de acento.
- Agrupación por secciones con encabezado (`text-h3` o versalitas); dos columnas solo para campos cortos y relacionados.
- Validación en línea al salir del campo y al enviar; el primer error recibe foco.
- Acciones del formulario abajo a la derecha: **Cancelar** (fantasma) + **Guardar/Crear** (primario). Los formularios largos guardan borrador (p. ej. creador de personajes).

## 4. Listas y tablas

- **Fila fija de dos líneas** (decisión del propietario 2026-11-09): **ninguna fila crece por envoltura de texto**. Línea 1: nombre (una línea, elipsis + tooltip) y badge de estado junto al nombre. Línea 2: subtítulo/descripción (elipsis) y metadatos en **una sola línea que nunca envuelve**, con la columna temporal («hace X») a la derecha con ancho fijo. Filas de 64 px (48 px de miniatura + padding).
- **Orden de sacrificio documentado** cuando falta ancho (container queries en `ui.css`): ① se trunca la descripción (flex) → ② se ocultan «participantes» (`--drop-2`, ≤760 px) → ③ se oculta la actividad/tiempo (`pv-row__meta-time`, ≤640 px) → ④ se trunca el rol (`--drop-4`, ≤540 px). **Nombre y estado nunca se ocultan.**
- **Metadatos temporales normalizados** a «hace X» y alineados en columna fija a la derecha (nunca en línea propia salvo filas de dos líneas sin subtítulo, donde acompañan al título).
- Hover `surface-3`; selección `accent-soft` + borde izquierdo `accent`.
- Acciones por fila: **icono-flecha discreto, siempre visible** (área 28 px, tooltip y `aria-label` con el nombre del elemento); texto solo en elementos destacados (≤3–5). Nunca solo al hover.
- Virtualizar cuando la lista supere ~200 elementos (biblioteca, mensajes).

## 5. Tarjetas compactas (campañas, contenido)

- Formato: **miniatura cuadrada** (`thumb-md`) + título (1 línea, elipsis) + fila de metadatos (rol/estado/participantes/última actividad) + acción rápida contextual.
- **Nunca tarjetas gigantes**: el bloque ocupa lo mínimo para ser escaneable; la rejilla densa gana a las galerías enormes.
- Hover: elevación `shadow-1` + borde `accent-border`; la miniatura no se anima.

## 6. Paneles (herramientas acoplables y secciones)

- Cabecera de 32 px: título + icono de colapsar + acciones. Cuerpo con padding 12–16 px.
- Los paneles colapsan conservando su ancho; el estado persiste.
- Los módulos de la mesa (chat, combate, ficha, notas, historial) **viven en el sistema de ventanas** (§7); la variante "acoplada" es un caso del mismo componente, no un sistema aparte.

## 7. Ventanas (sistema único — `docs/architecture/WINDOWS.md`)

- Cabecera arrastrable (32 px): icono + título + acciones (minimizar, cerrar). Redimensionable por bordes/esquinas (área de 6 px).
- Foco con elevación `shadow-window` y z-order gestionado; primera interacción enfoca.
- Posición y tamaño recordados por usuario y contexto; distribuciones restaurables.
- Ventanas hermanas (inspector de objeto, ficha de NPC) se abren desde el contenido con un solo clic; el tablero sigue accesible detrás.

## 8. Inspectores (panel derecho/contextual)

- Patrón clave-valor en secciones plegables; edición in situ (sin botón "editar" salvo cambios estructurales).
- Muestra el estado del elemento seleccionado; acciones al pie según permisos.
- Los secretos del DM solo aparecen para el DM (filtrado en servidor, nunca solo en UI).

## 9. Feedback y estados

| Elemento | Regla |
|---|---|
| **Toasts** | Abajo-derecha, 4 s (errores persistentes), máximo 3 apilados; acciones de deshacer cuando aplique |
| **Modales** | Solo para confirmaciones destructivas o decisiones que bloquean; la confirmación peligrosa exige escribir el nombre cuando es irreversible |
| **Estados vacíos** | Icono neutro + título + una frase + acción primaria (p. ej. «Todavía no hay escenas — Crea la primera»). **Excepción (decisión 2026-11-09):** si la superficie ya expone la misma acción (p. ej. «Crear campaña» en la cabecera), el estado vacío **no la repite** y su texto guía hacia ella. Nunca una lista vacía muda |
| **Skeletons** | Forma del contenido real; parpadeo mínimo; nunca spinners de pantalla completa salvo carga inicial |
| **Errores** | Mensaje del servidor traducido a lenguaje humano + acción sugerida; nunca trazas |
| **Sesión caducada** | Un solo manejo global: aviso + redirección al login conservando la ruta de vuelta |

## 10. Búsqueda y navegación por teclado

- **Paleta de comandos** (Ctrl/Cmd+K): búsqueda global (campañas, escenas, personajes, contenido, acciones) con resultados agrupados y atajos.
- Atajos documentados y consistentes: `Ctrl+K` buscar, `Esc` cerrar/cancelar, `Supr` borrar (con confirmación), flechas en listas.
- Toda acción de la UI tiene camino de teclado; el foco nunca queda atrapado.

## 11. Miniaturas de contenido

- Siempre cuadradas y compactas (`DESIGN_TOKENS.md` §11): la imagen ayuda a **reconocer** el elemento, no a dominar la interfaz.
- Click en la miniatura → selecciona/inspecciona; **doble formato de ampliación** solo bajo petición (lightbox).
- Sin imagen: placeholder con icono por tipo de contenido (raza, objeto, criatura…), nunca un rectángulo gris sin más.

## 12. Accesibilidad (obligatoria en componentes)

Contraste ≥4.5:1 (3:1 en componentes); foco visible en todos los controles; roles ARIA correctos en listas/tabs/ventanas; `aria-live` en toasts y estados de conexión; movimiento reducido respetado; objetivos ≥28 px.

## 13. Qué NO se hace

- Botones sin jerarquía (todo primario o todo igual).
- Imágenes grandes como representación habitual del contenido.
- Modales para confirmaciones triviales.
- Listas vacías sin estado de guía.
- Patrones "originales" por pantalla: si un patrón no está aquí, se define aquí antes de usarse.

## 14. Badges y chips (promovidos el 2026-11-09)

**`Badge`** (`src/ui/Badge.tsx`) — estado/etiqueta compacto junto al nombre:
- Tons: `accent` (activo/en curso), `warning` (en pausa), `neutral` (archivado/informativo), `success`, `danger`.
- Siempre en la **línea 1** de la fila, pegado al nombre; nunca compite con el texto principal ni se usa como botón.
- Máximo uno por fila; texto corto (1–2 palabras).

**`FilterChip`** (`src/ui/FilterChip.tsx`) — filtro conmutador en barras de herramientas:
- Estados `selected`/normal; expone `aria-pressed`; el grupo se anuncia con un `role="group"` etiquetado.
- Uso para facetas cerradas (Todas/Propias/Compartidas/Archivadas); para filtros abiertos usar menús desplegables.
- No usar chips como botones de acción (para acciones: `Button`/`IconButton`).
