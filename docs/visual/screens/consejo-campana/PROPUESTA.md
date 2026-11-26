# Consejo de diseño — Espacio de campaña (2026-11-24)

## Problema observado (capturas reales, arriba)

1. **Arrancar no manda.** En «Resumen», «Ir a la mesa» está al final de un bloque pequeño, bajo «Última actividad», con ~60 % del lienzo vacío. El código abre `scenes[0]`: entras a ciegas, sin saber qué escena ni quién controla cada personaje. Hay tres caminos al mismo sitio y ninguno destaca.
2. **Consultar interrumpe.** En «Escenas y mapas», «Notas del GM» e «Historial» son enlaces que además se abren como **dos ventanas flotantes solapadas**; la de delante tapa a la otra y deja ver IDs crudos. Es el «bloque emergente» que señaló el propietario. En la mesa no existen esas herramientas.
3. **El subnavegador promete más de lo que da.** 13 secciones: 4 reservadas, 3 redirigen al grafo de lore. El globo de navegación tapa «Facciones» y «Localizaciones».
4. **Ruido y baja jerarquía.** Metadatos repetidos («0 tokens», «Tipo Personaje · Nivel 3», «Ver en la mesa» sin nombre) y tipografía plana; morado compitiendo en píldora, botón, pestañas y globo; un segundo primario blanco «Crear».

## Desacuerdos del consejo y resolución

| Desacuerdo | Posiciones | Decisión del director | Coste / ganancia |
|---|---|---|---|
| Ventanas vs acoplado | Visual propone abrir Escenas/Personajes como ventanas (estilo «Tablero del director»); UX y GM exigen acoplar y eliminar el duplicado; Sistemas recuerda ADR-033 | **Acoplar Notas/Historial en un inspector con pestañas.** No crear ventanas nuevas en este espacio. El sistema de ventanas queda intacto **en la mesa**, que es para lo que se diseñó | Gana: desaparece el solapamiento y el usuario no "caza" ventanas. Pierde: la coherencia de usar el mismo sistema en todas partes —aceptado porque la regla de ADR-033 habla de la mesa, no de un explorador |
| Subnav plano vs agrupado | Visual: 3 grupos (Navegación/Mundo/Sistema); UX: 5 grupos (Sesión/Reparto/Mundo/Biblioteca/Gestión); Sistemas: no convertirlo en tabs | **5 grupos, como lista navegable (no tabs)**, con los nombres originales como subentradas | Gana: se ve el paso sin perder las 13 rutas. Pierde: dos niveles de profundidad en la navegación |
| Activo con píldora vs franja | Visual: franja morada de 2 px + fondo tonal; maqueta histórica: píldora | **Franja de 2 px + fondo tonal** | Gana: menos morado compitiendo; el acento queda para selección, foco y una sola acción primaria |
| Datos que no existen | GM y Sistemas: no hay escena activa, dueño de personaje ni recuentos en los DTO | **No ampliar contratos en esta ronda.** La acción primaria nombra la escena elegida en el inspector; se elige ahí, no `scenes[0]` | Gana: diseño verificable sin backend nuevo. Pierde: la "escena activa" sigue sin ser un dato del servidor |
| Homebrew | GM: debe nacer al crear, no de un hueco «Contenido homebrew» | El grupo **Biblioteca** conserva la entrada, pero el botón «Crear clase propia» se ofrece también desde «Crear personaje» | Gana: el flujo nace del acto de crear |

## Dirección elegida: **«Tablero del director», con una sola acción dominante**

Es la dirección A de Dirección Visual, recortada por las objeciones de UX/GM/Sistemas.

- **Cabecera (~112 px):** portada cuadrada de 128 px (la del SRD/portada de campaña o inicial), «CAMPAÑA · D&D 5e» en versalitas, título 28 px, descripción en una línea, Badge «Activa» pegado al nombre (§14). A la derecha, **una sola primaria**: «Ir a la mesa · *Puerto de Scaldar*». «Gestionar» pasa a secundario.
- **Izquierda (210 px):** 5 grupos con etiqueta en versalitas; activo con franja de 2 px + fondo tonal; entradas reservadas atenuadas y agrupadas al final. Sin globo flotante sobre el menú (se queda en el raíl global, fuera de este espacio).
- **Centro:** cifras en una franja con reglas de 1 px, **sin cajas**; dos listas compactas con `List`/`ListRow` de 64 px y dos líneas fijas (§4): miniatura cuadrada de 48 px, nombre + Badge, meta en una línea, tiempo en columna fija. **Una sola parada de Tab por fila**: la fila entera es el botón; la flecha es decorativa (`aria-hidden`) y el "abrir en la mesa" vive en el inspector. Esto corrige el botón anidado que señaló Sistemas.
- **Derecha (300 px):** inspector que aparece al seleccionar, con pestañas **Detalles / Notas / Historial**. Único hogar de notas e historial; nunca ventana. Notas solo para GM (el servidor ya filtra, PMB-3.8).
- **Identidad propia:** filete de 2 px solo en la ficha seleccionada + sombra desplazada de 1 px que sugiere un mazo sobre la mesa. Sin caligrafía, sin dorado, sin píldora de Linear. El filete **no** es foco: el foco es anillo de 2 px con offset 2 px y cambio de peso; se mitigó el riesgo que señaló Visual.
- **Estados:** selección (franja + filete), foco (anillo + peso), vacío (`EmptyState` compacto con una acción, §9), error (mensaje humano + reintentar), carga (skeleton con forma de fila), `reduced-motion` → 0 ms.

## Última crítica del consejo y correcciones aplicadas

Crítica visual: el prototipo incumplía su propia dirección —morado en seis sitios, activo en forma de píldora, identidad invisible, descripción truncada y botón de depuración estorbando.
Crítica de sistemas/accesibilidad: selector `ccp-time` vs `.ccp__time` (rompía la columna fija), metadatos repetidos, pestañas con ARIA incompleto, estado vacío sin acción y objetivos pequeños.

Correcciones hechas antes de recapturar:
- Morado reducido a la **única acción dominante** y a las franjas de selección; «Activa» en tono neutro; pestaña activa con regla neutra; se eliminó el botón duplicado del inspector.
- Activo del raíl con franja de 2 px + fondo **tonal neutro** (ya no `accent-soft`), fila seleccionada con franja + sombra de 1 px y texto neutro; sombra de la portada hecha visible.
- Descripción a dos líneas; actividad reciente añadida para aprovechar el lienzo; «Próximamente» a 10 px y sin opacidad.
- `ccp-time` → `ccp__time`; metadatos repetidos podados; `role="tabpanel"` + `aria-controls` + `tabindex` y flechas en las pestañas; estado vacío con una acción contextual por sección; botón de depuración eliminado (la variante `-vacio` se controla por hash).

Capturas finales: `prototipo-resumen.jpeg`, `prototipo-inspector-notas.jpeg` y `prototipo-estado-vacio.jpeg` (esta carpeta).

## Qué NO se toca
Contratos, permisos, reglas de negocio, rutas, sistema de ventanas de la mesa ni contenido del SRD. Es composición, jerarquía y reutilización de componentes existentes.

## Integración (aprobada e integrada 2026-11-24)

La dirección se integró en `CampaignSpaceView` con dos precisiones pedidas por el propietario antes de integrar:
- **Las 13 secciones** se agrupan en los cinco bloques del consejo, conservando nombres, rutas y funcionalidad (incluidas las reservadas y las de lore).
- **La imagen de campaña** usa la portada real (`useCampaignCover`); si no hay, muestra la inicial. El propietario puede cambiarla desde la propia cabecera (`uploadCover`, solo Owner, validación en servidor).

Notas e Historial son pestañas del inspector acoplado (Notas solo para GM por filtrado de servidor). Se corrigió además que la pista del raíl tapara la subnavegación: ahora se muestra abajo-izquierda.

Capturas de la app real: `integrado-resumen.jpeg`, `integrado-inspector-historial.jpeg` e `integrado-escenas-sin-ventanas.jpeg`.

**Cierre del hueco detectado tras integrar:** la sección «Escenas y mapas» seguía delegando en `ScenesView`, que abría «Notas del GM» e «Historial» como ventanas flotantes con `openByDefault` —exactamente el bloque emergente señalado. Se sustituyeron por un inspector acoplado con pestañas; al estar incrustada en el espacio de campaña, esa vista usa el inspector del padre (una sola hogar por función) y sus filas seleccionan en él en vez de abrir la mesa directamente. Verificado con E2E: cero ventanas flotantes en esa sección.
