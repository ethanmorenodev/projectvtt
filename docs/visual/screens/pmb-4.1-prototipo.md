# PMB-4.1 — Prototipo del explorador de Biblioteca

**Fecha:** 2026-11-10 · **Estado:** ✅ **aprobado por el propietario 2026-11-10** («Aprobado — construir explorador real»); explorador real implementado en PMB-4.2 (commit `855ad50`).
**Base:** arquetipo «Explorador» de `docs/architecture/LIBRARY.md` §2 + tokens/componentes aprobados. **Ruta dev:** `#prototipo-biblioteca[-vacio|-extremo]`.

## 1. Composición (tres zonas, guía visual §3)

- **Izquierda — árbol de tipos y categorías**: los 3 tipos reales del seed (`Razas 18`, `Clases 12`, `Trasfondos 13`) con jerarquía de subrazas plegable (Dwarf → Hill/Mountain Dwarf, Elf → High/Wood/Drow…) y acción **«+ Nueva categoría»** (crear desde el árbol, LIBRARY §2).
- **Centro — rejilla de miniaturas cuadradas** (`thumb-md`, regla visual): buscador, chips de **ámbito** (Todos/SRD/Campaña), **orden** (Nombre/Recientes/Uso) y contador de resultados.
- **Derecha — inspector**: nombre + clave (`race/dwarf`), chips (tipo · ámbito · estado), resumen, **propiedades reales** del seed, **herencia** de subrazas (`extend`/`override`), **procedencia** (paquete `dnd5e-srd-5.1` · v5.1 · **CC BY 4.0** + atribución completa; los homebrew muestran «campaña · autor DM · revisión 1») y acciones (**Editar · Duplicar · Publicar · Ver usos**).

## 2. Datos honestos

Todo el contenido del prototipo sale del seed real `Seed/dnd5e-srd-5.1.json` (43 items, propiedades reales). Los nombres de item están en inglés porque es el original del SRD (**traducción pendiente**, plan §16); la interfaz va en español. Único item ficticio: el homebrew con nombre largo de la variante `extremo` (borrador de campaña, claramente etiquetado).

## 3. Variantes

- `#prototipo-biblioteca`: Dwarf seleccionado con inspector completo.
- `#prototipo-biblioteca-vacio`: filtro «Campaña» sin resultados → estado vacío honesto + inspector sin selección.
- `#prototipo-biblioteca-extremo`: 19 elementos densos + homebrew de nombre largo envuelto en árbol, rejilla e inspector.

## 4. Verificación

5 pruebas (`BibliotecaPrototype.test.tsx`: flujo árbol→rejilla→inspector con datos SRD, herencia de subrazas, filtros/limpiar, variantes vacío y extremo) — suite web **151/151** + build limpio. Capturas: `pmb-4.1-biblioteca-{normal,vacio,extremo}-2026-11-10.jpeg`.

## 5. Decisión del propietario

**2026-11-10 — Aprobado** («Aprobado — construir explorador real»): la composición de tres zonas (árbol · rejilla de miniaturas · inspector) es la dirección aceptada; no se pidieron ajustes. PMB-4.2 construyó el explorador real sobre la API de contenido con esta misma composición.
