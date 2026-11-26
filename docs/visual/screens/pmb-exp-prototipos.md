# EXP-1 / EXP-2 — Prototipos de creación y equipo visual

**Fecha:** 2026-11-17 · **Estado:** ⏳ prototipos listos, **pendientes de aprobación del propietario**. No se ha tocado la app real.
**Rutas dev:** `#prototipo-creador-visual` y `#prototipo-inventario-visual`.

## 1. Investigación y referencias

- **BG3 como referencia de presentación**, no de recursos: elecciones con peso visual, imagen del personaje protagonista, equipo con huecos. No se copia arte ni se exige 3D.
- Competidores (ver `pmb-8.6c-rediseno.md`): Foundry/Carolingian/Owlbear convergen en tablero protagonista y cromo discreto; aquí se aplica al **creador** y al **equipo**, no a la mesa.
- Licencias: los mini-iconos de las tarjetas son los SVG CC BY 3.0 ya aprobados (47 en `apps/web/public/icons/`, registro en `LICENSES.md`). La imagen grande es un **marcador honesto**: el jugador aportará la suya; no se generan retratos.

## 2. EXP-1 — Creador visual

Composición aprobada a proponer:

- **Izquierda:** pasos numerados (Identidad → Raza → Clase → Atributos → Presentación) con estado *hecho/activo*, y resumen vivo «Tu personaje».
- **Centro:** elecciones en **tarjetas con imagen + nombre + frase sencilla**. «¿Qué aporta?» despliega la explicación, los números y —cuando aplica— la **recomendación con motivo**. Opción visible «Crear una opción propia (DM)».
- **Derecha:** imagen grande del personaje **siempre visible**, con *Subir / Reemplazar / Ajustar encuadre* (el encuadre es reversible y avisa de que no se pierde trabajo) y resumen en vivo.
- Cierre con paso **Presentación** (borrador guardable y vuelta atrás en cualquier momento).

Estados capturados: elección con detalle abierto, paso de presentación.

## 3. EXP-2 — Equipo visual

- **Izquierda:** mochila con miniaturas; inspector del objeto con *Equipar / Comparar* (botones, sin depender del arrastre).
- **Centro:** misma imagen del personaje; debajo, panel de **efectos del equipo** (CA, velocidad, daño) con aviso explícito: «La ilustración no cambia de armadura: los efectos sí se aplican».
- **Derecha:** objetos equipados **por huecos del sistema** (ADR-041), con *Desequipar* por hueco.
- Comparación lado a lado entre lo equipado y lo seleccionado.

Estados capturados: comparación, armadura equipada con CA actualizada.

## 4. Datos y alcance

Todo de **muestra** (razas/clases del SRD, objetos de ejemplo). No hay llamadas al servidor, subida real de imagen ni persistencia. Es una propuesta visual: la integración deberá reutilizar blueprint, contenido publicado, `/api/v1/images`, `portraitId` del actor y el inventario ADR-041, sin duplicar reglas ni debilitar permisos.

## 5. Capturas

| Prototipo / estado | Archivo |
|---|---|
| Creador — elección con explicación | `pmb-exp1-creador-eleccion-prototipo-2026-11-17.jpeg` |
| Creador — presentación final | `pmb-exp1-creador-presentacion-prototipo-2026-11-17.jpeg` |
| Equipo — comparación | `pmb-exp2-inventario-comparar-prototipo-2026-11-17.jpeg` |
| Equipo — objeto equipado y efectos | `pmb-exp2-inventario-equipado-prototipo-2026-11-17.jpeg` |

## 6. Decisiones pendientes del propietario

1. **EXP-1:** ¿aprobar la composición izquierda/centro/derecha y el formato de tarjeta con «¿Qué aporta?» + recomendación?
2. **EXP-2:** ¿aprobar el centro con la imagen del personaje y el panel de efectos, más botones además del arrastre?
3. **Imagen del personaje:** el prototipo usa un marcador. ¿Adjuntas una imagen de muestra para la integración, o se deja el espacio para que cada jugador suba la suya desde el primer día?
4. **EXP-3 (creador de clases homebrew):** aún sin prototipo. ¿lo preparo a continuación con el mismo formato?

**Hasta la aprobación no se integra ninguno de estos diseños en la app real.**
