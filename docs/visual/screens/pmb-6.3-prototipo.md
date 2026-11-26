# PMB-6.3 — Prototipo del inventario visual

**Fecha:** 2026-11-17 · **Estado:** ⏳ **prototipo listo, pendiente de aprobación del propietario** (no se ha tocado la app real).
**Ruta dev:** `#prototipo-inventario` (`#prototipo-inventario-vacio`).

## 1. Referencias

- `CHARACTERS.md` §equipment: silueta 2D central con huecos, miniaturas `thumb-sm` y mochila; **espacios configurables por sistema** (ADR-041).
- Referencia visual **BG3**: huecos alrededor de la silueta, tarjetas de objeto con peso/valor/rareza y acciones de equipar/desequipar.

## 2. Composición

- **Izquierda — Mochila:** lista de objetos con glifo, nombre y peso; contador y estado vacío honesto.
- **Centro — Silueta:** 8 huecos posicionados (arma principal, secundaria, casco, armadura, guantes, botas, accesorio 1/2) mostrando el objeto equipado o el rótulo del hueco; al pulsar un hueco ocupado se selecciona su objeto.
- **Derecha — Detalle:** peso, valor, rareza, contenedor si aplica, efectos declarados, selector de hueco + acciones **Equipar** (sin sobrescribir huecos ocupados) y **Desequipar**.
- Aviso de estado (`role=status`) para cada operación.

## 3. Datos honestos

Todos de MUESTRA (`inventoryData.ts`): 7 objetos (Longsword, Escudo, Chain Mail, Backpack, Potion of Healing, Rope, Boots of Elvenkind) con glifos como miniaturas; sin imágenes externas.

## 4. Próximo paso tras aprobación

PMB-6.4 (UI real consumiendo `ActorItem` con `slotId`/`ContentItemId` y los endpoints de PMB-6.2) y PMB-6.5 (`ApplyEquipment` para peso/efectos). El servidor manda; la UI solo presenta.

## 5. Capturas

| Estado | Captura |
|---|---|
| Silueta + huecos + mochila | `pmb-6.3-inventario-prototipo-2026-11-17.jpeg` |
| Objeto equipado en Casco | `pmb-6.3-inventario-equipado-prototipo-2026-11-17.jpeg` |

## 6. Decisiones pendientes del propietario

1. **Composición:** ¿se aprueba la silueta con huecos alrededor (estilo BG3) + mochila lateral + inspector, o se prefiere otra distribución?
2. **Interacción:** ¿equipar con selector de hueco + botón es suficiente, o se prefiere arrastrar y soltar desde el inicio?
3. **Rareza/efectos:** ¿se mantiene la etiqueta de rareza y la lista de efectos en el detalle, o falta algún dato?

**Respuesta esperada:** «Aprobado — construir» (con alcance) o los cambios concretos. Hasta entonces no se modifica la app real.
