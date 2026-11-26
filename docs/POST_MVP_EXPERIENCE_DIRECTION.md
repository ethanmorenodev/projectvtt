# Dirección de experiencia y creación visual (decisiones del propietario)

**Fecha:** 2026-11-17 · **Estado:** vigente — guía para las próximas fases de experiencia.
**Origen:** instrucciones del propietario (2026-11-17). Contrastadas con `POST_MVP_PLAN.md` §9, §12, §14 y con el código actual.

## 1. Objetivo

Que cualquier persona, aunque nunca haya jugado a rol, pueda **crear personajes, campañas y universos de forma visual, intuitiva y entretenida**, sin perder la profundidad para DM y construcción de mundos. Referencia visual: **Baldur's Gate 3** en presentación y carácter de las elecciones; **no se copian sus recursos** ni se exige personaje 3D: se usa la imagen que aporta el usuario.

## 2. Decisiones (fuente: propietario)

| # | Decisión | Dónde encaja en el plan |
|---|---|---|
| D1 | **Creación y gestión son composiciones distintas**. Creación: izquierda elecciones, derecha imagen grande del personaje. Gestión de equipo: izquierda inventario, centro la misma imagen, derecha objetos equipados por huecos del sistema. | §9 (amplía el detalle actual) |
| D2 | La imagen del personaje la **aporta el usuario**; se puede subir, reemplazar y ajustar el encuadre **sin perder el trabajo**. | §9 / §11 Fase 3 |
| D3 | Elecciones como **tarjetas con imagen + nombre + descripción breve**; pasos comprensibles (identidad, raza, clase, trasfondo, atributos, habilidades, equipo); resumen que se actualiza; volver atrás, experimentar, deshacer y guardar borrador; si una elección invalida otra, **explicarlo y conservar lo válido**; cierre con presentación visual antes de confirmar. | §9 / §14 |
| D4 | **Accesible para principiantes**: explicar primero qué permite una opción, reglas/cifras al profundizar; sin jerga; atributos y equipo recomendados **con explicación y personalizables**; pocas decisiones a la vez; probar/comparar/deshacer; imágenes con procedencia y licencia registradas; **no usar iniciales gigantes como solución definitiva**. | §14 + `POST_MVP_ASSETS_RESEARCH.md` |
| D5 | **Creador de clases homebrew para DM**, opción visible «Crear clase» con permisos: desde cero o duplicar; guiado y visual (nombre, imagen, concepto, estilo, atributos, recursos, competencias, equipo inicial, habilidades/pasivas, progresión y especializaciones, elecciones del jugador); reutilizar o crear contenido desde contexto; borradores, previsualización y publicación a mundos/campañas; jugadores solo ven contenido permitido; funciona igual que las oficiales; **sin programar ni editar JSON**; distinguir efectos automáticos de resolución manual; **actualizar una clase no debe modificar silenciosamente personajes existentes**. | §10 Fase 5 (amplía ADR-040/041) |
| D6 | **Lenguaje visual en toda la app**: protagonismo del contenido y sus imágenes, tarjetas/fichas/inspectores contextuales, miniaturas compactas en listados e imagen grande solo para protagonista o inspección, campos de texto integrados, **evitar pantallas de controles desactivados que anuncian funciones futuras**. | §4 / §14 |
| D7 | **Eliminar referencias internas de la interfaz**: `PMB-4.7`, `ADR-031`, `abilityBonuses`, explicaciones de serialización. Etiquetas humanas: «Bonificaciones de atributos», «Velocidad», «Idiomas», con unidades. Cuidar texto, contraste, teclado y márgenes. | §14 + PMB-8.4 |
| D8 | **Estados distintos**: carga / error / vacío. Si falla cargar campañas → «No se pudieron cargar las campañas» + reintentar; **no mostrar «Aún no hay campañas» ante un error**. Aplicar a biblioteca, créditos, imágenes y todo listado. | §14 |
| D9 | **Proceso**: contrastar con el plan y actualizar documentación; reutilizar sistemas sin duplicar reglas ni debilitar permisos; prototipos interactivos con capturas reales y estados representativos; **aprobación visual antes de integrar**; comprobar recorridos completos, no solo tests. No declarar terminada una pantalla solo porque compila/guarda/pasa tests: debe cumplir composición, comprensibilidad y comodidad. | §12 + §4 |

## 3. Estado actual vs. objetivo (huecos)

| Ámbito | Hoy | Falta según decisiones |
|---|---|---|
| Creador de personajes | Rail de pasos + contenido; **sin imagen del personaje** en el recorrido; opciones en rejilla de texto/chips; no hay subida de retrato (`ActorDto.portraitId` existe pero no se usa al crear). | D1, D2, D3: columna derecha con imagen grande y persistente; tarjetas de elección con imagen/descripción; subir/reemplazar/encuadrar sin perder borrador; presentación final. |
| Inventario | Ya es 3 columnas (mochila / silueta / equipado por huecos ADR-041), con miniaturas y compare parcial. | D1: sustituir la silueta genérica por la **imagen del personaje**; asegurar comparar/equipar/desequipar por botón además de arrastre; efectos en estadísticas visibles. |
| Homebrew (clases) | Editor universal + habilidades/elecciones/efectos (Fases 5), propuestas del DM y publicación; **no hay «Crear clase» guiado**. | D5: flujo dedicado, duplicar, previsualización, publicación por mundos/campanas, sin JSON, no silencioso sobre personajes existentes. |
| Lenguaje de interfaz | Código y comentarios con PMB/ADR; **cadenas visibles** en `i18n/es.ts` con referencias internas y notas de «Fase 5 (PMB-5.2)» y «serializar (ADR-031)». | D7: limpiar cadenas visibles (los comentarios de código no son interfaz). |
| Estados | Algunos listados muestran estado vacío o error por separado; conviene auditar todos. | D8: patrón uniforme carga/error/vacío con reintento. |
| Assets | 47 iconos CC BY 3.0; contenido del SRD sin ilustraciones específicas por hechizo/clase. | D4/D6: ampliar imágenes por hechizo, clase y otros contenidos D&D (licencias limpias), sin usar iniciales como solución final. |

## 4. Cómo se ejecutará (respetando §12)

1. **Documentación** (este archivo + enlaces desde el plan y el backlog). ✅
2. **Reutilizar**: sistema de ventanas, modelo de contenido, permisos del servidor, blueprint de personajes, `/api/v1/images`, inventario ADR-041 y editor universal. No duplicar reglas ni debilitar autorización.
3. **Prototipos interactivos** antes de tocar lo real, con capturas de estados representativos (normal, vacío, error, borrador, principiante/DM).
4. **Aprobación visual** del propietario para cada pantalla nueva.
5. **Verificación de recorridos completos** (crear personaje con imagen, equipar y ver efectos, crear/publicar clase, provocar error de carga y reintentar), además de las suites técnicas.

## 5. Tareas propuestas (se registrarán en el backlog tras aprobación)

- **EXP-1** Creador visual con imagen del personaje (D1–D4).
- **EXP-2** Gestión de equipo con la imagen del personaje y comparación/efectos (D1).
- **EXP-3** Creador guiado de clases homebrew para DM (D5).
- **EXP-4** Limpieza de lenguaje interno en la interfaz y estados carga/error/vacío (D7, D8).
- **EXP-5** Assets de contenido D&D con licencias registradas (D4, D6).

No se implementará ninguna pantalla nueva sin su prototipo aprobado.
