# Iteración V2 — evaluación del diseño actual y propuesta (2026-11-19)

Estado: **aprobada por el propietario el 2026-11-19 (V2 completa, raíl oculto
por defecto) e integrada**. Prototipo: `#prototipo-alchemy-v2`. Capturas finales
de la app real: `v2-inicio-rail-oculto-real.jpeg` y `v2-inicio-rail-mas-real.jpeg`.

## 1. Evaluación de lo que hay (captura `exp7-inicio-editorial-real.jpeg`)

| Problema observado | Consecuencia |
|---|---|
| El raíl flotante tiene **10 iconos en tres grupos** y ocupa casi toda la altura útil | Domina la composición; compite con el hero en vez de acompañarlo |
| El logo aparece dos veces: marca arriba-izquierda y «ProjectVTT» en las migas | Ruido y redundancia visual |
| El avatar queda suelto abajo-izquierda, sin relación con el raíl | Se lee como un elemento huérfano |
| «Ver todas» y las flechas son controles pequeños y separados del título | No se entiende de un vistazo qué controla qué |
| Hero (430 px) + tarjetas (150 px) + actividad dejan grandes vacíos | La página parece incompleta aunque tenga contenido |
| El carrusel solo muestra 2 tarjetas de 244 px sobre 1060 px de ancho | Desperdicio de espacio; no se percibe como carrusel |
| Demasiadas rutas visibles en el raíl (Espacio, Imágenes, Créditos, DM, Lore, Gestión) | El usuario decide entre 10 opciones antes de empezar |

## 2. Principios de la corrección

1. **Una sola navegación, casi invisible.** El raíl se oculta por defecto; una
   flecha en el borde izquierdo lo revela al pasar el ratón (o al enfocarlo con
   teclado). Se puede fijar con un alfiler.
2. **Cinco destinos, no diez.** Inicio, Campañas, Mesa, Biblioteca y
   Herramientas. El resto se agrupa bajo «Más» y dentro del índice de
   Herramientas (que ya existe y no se elimina).
3. **Una acción principal por pantalla.** «Crear ▾» arriba-derecha; el resto de
   acciones se derivan al contexto.
4. **Densidad intencionada.** Hero más bajo, tarjetas más anchas (4 de pie),
   actividad e invitaciones en una sola franja compacta.
5. **Accesibilidad sin sacrificar estética.** La flecha es un botón real con
   `aria-expanded`/`aria-controls`; `Escape` cierra; el foco revela el raíl;
   `title`/`aria-label` en todo icono.

## 3. Qué se propone cambiar respecto a lo integrado

- `AppShell`: raíl plegable por hover/foco + botón de fijado; 5 destinos + «Más»;
  migas sin repetir la marca; avatar integrado en el raíl revelado.
- `InicioView`: hero de 360 px; carrusel con tarjetas de 244 px pero 4 visibles;
  franja única «Actividad · Invitaciones»; estado vacío con CTA explícita.
- Sin cambios de contratos, permisos, reglas ni contenido. Los destinos
  eliminados del raíl siguen accesibles desde Herramientas/Biblioteca/Campañas.

## 4. Riesgos

- Ocultar el raíl puede reducir el descubrimiento de áreas poco usadas; se
  compensa con la flecha siempre visible y con el índice de Herramientas.
- El comportamiento por hover debe tener equivalente por teclado y táctil
  (clic en la flecha), no solo ratón.
