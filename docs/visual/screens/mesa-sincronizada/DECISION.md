# Sincronización al entrar en la mesa (EXP-48, 2026-11-26)

Tras la partida de cinco jugadores, elegimos la voz de Nuria: lo que impedía confiar era que el estado real no aparecía hasta recargar. Tres causas confirmadas en código y corregidas:

1. `SceneView` cargaba seis recursos con un único `Promise.all`: si fallaba uno (p. ej. `/combat`), **no se cargaba ninguno**, y el chat mostraba «Nadie ha escrito todavía» con mensajes en servidor. Ahora `Promise.allSettled` aplica cada recurso por separado, fusiona con los eventos del hub sin sobrescribirlos, muestra qué falló y ofrece «Reintentar carga».
2. `CampaignSpaceView` mostraba «0 escenas / 0 personajes» mientras llegaban los datos: añade estado de carga y guardia de generación; los `EmptyState` solo aparecen tras carga correcta.
3. `App` borraba campaña/escena ante cualquier error de hidratación: ahora conserva el estado previo, muestra el error y permite reintentar.

## Evidencia

- `01-espacio-cifras.jpeg`: campaña con 1 escena y 1 personaje reales, sin falsos ceros.
- `02-mesa-chat-inicial.jpeg`: al entrar en la mesa se ven los **3 mensajes previos** guardados por API (`verificacion.txt`: `mensajes visibles al entrar: 3`), sin `page.ReloadAsync`.
- `tests/E2E/TableSyncTests.cs`: 2 pruebas (mensajes previos y fallo inducido de `/combat` con aviso/reintento). **2/2**.
- Unitarias nuevas en `SceneView.test.tsx` (fallo parcial, fusión con eventos) y en `App`/`CampaignSpaceView` (carga y conservación de estado). Web **301/301** (incluye el caso «combate en vivo → reintento» que evita que la respuesta HTTP sobrescriba el evento).

## Matiz observado

En la captura de la mesa el indicador superior aún decía «Conectando…» durante el primer instante, aunque el chat ya estaba hidratado por HTTP. El estado del hub se actualiza al terminar `start`/`JoinCampaign`/`SyncScene`; no se ha automatizado la transición en navegador. No bloquea la lectura del estado, pero conviene vigilarlo en la próxima partida (el grupo reportó un error de negociación SignalR en consola).

Sin capturas pendientes.
