# PMB-3.4 — Prototipo del creador de personajes

**Fecha:** 2026-11-10 · **Estado:** ⬜ **pendiente de aprobación del propietario** (tras aprobarlo: PMB-3.5 integra el creador real).
**Base:** mapa de pasos aprobado (`pmb-3.1-creador-flujo.md`, con las 4 decisiones del propietario) + ADR-034 confirmada. **Ruta dev:** `#prototipo-creador[-vacio|-extremo]` (montado en el shell real).

## 1. Composición (10 pasos del mapa)

- **Cabecera**: título con la campaña, chip **«Borrador guardado»** (guardar y continuar, paso actual + selecciones) y contador «Paso N de 10».
- **Raíl de pasos** (232 px, como el Espacio de campaña): los 10 pasos, con ✓ en los completados; clicable para saltar.
- **Contenido**: el paso activo; **efectos visibles de cada elección** (bonificaciones, velocidad, idiomas, rasgos) en panel «Efectos de la elección».
- **Barra inferior**: Atrás · **Guardar y salir** · Siguiente.

## 2. Decisiones del propietario aplicadas

| Decisión | Cómo se ve |
|---|---|
| 1. SRD amplio | Tarjetas de razas del SRD con subrazas (Dwarf → Hill/Mountain…), clases, trasfondos |
| 2. Ruta completa | El asistente ocupa el área del espacio (no ventana) |
| 3. Manual + compra de puntos | Segmentado en «Atributos»: compra con −/+, coste por valor y **presupuesto 0/27**; manual con campos 3-20 |
| 4. Equipo inicial en el creador | Paso 8 «Equipo inicial» (reglas por clase llegan con el contenido de objetos) |

## 3. Variantes de revisión

- `#prototipo-creador`: build de ejemplo (Elenar, Enano de montaña, Rogue, Criminal).
- `#prototipo-creador-vacio`: sin elecciones — estados vacíos honestos por paso.
- `#prototipo-creador-extremo`: nombres largos («Guardia de élite de la ciudadela de Scaldar con armadura ceremonial») para valorar desbordes.

## 4. Verificación

3 pruebas nuevas (`CreadorPrototype.test.tsx`: navegación + efectos, compra de puntos, variantes) — suite web **132/132** + build limpio. Capturas archivadas: `pmb-3.4-creador-{paso1-sistema,raza-efectos,atributos,revision,vacio,extremo}-2026-11-10.jpeg`.

**Corrección al capturar**: los pasos del raíl heredaban el estilo azul nativo de `<button>` (los estilos `proto-subnav__item` están pensados para `div`); reset aplicado en `creador.css`.

## 5. Relación con el backend (ya hecho, PMB-3.2/3.3)

El creador real consumirá `GET /characters/blueprints` (pasos del asistente) y `POST /characters/build/preview` (validación por paso + estadísticas derivadas en servidor: bonificaciones de subraza, compra de puntos, cuota de habilidades). El prototipo muestra esa misma información con datos de muestra.

## 6. Pendiente de aprobación

1. ¿La composición general (raíl de pasos + cabecera + efectos) es la que quieres?
2. ¿Los pasos y su orden (los del mapa aprobado) siguen siendo correctos vistos en pantalla?
3. Cualquier ajuste de densidad, texto o comportamiento antes de integrar PMB-3.5.
