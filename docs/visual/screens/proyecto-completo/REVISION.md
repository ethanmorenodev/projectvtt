# Revisión visual de todo el proyecto (2026-11-19)

16 capturas reales de la app integrada (login, Inicio, Campañas, Espacio de
campaña, creador, mesa, Character Hub + Relaciones, Biblioteca, Imágenes,
Créditos, Flujo del DM, Lore, Inventario, Clase propia y Herramientas).
Datos de prueba reales creados por API: campaña, escena y personaje.

## Lo que ya está bien
- **Login**: imagen cinematográfica y tarjeta centrada; buena primera impresión.
- **Inicio editorial**: hero, carrusel de campañas y franja compacta.
- **Biblioteca**: portadas CC0 con variedad, filtros plegables e inspector.
- **Creador y Clase propia**: asistentes de pasos con panel de resumen en vivo.
- **Mesa**: escenario protagonista, paneles laterales, dock y modo Enfocar.
- **Inventario y Herramientas**: composición clara, sin controles huérfanos.
- **Character Hub**: pestaña Relaciones conectada a la API (usa los `usages`
  del personaje y las relaciones tipadas del contenido).

## Lo que se ha corregido en esta pasada
- **Biblioteca de imágenes**: el formulario de subida ya no aparece siempre
  ocupando la parte superior; se abre con «Subir imagen» desde la cabecera o
  desde el estado vacío, y obliga a revisar licencia/atribución.
- **Character Hub**: «Relaciones» dejó de ser un texto reservado; ahora carga
  usos del actor + relaciones entrantes/salientes, con estados de carga, error
  y vacío, y respeta el filtrado del servidor.

## Huecos que aún se ven (contenido, no diseño)
1. ✅ **Corregido**: Flujo del DM y Lore muestran un estado «primera vez» con
   una acción principal que lleva a la Biblioteca a crear contenido.
2. ✅ **Corregido**: el resumen de campaña muestra tarjetas de escenas y
   personajes (con acceso a la mesa y al inventario), no solo contadores.
3. ✅ **Corregido**: Créditos en tarjetas por licencia con enlaces a los
   archivos `LICENSES.md` y acción de exportación en la cabecera.
4. ✅ **Corregido**: la pista del raíl se retira sola a los 8 segundos (y al
   primer uso), con preferencia persistida.
5. ✅ **Corregido**: el lanzador de herramientas de la mesa muestra etiquetas
   visibles (Ficha, Combate, Chat, Character Hub, Herramientas de mapa).

## Prioridad recomendada (actualizada)
1. ✅ Estados «primera vez» en Flujo del DM y Lore.
2. ✅ Resumen de campaña con tarjetas de escenas/personajes.
3. ✅ Autocierre de la pista del raíl tras 8 segundos.
4. ✅ Créditos en tarjetas por licencia con enlaces a los archivos `LICENSES.md`.
