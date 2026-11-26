# Prototipo D1+D2 — Inicio editorial inspirado en Alchemy (2026-11-19)

Estado: **aprobado por el propietario el 2026-11-19** (D1+D2 completos,
collage CC0 actual, portadas con imagen subida + degradado) e **integrado**.
Prototipo de referencia: `#prototipo-alchemy`. Capturas: `prototipo-alchemy-inicio.jpeg`
y `exp7-inicio-{editorial,crear}-real.jpeg` (app real).

## Comparación

| | Alchemy (captura del propietario) | ProjectVTT actual | Prototipo D1+D2 |
|---|---|---|---|
| Entrada | Hero «Featured» con collage de portadas, título serif y CTA | Tarjeta pequeña con icono de 96 px | Hero «Destacado» con collage, título serif «D&D 5e» y CTA |
| Contenido | «Jump Back In», tarjetas horizontales con flechas | Rejilla de 2 tarjetas sin portada | «Continuar jugando», 4 tarjetas con portada y flechas |
| Navegación | Cápsula flotante a media altura, 5 iconos | Raíl fijo de 68 px con etiquetas de grupo | Cápsula flotante, 5 iconos con tooltip y `aria-label` |
| Acción principal | Píldora blanca «CREATE ▾» | Botón dentro de la tarjeta de Inicio | Píldora «Crear ▾» arriba-derecha con menú |
| Cabecera | Casi inexistente | Logo + migas + buscador + Salir | Solo marca y Crear |
| Tipografía | Serif para títulos, sans para UI | Todo sans, mismo peso | Serif del sistema para el hero, sans para controles |

## Qué se ha prototipado

- **D1 (Inicio editorial)**: hero con degradado y collage de 6 ilustraciones CC0,
  título serif, subtítulo y CTA «Explorar biblioteca»; carrusel «Continuar
  jugando» con portadas, metadatos y flechas.
- **D2 (chrome mínimo)**: raíl en cápsula flotante, píldora «Crear ▾» con menú
  (nueva campaña / clase propia / contenido) y avatar inferior izquierdo.

## Legalidad de los assets

No se usa ningún arte de Alchemy ni contenido protegido. El collage y las
portadas son ilustraciones **CC0 1.0 de OpenGameArt (Drummyfish)** ya incluidas
en `apps/web/public/art/fantasy/` y documentadas en `public/art/LICENSES.md`.
La tipografía serif es del sistema (Georgia / Times New Roman), sin licencia
nueva. No se añaden dependencias.

## Qué cambiaría al integrar (si se aprueba)

1. `InicioView`: sustituir el bloque «Continuar jugando» por el hero y el
   carrusel. Los datos seguirían viniendo de la API real (campañas, conteo del
   SRD); el prototipo usa datos de muestra.
2. `AppShell`: convertir el raíl en cápsula flotante y mover las acciones de
   creación a la píldora «Crear ▾». Se conservarían `aria-label`, `aria-current`,
   el salto al contenido y las migas de pan en modo accesible.
3. Estilos nuevos bajo tokens `--pv-*`; sin tocar reglas, permisos, rutas ni
   contenido.

## Decisiones pendientes

1. ¿Aprobar la **cápsula flotante** como raíl definitivo (sustituye al raíl de
   68 px aprobado en EXP-6 Fase A)?
2. ¿Aprobar el **hero con collage de iconos CC0** tal cual, o preferir esperar a
   incorporar un fondo CC0 de alta resolución (requiere buscar y acreditar un
   asset externo)?
3. ¿Las portadas de campaña deben usar **imagen subida por el usuario** cuando
   exista y el degradado con inicial como respaldo?

## Riesgos conocidos

- Las ilustraciones CC0 son de 128×128 px: ampliadas se ven algo blandas. Es
  aceptable en el collage difuminado del hero, pero las portadas conviene
  alimentarlas con imágenes reales de campaña cuando estén disponibles.
- La cápsula flotante reduce la visibilidad de las etiquetas de grupo; se
  compensa con tooltips y nombres accesibles, pero es un cambio de patrón
  frente a EXP-6 Fase A.
