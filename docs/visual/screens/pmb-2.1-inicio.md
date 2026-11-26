# PMB-2.1 / PMB-2.3 — Prototipos de Inicio y Campañas

**Fecha:** 2026-11-09 (revisión 2) · **Estado:** prototipos actualizados con las decisiones del propietario — **capturas presentadas para revisión; NO integrar hasta aprobación explícita** (las respuestas a las tres cuestiones no son aprobación de diseños, por decisión expresa del propietario).  
**Montaje:** dentro del shell real (AppShell), rutas `#prototipo-inicio[-vacio|-extremo]` y `#prototipo-campanas[-vacio|-extremo]`. Datos estáticos.

## 1. Decisiones del propietario (2026-11-09) aplicadas

1. **Miniaturas: 48 px en listas densas**; 64 px reservado a vistas amplias o elementos destacados («Continuar jugando»). Tokens actualizados (`--pv-thumb-xs`, DESIGN_TOKENS §11).
2. **Campañas: descripción y portada opcional** (ampliación de modelo aditiva en PMB-2.4, conservando datos) con **imagen alternativa determinista cuando no hay portada**: cuadrado `accent-soft` con la inicial del nombre.
3. **«Continuar jugando»: máximo 3 elementos + «Ver todas»**, sin scroll interno.

## 2. Capturas de revisión (6)

### Inicio
1. **`pmb-2.1-inicio-normal.jpeg`** — composición principal: 3 destacadas con miniatura 64 px + «Ver todas», actividad reciente, invitaciones y acceso rápido.
2. **`pmb-2.1-inicio-vacio.jpeg`** — estados vacíos guiados («Aún no hay campañas» con CTA, «Sin actividad reciente», «Sin invitaciones»).
3. **`pmb-2.1-inicio-extremo.jpeg`** — nombre muy largo (el título se trunca con elipsis en la tarjeta) y campaña **sin portada** (alternativa con inicial «L»).

### Campañas
4. **`pmb-2.3-campanas-normal.jpeg`** — lista densa con miniaturas 48 px, portadas y alternativas con inicial («C», «E»), descripción de una línea, badges de estado y metadatos separados.
5. **`pmb-2.3-campanas-vacio.jpeg`** — estado vacío con CTA.
6. **`pmb-2.3-campanas-extremo.jpeg`** — **14 campañas**: nombres largos (descripción truncada con elipsis), sin portada (iniciales L/A/C/B), estados mixtos y acciones rápidas — lista escaneable sin saturar.

## 3. Casos límite comprobados (petición del propietario)

| Caso | Comportamiento en las capturas |
|---|---|
| Estados vacíos | ✅ guiados con título, frase y acción primaria (Inicio y Campañas) |
| Nombres largos | ✅ título de tarjeta con elipsis; descripción de fila con elipsis; fila de lista admite el nombre completo |
| Campañas sin portada | ✅ imagen alternativa determinista con inicial sobre `accent-soft` |
| Listas con muchos elementos | ✅ 14 filas escaneables: miniatura 48 px, metadatos en línea, acciones alineadas a la derecha |

## 4. Qué valorar en la revisión

Composición, jerarquía, densidad, proporciones (miniaturas 48/64), separación entre elementos y comodidad de uso. Puntos abiertos a criterio del propietario: si la fila de lista densa prefiere metadatos en una o dos líneas cuando el nombre es largo; y el peso visual de las acciones rápidas por fila.

## 5. Decisión pendiente

**Aprobación explícita de estas capturas.** Solo tras ella se integran las pantallas definitivas (PMB-2.2 Inicio, PMB-2.4 Campañas con descripción/portada y las capacidades ocultas de API). Si se piden correcciones, se actualiza el prototipo y se vuelve a presentar.
