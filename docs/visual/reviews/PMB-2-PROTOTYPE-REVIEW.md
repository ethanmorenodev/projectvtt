# Revisión visual autónoma — Prototipos PMB-2.1/2.3 (Fase 2)

**Estado:** AUTOREVISIÓN VISUAL (IA) — **el diseño NO está aprobado**. Revisión preparada para revisión humana; las decisiones finales quedan identificadas al final.  
**Fecha:** 2026-11-09 · **Revisor:** agente (MiMo) · **Motivo:** revisión autónoma al no poder compartir las capturas externamente.  
**Alcance:** los seis escenarios capturados en `docs/visual/screens/pmb-2.*.jpeg` (commit `946852b`, que contiene el código que las produce).  
**Restricciones respetadas:** no se declara aprobación, no se integran PMB-2.2/2.4, no se modifica el MVP, los problemas se enumeran con severidad sin suavizar.

> **REVISIÓN 2 (2026-11-09, tras correcciones aprobadas): ver §10.** Las capturas del repositorio son ya las de la revisión 2; los §§1–9 describen el estado previo a las correcciones y se conservan como histórico.

---

## 0. Verificación previa (punto 6 de la petición)

| Comprobación | Resultado |
|---|---|
| Las 6 capturas existen en el repositorio | ✅ `docs/visual/screens/pmb-2.1-inicio-{normal,vacio,extremo}.jpeg` y `pmb-2.3-campanas-{normal,vacio,extremo}.jpeg` (71–124 KB) |
| Las capturas corresponden al código actual | ✅ árbol limpio; código y capturas se commitearon juntos (`946852b`) y no ha habido cambios posteriores |
| Suite web | ✅ **115/115** (25 ficheros) |
| Build (`tsc --noEmit` + `vite build`) | ✅ sin errores |
| Funcionalidad del MVP | ✅ intacta (la revisión solo añade este documento) |

**Leyenda de severidad:** **ALTA** (rompe el objetivo de la pantalla) · **MEDIA** (deteriora la experiencia de forma visible) · **BAJA** (pulido o coherencia menor).

---

## 1. Inicio normal (`pmb-2.1-inicio-normal.jpeg`)

**Jerarquía visual.** Correcta en lo esencial: título + una acción primaria («Crear campaña») claramente dominante arriba-derecha, bloques con etiquetas en versalitas apagadas y el contenido destacado (3 tarjetas) ocupando el ancho útil. La zona de mayor peso visual son las miniaturas de las tarjetas, que es donde debe estar.

**Legibilidad y densidad.** Buena en tarjetas y columnas; floja en la lista de actividad: cada evento ocupa dos líneas porque la marca de tiempo se coloca **debajo** del texto («hace 12 min»), lo que alarga la lista y rompe el ritmo de lectura vertical.

**Espaciado y alineación.** Separación entre bloques consistente (24 px) y columnas alineadas por arriba. Problemas: (a) los títulos de las tarjetas 2 y 3 se truncan pronto («Crónicas del Mar I…», «One-shot: la mina …») porque la acción «Continuar →» compite por el ancho dentro de la cabecera de la tarjeta; (b) la fila de invitación envuelve su segundo texto («jugador» salta de línea) y el botón «Aceptar» sólido la desequilibra.

**Consistencia de componentes.** Botones, listas y tarjetas son de la biblioteca (`src/ui`). Dos elementos **no** lo son: los filtros/estados usan clases locales del prototipo (`proto-chip`, `f2-state`) y «Ver todas» se pinta como enlace mientras el resto de acciones son `Button ghost` — inconsistencia menor pero real.

**Problemas detectados.**

| # | Problema | Severidad |
|---|---|---|
| 1.1 | Las marcas de tiempo de «Actividad reciente» ocupan una segunda línea por evento (5 eventos = 10 líneas) | MEDIA |
| 1.2 | Truncado temprano de títulos en tarjetas: la acción de la tarjeta resta ancho al nombre | MEDIA-BAJA |
| 1.3 | «Ver todas» con aspecto de enlace frente al resto de acciones (botón ghost) | BAJA |
| 1.4 | Redacción de metadatos inconsistente entre tarjetas («última sesión hace 2 h» vs «hace 3 días») — problema de datos de muestra que evidencia la falta de una regla de redacción | BAJA |
| 1.5 | Fila de invitación con texto envuelto y acción primaria sólida de gran peso en fila compacta | BAJA |

**Mejoras propuestas.** Marca de tiempo alineada a la derecha en la misma línea del evento (ver §7.1); reservar el ancho completo del título a la tarjeta y pasar la acción a la línea de metadatos o mostrarla al enfocar (sin depender solo del hover); «Ver todas» como `Button ghost` con icono; fijar regla de redacción de actividad («hace X» siempre); «Aceptar» primario pero de tamaño compacto.

---

## 2. Campañas normal (`pmb-2.3-campanas-normal.jpeg`)

**Jerarquía visual.** Buena: nombre como elemento dominante por fila, descripción secundaria apagada, estado/rol/participantes/actividad como datos, acción alineada a la derecha. La búsqueda y los filtros quedan como herramienta, no compiten.

**Legibilidad y densidad.** Correcta con 5 elementos; las miniaturas de 48 px cumplen su función de reconocimiento. **Pero la altura de las filas no es constante**: la fila 2 («Crónicas del Mar Interior») envuelve sus metadatos a una segunda línea y las demás no.

**Espaciado y alineación.** Padding horizontal y separación vertical consistentes. Problema derivado del envolte: el **badge de estado oscila verticalmente** entre filas (unas veces en la línea 1, otras en la 2), rompiendo la columna visual del estado. Las acciones quedan alineadas a la derecha ✔.

**Consistencia de componentes.** `List`/`ListRow`/`Button` de la biblioteca ✔; badges de estado y chips de filtro son estilos locales del prototipo (pendientes de promover a componentes `Badge` y `FilterChip`); la miniatura-alternativa con inicial es nueva y correcta, pero su letra grande puede confundirse con contenido (ver 4.3).

**Problemas detectados.**

| # | Problema | Severidad |
|---|---|---|
| 2.1 | Altura de fila no constante: metadatos que envuelven a dos líneas en unas filas y no en otras | **ALTA** (es el objetivo §5: localizar rápido entre muchas) |
| 2.2 | El badge de estado salta de posición vertical entre filas (columna visual rota) | MEDIA |
| 2.3 | Acción «Continuar →» con texto repetido en todas las filas: ruido a la derecha que compite con los metadatos | MEDIA |
| 2.4 | Las iniciales de la alternativa sin portada («C», «E») compiten en peso con las miniaturas con imagen | BAJA |

**Mejoras propuestas.** Regla de fila de **dos líneas fijas** (ver §7.1) con el badge junto al nombre; acción de lista densa como icono (ver §7.2); letra de la alternativa un punto más apagada o con peso menor.

---

## 3. Inicio vacío (`pmb-2.1-inicio-vacio.jpeg`)

**Jerarquía visual.** Los estados vacíos guían bien (título + frase + CTA primaria en «Aún no hay campañas»). La jerarquía global se mantiene.

**Legibilidad y densidad.** Los tres estados vacíos con su padding generoso ocupan bastante vertical; aceptable en un estado excepcional, aunque «Actividad reciente» e «Invitaciones» con dos EmptyStates centrados generan un bloque muy vacío a la derecha.

**Espaciado y alineación.** Centrado consistente; separaciones entre bloques respetadas.

**Consistencia.** Estados vacíos con el componente de la biblioteca ✔; CTA duplicado (arriba-derecha y en el estado vacío) — es coherente con la regla «una acción primaria por superficie» **si** se interpreta el estado vacío como superficie propia; conviene decidirlo explícitamente.

**Problemas detectados.**

| # | Problema | Severidad |
|---|---|---|
| 3.1 | «Ver todas» sigue visible en «Continuar jugando» cuando **no hay nada que ver** (inconsistencia de estado) | **MEDIA** |
| 3.2 | CTA «Crear campaña» duplicado en la misma vista | BAJA (aceptable si se fija la regla) |

**Mejoras propuestas.** Ocultar (o deshabilitar con explicación) «Ver todas» cuando la colección está vacía; considerar un estado vacío más compacto para las columnas secundarias (sin CTA cuando el bloque no es la acción principal).

---

## 4. Campañas vacío (`pmb-2.3-campanas-vacio.jpeg`)

**Jerarquía visual.** Correcta: la barra de búsqueda/filtros como herramienta y el estado vacío como contenido. La acción primaria queda clara.

**Legibilidad y densidad.** Buena; el estado vacío es el único contenido y se lee de inmediato.

**Espaciado y alineación.** Correctos.

**Consistencia.** Mismo patrón de estado vacío que Inicio ✔.

**Problemas detectados.**

| # | Problema | Severidad |
|---|---|---|
| 4.1 | La barra de búsqueda y los filtros permanecen activos con 0 campañas (herramienta inútil en este estado) | BAJA |
| 4.2 | Igual que 3.2: CTA repetido (cabecera + estado vacío) | BAJA |

**Mejoras propuestas.** Deshabilitar búsqueda/filtros con 0 elementos (o mostrar el estado vacío dentro del área de resultados manteniendo la barra como referencia). Decidir la regla de CTA duplicado.

---

## 5. Inicio extremo (`pmb-2.1-inicio-extremo.jpeg`)

**Jerarquía visual.** Se mantiene con nombre largo: el título de la tarjeta trunca con elipsis y la miniatura-alternativa («L») aparece sin portada ✔.

**Legibilidad y densidad.** El truncado («La épica campaña …») es correcto y previsible; el nombre completo está disponible en la lista de Campañas y al entrar.

**Espaciado y alineación.** Sin cambios respecto al normal ✔.

**Consistencia.** Comportamiento de truncado coherente con COMPONENT_RULES §5 (una línea + elipsis).

**Problemas detectados.**

| # | Problema | Severidad |
|---|---|---|
| 5.1 | Con nombres largos + acción dentro de la tarjeta, el título se reduce a ~18 caracteres útiles | MEDIA-BAJA (misma causa que 1.2) |
| 5.2 | Sin tooltip visible en la captura para el nombre truncado (el componente sí pone `title`, verificar en integración) | BAJA (comprobable solo en producto) |

**Mejoras propuestas.** Misma que 1.2 (ancho completo para el título); asegurar tooltip/nombre accesible completo en truncados.

---

## 6. Campañas extremo — 14 elementos (`pmb-2.3-campanas-extremo.jpeg`)

**Jerarquía visual.** El nombre manda en cada fila y la lista es escaneable en su conjunto; sin embargo la **columna derecha de 14 «Continuar →»** forma un muro de texto que reclama atención indebidamente.

**Legibilidad y densidad.** Densidad apropiada (14 filas caben sin scroll en una pantalla), pero la lectura vertical sufre: **la mitad de las filas son de doble altura** (metadatos envueltos) y la otra mitad de una línea, creando un efecto acordeón. Buscar un nombre concreto implica saltos irregulares.

**Espaciado y alineación.** Padding consistente; la columna de acciones está bien alineada a la derecha. El **badge de estado oscila** entre filas (línea 1 o 2) y con él las columnas visuales de «estado», «rol», «participantes» y «actividad» se desalinean entre filas.

**Consistencia.** Truncado de descripción con elipsis correcto («…con la burocrac…»); alternativas con inicial («L», «A», «C», «B») consistentes entre sí; colisiones de inicial (dos «L», dos «C») inevitables con este método.

**Problemas detectados.**

| # | Problema | Severidad |
|---|---|---|
| 6.1 | Efecto acordeón: alturas de fila mixtas (≈7 filas de 2 líneas) rompiendo el ritmo de escaneo | **ALTA** |
| 6.2 | Badge de estado y columnas de metadatos sin alineación vertical entre filas | **MEDIA-ALTA** |
| 6.3 | Muro de 14 acciones «Continuar →» con texto idéntico repetido (ruido visual) | MEDIA |
| 6.4 | Colisiones de inicial en la miniatura-alternativa (2×«L», 2×«C») reducen la diferenciación | BAJA |
| 6.5 | Las columnas de metadatos (rol/participantes/actividad) cambian de posición horizontal según dónde termine la descripción | MEDIA |

**Mejoras propuestas.** Regla de fila de dos líneas fijas con zonas horizontales estables (nombre+estado | descripción | metadatos | acción — ver §7.1); acción como icono (§7.2); en la integración con datos reales, considerar color de fondo derivado de la portada para las iniciales (mejor diferenciación) — pendiente de decisión.

---

## 7. Resolución de los dos puntos pendientes (recomendación argumentada)

### 7.1 Metadatos en filas largas: ¿una o dos líneas?

**Recomendación: DOS líneas FIJAS por fila, con metadatos en UNA sola línea que nunca envuelve.** Concretamente:

- **Línea 1 — identidad y estado:** nombre (una línea, elipsis) + **badge de estado junto al nombre** (columna visual fija).
- **Línea 2 — contexto y datos:** descripción (elipsis, ancho flexible) y, en zona horizontal fija a la derecha, los metadatos en una línea: `rol · participantes · actividad`.

**Argumentación:**
1. Las capturas 2 y 6 muestran el coste real de permitir el envolte libre: altura de fila variable («efecto acordeón»), badge saltando de posición y columnas de metadatos desalineadas — el escaneo vertical, que es el objetivo §5 («fácil localizar con muchas»), es lo primero que se degrada.
2. La información que se pierde al truncar es de bajo valor para localizar (participantes/actividad exactos): está un clic más allá (espacio de campaña) y, en el futuro, en el inspector.
3. La alternativa de **una sola línea total** (nombre y metadatos juntos) se descarta: separar identidad de contexto empeora la legibilidad de nombres largos.
4. Regla de sacrificio cuando falta ancho (siempre en este orden): truncar descripción → ocultar «participantes» → ocultar «actividad» → truncar «rol». **Estado y nombre nunca se ocultan.**
5. El badge pasa a la línea 1 también para Inicio (tarjetas destacadas: badge junto al título).

**Decisión final: pendiente de revisión humana.**

### 7.2 Peso visual de las acciones rápidas

**Recomendación: acción fantasma SIEMPRE visible, con icono de flecha (→) en listas densas y con texto solo en elementos destacados (máx. 3–5).**

**Argumentación:**
1. En la captura 6, catorce etiquetas «Continuar →» idénticas forman un bloque de texto que compite con los metadatos y atrae la mirada antes que los nombres: el ruido crece linealmente con el número de filas.
2. El icono de flecha conserva la acción **siempre visible y accesible** (con `aria-label` «Continuar {nombre}» y tooltip), mantiene la columna derecha ligera y es reconocible de forma universal; el texto se reserva para las tarjetas de Inicio donde hay solo 3 instancias y la acción merece énfasis.
3. Se descarta **ocultar hasta hover/foco**: rompe accesibilidad por teclado y experiencia táctil, y contradice COMPONENT_RULES §4 (acciones alcanzables siempre).
4. Se descarta acción primaria/sólida por fila: el peso visual debe ser del contenido, no de las acciones (principio de guías visuales).
5. En invitaciones, «Aceptar» **mantiene** el estilo primario (decisión con consecuencia, pocas instancias) pero en tamaño compacto; «Rechazar» fantasma.
6. Ajuste adicional: la columna de acciones pasa a **ancho fijo** para que las flechas alineen verticalmente aunque cambie el texto interno («Ver» en archivadas usa el mismo hueco).

**Decisión final: pendiente de revisión humana.**

---

## 8. Resumen de problemas por severidad

| Severidad | Problemas |
|---|---|
| **ALTA** | 2.1/6.1 altura de fila no constante (metadatos que envuelven) |
| **MEDIA-ALTA** | 6.2 badges y columnas de metadatos sin alineación vertical |
| **MEDIA** | 1.1 tiempos a dos líneas en actividad · 1.2/5.1 truncado temprano de títulos · 2.3/6.3 muro de acciones con texto · 3.1 «Ver todas» visible con colección vacía · 6.5 columnas de metadatos en posición variable |
| **BAJA** | 1.3 «Ver todas» como enlace · 1.4 regla de redacción de metadatos · 1.5 fila de invitación envuelta · 2.4/6.4 peso y colisiones de las iniciales · 3.2/4.2 CTA duplicado · 4.1 búsqueda/filtros con 0 elementos · 5.2 tooltip de truncados (verificar en integración) |

**Coherencia transversal (añadido):** los badges de estado y chips de filtro son estilos del prototipo (`f2-state`, `proto-chip`), no componentes de la biblioteca — promoverlos a `Badge` y `FilterChip` durante la integración para no perder coherencia (problema de **MEDIA** de deuda de componentes).

---

## 9. Decisiones pendientes de revisión humana

1. **Aprobación (o no) del diseño** de los seis escenarios — esta autorevisión **no** lo aprueba.
2. **§7.1** — metadatos en filas largas: recomendación «dos líneas fijas, metadatos en una línea que no envuelve, badge junto al nombre».
3. **§7.2** — acciones rápidas: recomendación «icono-flecha en listas densas, texto en destacadas, siempre visibles».
4. Regla de **CTA duplicado** (cabecera + estado vacío): ¿se permite o se omite uno?
5. Tratamiento de las **iniciales** en campañas sin portada (peso de letra, posible color derivado).
6. Regla de **redacción de metadatos** de actividad (normalizar «hace X»).
7. Promoción de `Badge` y `FilterChip` a la biblioteca de componentes en la integración (ajuste técnico propuesto).

*Solo tras la revisión humana de este documento y de las capturas se decidirá si se integran PMB-2.2 y PMB-2.4. Hasta entonces no se modifica ninguna pantalla del producto.*

---

# 10. REVISIÓN 2 (2026-11-09) — correcciones aprobadas aplicadas

Se aplicaron las seis recomendaciones aprobadas por el propietario **solo** en los prototipos PMB-2.1/2.3 y en la biblioteca compartida. Capturas nuevas: **7 escenarios** (los 6 anteriores + **Campañas «sin resultados»**), en `docs/visual/screens/pmb-2.*.jpeg`.

## 10.1 Correcciones aplicadas y verificación por escenario

| Decisión | Aplicación | Verificado en |
|---|---|---|
| 1. Dos líneas fijas por fila, sin crecer por envoltura | `ListRow` reestructurado (línea 1: nombre+badge; línea 2: descripción+metadatos en una línea); altura fija 64 px; orden de sacrificio en container queries (COMPONENT_RULES §4) | `campanas-normal`, `campanas-extremo` (14 filas con **altura uniforme**, efecto acordeón eliminado — problemas 2.1/6.1) |
| 2. Acción icono-flecha en listas densas | `IconButton` (→) de 28 px con tooltip y `aria-label` «Continuar/Ver {nombre}»; texto «Continuar →» solo en las 3 tarjetas destacadas de Inicio | `campanas-*` (columna de flechas discreta y alineada — 2.3/6.3), `inicio-normal` (texto en destacadas) |
| 3. Sin CTA duplicados | Estados vacíos de página sin botón (el texto guía hacia «Crear campaña» de la cabecera); regla añadida a COMPONENT_RULES §9 | `inicio-vacio`, `campanas-vacio` (una sola CTA — 3.1/3.2/4.2) |
| 4. Iniciales con protagonismo reducido | Alternativa con inicial más pequeña (15 px) y color apagado (`text-secondary`) | `inicio-extremo`, `campanas-extremo` (reconocibles, secundarias — 2.4/6.4) |
| 5. Tiempos «hace X» normalizados y alineados | Todos los datos temporales a «hace X»; columna fija a la derecha (88 px, texto alineado a la derecha); en filas sin subtítulo el tiempo acompaña al título | `inicio-normal` (actividad en una línea con columna de tiempos alineada — 1.1/1.4), `campanas-*` (columna de actividad alineada — 6.2/6.5) |
| 6. Badge y FilterChip como componentes | `src/ui/Badge.tsx` (5 tons) y `src/ui/FilterChip.tsx` (estado `selected` + `aria-pressed`) promovidos con reglas en COMPONENT_RULES §14 | Todos los escenarios (badges junto al nombre, chips de filtro — deuda de componentes cerrada) |

**Escenario nuevo:** `campanas-sin-resultados` — búsqueda activa («mina») sin coincidencias: estado «Sin resultados para «mina»» + acción «Limpiar búsqueda» (la búsqueda y filtros permanecen visibles para corregir la consulta).

**Problemas adicionales corregidos durante la recaptura** (detectados mirando las capturas, no el código): los metadatos de «Actividad» caían a una segunda línea y los tiempos no formaban columna fija (el grupo de identidad no empujaba los metadatos a la derecha) — corregido y **recapturado**.

## 10.2 Revisión de los seis escenarios + «sin resultados» (estado actual)

- **Inicio normal:** jerarquía correcta; actividad compacta (5 eventos en 5 líneas, columna temporal alineada); tarjetas con miniatura 64 px y acciones en texto. **Sigue pendiente (no aprobada para cambiar):** truncado temprano de títulos de tarjeta por la acción interna (problemas 1.2/5.1).
- **Campañas normal:** filas uniformes, badge pegado al nombre, tres columnas de metadatos alineadas entre filas, flechas alineadas. Densidad y escaneo muy mejorados respecto a la revisión 1.
- **Inicio vacío / Campañas vacío:** estados guiados sin CTA duplicada; «Ver todas» oculto con colección vacía; búsqueda/filtros siguen visibles con 0 elementos (problema 4.1 **no corregido**: quedó fuera de las 6 decisiones aprobadas; se propone deshabilitarlos).
- **Inicio extremo / Campañas extremo:** nombres largos con elipsis + tooltip (`title`), 14 filas de altura uniforme, alternativas de inicial discretas. Colisiones de inicial (2×«L», 2×«C») **no resueltas** (inherentes al método de inicial; se propone color derivado de la portada, pendiente).
- **Campañas sin resultados (nuevo):** guía + «Limpiar búsqueda».

## 10.3 Problemas abiertos tras la revisión 2

| # | Problema | Severidad | Estado |
|---|---|---|---|
| 1.2/5.1 | Títulos de tarjeta se truncan pronto porque la acción compite por el ancho | MEDIA-BAJA | **No corregido** — fuera de las 6 decisiones aprobadas; requiere decisión (acción a la línea de metadatos o al foco) |
| 4.1 | Búsqueda/filtros visibles con 0 elementos | BAJA | **No corregido** — propuesta: deshabilitar con motivo |
| 6.4 | Colisiones de inicial en portadas alternativas | BAJA | **No resuelto** — propuesta: color derivado (pendiente) |
| — | Las filas de 64 px sin miniatura (actividad, acceso rápido) son algo espaciosas para contenido de una línea | BAJA | Observación de densidad a criterio humano |
| — | Comportamiento responsive del orden de sacrificio (container queries) no es verificable con pruebas unitarias (jsdom) | BAJA | Limitación: verificado solo visualmente en capturas |

## 10.4 Verificación (revisión 2)

- Suite web: **119/119** (25 ficheros; +4: Badge, FilterChip, variante vacía/sin-resultados y extremo).
- `npm run build` (tsc + vite): sin errores.
- Capturas (7) correspondientes al código actual (generadas tras los últimos cambios y verificadas contra el build).
- MVP sin tocar; PMB-2.2/2.4 **no integrados**.

## 10.5 Decisiones pendientes de revisión humana (actualizado)

1. **Aprobación (o no) del diseño** con las correcciones aplicadas — sigue **sin** aprobarse por autorevisión.
2. Los tres problemas abiertos del §10.3 (truncado de títulos en tarjeta, búsqueda con 0 elementos, colisión de iniciales).
3. Densidad de filas sin miniatura (64 px vs 48 px).
4. Autorización para integrar PMB-2.2 y PMB-2.4 tras la revisión de estas capturas.
