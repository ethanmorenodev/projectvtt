# ProjectVTT — Tokens de diseño (DESIGN_TOKENS.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación del propietario.  
**Uso:** fuente única de verdad del sistema visual. Los valores se implementarán como variables CSS (y constantes de tema) en la Fase 1; hasta entonces son la especificación.  
**Unidad base:** 4 px. Todo espaciado, tamaño y alto de control es múltiplo de la retícula.

---

## 1. Color — superficies (carbón, sin negro puro)

| Token | Valor | Uso |
|---|---|---|
| `surface-0` | `#16171A` | Fondo de aplicación (carbón profundo, nunca `#000`) |
| `surface-1` | `#1B1D21` | Paneles y barras |
| `surface-2` | `#212429` | Tarjetas, listas elevadas, ventanas |
| `surface-3` | `#282C32` | Inputs, hover, filas alternas |
| `surface-4` | `#31363E` | Estados activos/press, dropdowns |
| `border-subtle` | `#33383F` | Separadores y bordes de panel |
| `border-strong` | `#454B55` | Bordes de ventana, inputs, foco secundario |
| `overlay` | `rgba(8, 9, 11, 0.62)` | Fondos modales |

## 2. Color — texto

| Token | Valor | Uso |
|---|---|---|
| `text-primary` | `#EDECF2` | Texto principal |
| `text-secondary` | `#B9B5C4` | Texto de apoyo, metadatos |
| `text-muted` | `#A39FAD` | Placeholders, hints, deshabilitado (AA ≥4,7:1 en todas las superficies; antes `#8B8797` llegaba a 3,48:1) |
| `text-inverse` | `#16171A` | Texto sobre acento claro |

## 3. Color — acento (morado apagado)

| Token | Valor | Uso |
|---|---|---|
| `accent` | `#6259A3` | Acciones primarias, foco, selección (AA: 5,43:1 con `accent-on`; antes `#7E72C9` daba 3,67:1) |
| `accent-hover` | `#6D63AB` | Hover (AA: 4,69:1 con `accent-on`) |
| `accent-active` | `#564D8F` | Press/activo (AA: 6,61:1 con `accent-on`) |
| `accent-soft` | `#2A2740` | Fondos de selección, chips, badges |
| `accent-on` | `#F4F1FF` | Texto sobre acento pleno |
| `accent-border` | `#4A4470` | Bordes de elementos seleccionados |

Regla: el acento **marca y orienta** (primarios, foco, selección, estados activos); jamás rellena grandes superficies ni decora.

## 4. Color — semántico

| Significado | Foreground | Background | Uso |
|---|---|---|---|
| Éxito | `#7FBF8E` | `#223026` | Confirmaciones, estados activos positivos |
| Advertencia | `#D6A75C` | `#332A1B` | Avisos, "actualización disponible" |
| Peligro | `#D27272` | `#332222` | Acciones destructivas, errores |
| Información | `#79A9D6` | `#1F2833` | Notas informativas, estados de conexión |

## 5. Tipografía

- **Familia UI:** `Inter, "Segoe UI", system-ui, sans-serif`.  
- **Familia mono (códigos, dados, fórmulas):** `"JetBrains Mono", ui-monospace, monospace`.  
- ProjectVTT **no impone tipografía temática** a los universos del usuario (el contenido puede usar su estilo en textos de lore si se permite riqueza tipográfica futura).

| Token | Tamaño / interlínea | Uso |
|---|---|---|
| `text-xs` | 11 px / 1.35 | Etiquetas, chips, leyendas |
| `text-sm` | 12 px / 1.4 | Metadatos, tablas densas |
| `text-base` | 13 px / 1.45 | UI por defecto (densidad compacta) |
| `text-body` | 15 px / 1.6 | Prosa (descripciones, lore) |
| `text-h3` | 18 px / 1.3 | Títulos de panel/sección |
| `text-h2` | 22 px / 1.25 | Títulos de pantalla |
| `text-h1` | 28 px / 1.2 | Títulos de dashboard/hero contenido |

Pesos: 400 (texto), 500 (UI), 600 (títulos y acciones). Versalitas de sección: 11 px, 600, `letter-spacing: 0.06em`.

## 6. Espaciado

`2 · 4 · 6 · 8 · 12 · 16 · 20 · 24 · 32 · 48` px.  
Padrón: 8 px entre controles relacionados, 16 px entre grupos, 24 px entre secciones, 32–48 px de aire en arquetipos dashboard.

## 7. Forma y elevación

| Token | Valor | Uso |
|---|---|---|
| `radius-xs` | 4 px | Inputs, chips, miniaturas pequeñas |
| `radius-sm` | 6 px | Botones, tarjetas, miniaturas |
| `radius-md` | 8 px | Paneles, ventanas, dropdowns |
| `radius-lg` | 12 px | Modales, overlays grandes |
| `radius-pill` | 999 px | Badges, switches |

Elevación (tema oscuro: bordes + sombras sutiles, nunca "tarjetas flotantes" por doquier):

- `shadow-1`: `0 1px 2px rgba(0,0,0,0.35)` — menús, tooltips.
- `shadow-2`: `0 4px 12px rgba(0,0,0,0.40)` — dropdowns, paneles flotantes.
- `shadow-window`: `0 12px 32px rgba(0,0,0,0.50)` — ventanas del sistema de ventanas.

## 8. Controles y densidad

- Densidad **compacta por defecto**: alto de control 28 px (32 px en modo cómodo para formularios largos), filas de tabla 32 px, iconos 16 px (20 px en títulos).
- Bordes 1 px (`border-subtle` en interiores, `border-strong` en contenedores).
- Foco: anillo `2px solid accent` con `offset 2px`.

## 9. Movimiento

- Duraciones: `fast 120 ms` (hover/press), `base 160 ms` (aperturas), `slow 240 ms` (ventanas/paneles).
- Curva: `cubic-bezier(0.2, 0, 0, 1)` (salida suave).
- Solo `opacity` y `transform`. Nada de animaciones decorativas constantes.
- `prefers-reduced-motion: reduce` → duraciones a 0 ms.

## 10. Capas (z-index)

| Capa | Valor |
|---|---|
| Tablero / canvas | 0 |
| HUD del tablero | 100 |
| Barra de herramientas / docks | 200 |
| Ventanas (gestionadas) | 300–500 (asigna el gestor) |
| Modales | 600 |
| Toasts | 700 |
| Paleta de comandos | 800 |
| Tooltips | 900 |

## 11. Miniaturas de contenido (regla visual fundamental)

- **Proporción 1:1 (cuadradas) y compactas** para toda representación habitual de contenido (razas, clases, trasfondos, habilidades, objetos, armas, armaduras, criaturas, NPC, imágenes de biblioteca).
- Tamaños (**decisión del propietario, 2026-11-09**): `thumb-xs 48 px` (listas densas), `thumb-sm 64 px` (vistas amplias y elementos destacados), `thumb-md 128 px` (rejillas), `thumb-lg 256 px` (previsualización en inspector). Ampliación a tamaño real solo bajo petición del usuario.
- **Sin portada/imagen → imagen alternativa determinista**: cuadrado `accent-soft` con la inicial del nombre (nunca un rectángulo gris sin más; ver COMPONENT_RULES §11).
- Recorte: `object-fit: cover`; encuadre por defecto centro-arriba (retratos), ajustable por imagen (punto focal guardado).
- Placeholder mientras carga: `surface-3` + icono neutro; carga diferida (`lazy`) en rejillas.
- Radio: `radius-sm` (6 px). Borde: `border-subtle` (el marco es neutro; el estilo artístico es del usuario).
- Almacenamiento y derivados: `docs/architecture/CONTENT_MODEL.md` §Imágenes (y ADR-035).

## 12. Puntos de ruptura (desktop-first)

| Token | Ancho | Uso |
|---|---|---|
| `bp-sm` | 960 px | Mínimo tolerable (navegación colapsada) |
| `bp-md` | 1280 px | **Mínimo soportado** para la mesa de juego |
| `bp-lg` | 1600 px | Referencia de trabajo (mesa + ventanas) |
| `bp-xl` | 1920 px | Diseño de referencia para la mesa |

La mesa se diseña para ≥1440 px de ancho útil; por debajo de 1280 px se muestra aviso de resolución recomendada.
