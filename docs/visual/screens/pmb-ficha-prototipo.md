# Barrido de paneles MVP — Prototipo del rediseño de la Ficha clásica

**Fecha:** 2026-11-10 · **Estado:** ✅ **aprobado por el propietario** (2026-11-10, «Aprobado — aplicar a todos los paneles») → aplicado en `barrido-paneles.md`.
**Base:** lenguaje visual aprobado del Character Hub (pmb-3.6-hub.md) sobre los tokens aprobados. **Ruta dev:** `#prototipo-ficha[-vacio|-extremo]`.

## 1. Qué cambia (y qué NO)

La Ficha clásica **conserva toda su función** (crear fichas, seleccionar, editar nombre/atributos/PG, inventario con alta/edición/borrado, efectos, tiradas, sincronización con el Hub). Solo cambia su **composición y acabado**:

- **Cabecera** con retrato (inicial), nombre (elipsis en largos), identidad («Personaje · Nivel 1 · dnd5e») y el botón **«Editar ficha»** como acción principal (alterna a «Guardar» en modo edición).
- **Paneles de sección** con títulos en versalitas (lenguaje del Hub): ATRIBUTOS · ESTADO · INVENTARIO · TIRADAS.
- **Atributos en tarjeta** (puntuación grande + abreviatura + modificador en morado) — en edición, campos numéricos.
- **Estado**: PG + **chips de condiciones** con retirada (×).
- **Inventario** en filas limpias (nombre · ×cantidad) + fila de alta compacta.
- **Tiradas** en filas (tirada · resultado).

## 2. Variantes

- `#prototipo-ficha`: personaje de ejemplo (Elenar).
- `#prototipo-ficha-vacio`: alta de personaje + «Aún no hay actores en esta campaña.»
- `#prototipo-ficha-extremo`: nombre largo con elipsis en cabecera e inventario denso (9 ítems con nombres largos) sin romperse.

## 3. Verificación

3 pruebas nuevas (`FichaPrototype.test.tsx`: secciones + modo edición, vacío, extremo) — suite web **146/146** + build limpio. Capturas archivadas: `pmb-ficha-prototipo-{normal,vacio,extremo}-2026-11-10.jpeg`.

## 4. Pendiente de aprobación

1. ¿La composición (cabecera + paneles de sección + atributos en tarjeta) es la dirección que quieres para los paneles?
2. ¿Algún ajuste antes de aplicarla a la Ficha real y replicar el lenguaje a Chat/Combate/Notas/Historial?
