# Biblioteca arreglada (EXP-47, 2026-11-26)

El propietario señaló que la Biblioteca «no estaba bien implementada». La auditoría confirmó cuatro fallos reales y se corrigieron:

1. **Arte asignado**: las tarjetas usaban una miniatura derivada por nombre/tipo e ignoraban `imageId`. Ahora `LibraryTile` usa `useImageUrl(item.imageId)` con fallback a `contentCover`; el preview del editor usa el mismo helper en vez de una inicial. `contentCover` devuelve `kind:'cover'` para que todo el arte comparta recorte/esquinas/degradado (`--cover`).
2. **Búsqueda y filtros**: busca también por `typeKey`/tipo legible; cambiar de tipo reinicia búsqueda, etiqueta, estado y ámbito; el contador refleja los resultados visibles con filtros.
3. **Acciones**: publicar conectado al endpoint real (deshabilitado con motivo si ya está publicado), exportación de paquetes con botón y categorías vacías deshabilitadas con `title`.
4. **Escala**: paginación client-side de 60 en 60 con «Mostrar más».

## Evidencia

- `01-tarjeta-con-imagen.jpeg`: rejilla de Criaturas con recorte uniforme; el ítem «Dragón con imagen» muestra su blob autenticado (`verificacion.txt`: `src=blob:…`), frente al arte derivado del resto.
- `tests/E2E/LibraryFlowTests.cs`: 3 pruebas (imagen asignada vs. fallback, búsqueda por tipo + reset de filtros, paginación). **3/3**.
- Web **292/292** (incluye 8 pruebas nuevas de Biblioteca/iconografía/editor).

## Decisiones derivadas

- El 403 de importación sigue mostrándose tal cual: la UI no simula permisos de operador (ADR-044).
- El arte derivado por palabra clave se conserva como **fallback**, no como sustituto del arte subido por el usuario. Si el `imageId` existe pero la descarga falla (404/red), `useImageUrl` devuelve `null` y se cae al mismo fallback: la tarjeta sigue siendo legible, pero la UI **no distingue** «sin imagen» de «fallo de transporte». Decisión deliberada (probada en `LibraryView.test.tsx`), no un error funcional oculto.
- La paginación es client-side porque el endpoint HTTP no expone `skip/take`; si la biblioteca crece mucho, el siguiente paso es paginación en servidor.

Sin capturas pendientes.
