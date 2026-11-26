# PMB-3.6 — Prototipo del Character Hub

**Fecha:** 2026-11-10 · **Estado:** ⬜ **pendiente de aprobación del propietario** (tras aprobarlo: PMB-3.7 lo integra como ventana sincronizada con la ficha clásica).
**Base:** `docs/architecture/CHARACTERS.md` §3 (ventana tipo menú de RPG) + §4 (equipamiento visual). **Ruta dev:** `#prototipo-hub[-vacio|-extremo]`.

## 1. Composición

- **Cabecera**: retrato + nombre + línea de identidad (nivel · clase/subclase · raza/variante) + pestañas **Perfil / Equipo / Relaciones / Historia**.
- **Perfil**: atributos con modificadores, estado (PG, dado de golpe, CA, condiciones) y personalidad.
- **Equipo**: silueta central + **espacios de equipamiento configurables por sistema** (arma principal/secundaria, armadura, casco, guantes, botas, accesorios) + mochila con cantidades.
- **Relaciones**: grafo simple (personajes, facciones, rivales, familia) — el real se deriva de `ContentRelation` (ADR-031 §8).
- **Historia**: cronología de sesiones + **lore privado del DM** con badge **«Solo DM»** y un toggle de revisión que muestra cómo queda **oculto para el jugador** (en el Hub real se filtra en servidor — requisito obligatorio, PMB-3.8).
- **Pie**: nota de sincronización con la ficha clásica + acciones (Cerrar, Tirar d20).

## 2. Variantes

- `#prototipo-hub`: personaje de ejemplo (Elenar, Rogue/Thief, Enano de montaña) con equipo y relaciones.
- `#prototipo-hub-vacio`: estados vacíos honestos (sin recursos, mochila vacía, sin relaciones/historia).
- `#prototipo-hub-extremo`: nombres largos en el grafo y el título (desbordes).

## 3. Verificación

3 pruebas nuevas (`CharacterHubPrototype.test.tsx`: secciones, privacidad del DM reversible, variantes) — suite web **135/135** + build limpio. **Capturas pendientes del capturador** (bloqueo de ventana al final de la sesión; las rutas están listas para tomarlas).

## 4. Pendiente de aprobación

1. ¿La composición del menú de RPG (cabecera con pestañas + secciones) es la que quieres?
2. ¿El planteamiento de la silueta/espacios de equipo (referencia Baldur's Gate 3 del §4) va por buen camino?
3. Cualquier ajuste antes de que PMB-3.7 lo integre como ventana real del sistema de ventanas.
