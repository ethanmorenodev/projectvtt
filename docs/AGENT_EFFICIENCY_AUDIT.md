# Auditoría de eficiencia del entorno de agentes (2026-11-16)

Fases 1-5 ejecutadas **sin compilar la aplicación**. Baseline conservado para la evaluación posterior.

## 1. Medición inicial (verificable)

Fuentes: `%USERPROFILE%\.local\share\opencode\opencode.db` (`session_v2`, `session_message`) y `log/opencode.log`, consultadas en solo lectura con `node:sqlite`. No existe CLI de OpenCode en PATH.

| Indicador | Antes |
|---|---|
| Tokens entrada / salida / razonamiento (todas las sesiones) | 23.262.218 / 2.539.740 / 2.994.753 |
| Tokens cache lectura / coste | 1.197.893.248 / $0,0448 (plan gratuito) |
| Sesiones ProjectVTT (tabla `session_v2`) | 1 larga: in 13.674.308, out 1.381.279, cacheRead 887.651.328, 23 compactaciones (la tabla puede no incluir sesiones activas) |
| Llamadas a herramientas (ProjectVTT) | 4.795 (edit 1.475 · shell 1.206 · read 616 · grep 473 · write 423 · glob 68) |
| Subagentes usados | 0 |
| Lecturas de `.md` | 163 (36 distintos): DECISIONS 13× (76 KB), BACKLOG 14× (30 KB), POST_MVP_BACKLOG 13× (45 KB), ARCHITECTURE 11×, README 10× |
| Lecturas de código repetidas | ContentService.cs 18×, Program.cs 16×, EditorView.tsx 16×, App.tsx 15×, types.ts 15× |
| Comandos shell build / test / total | 201 / 249 / 1.206 (lint: 0, no existe) |

Después: sin datos comparables todavía → **ahorro no cuantificable** (no se inventan métricas).

## 2. Problemas hallados (evidencia)

1. `AGENTS.md` §2 obligaba a leer 6 documentos (~132 KB) en cada tarea; `DECISIONS.md` (76 KB) leído 13 veces.
2. Instrucción obsoleta: «ejecuta lint» (AGENTS §4) cuando `apps/web` no tiene script de lint.
3. Duplicados: bloque de comandos idéntico README ↔ CONVENTIONS; índice de documentación duplicado en README.
4. README obsoleto: «pendiente de aprobación antes de Fase 1, no se ha tocado código» y recuento de pruebas anticuado.
5. Compilaciones frecuentes: 201 builds en historial (patrones `npm test; npm run build` repetidos); `tests/E2E/E2eAppFixture.cs:49` ejecuta `npm run build` en **cada** arranque E2E y `dotnet run` recompila la API.
6. 0 subagentes históricos: todo el razonamiento y las salidas de prueba pasaban por el contexto principal (23 compactaciones).
7. Referencias circulares leves de navegación: AGENTS ↔ DECISIONS ↔ BACKLOG ↔ CONVENTIONS.

## 3. Cambios realizados (solo documentación y configuración; ningún código funcional)

- **Nuevo `docs/INDEX.md`**: índice canónico con política de lectura bajo demanda (estado vigente/histórico/en curso, tamaño y cuándo leer cada documento).
- **`AGENTS.md`**: §2 → lectura vía índice y por secciones; §4.6 → política de compilación agrupada (sin lint); §7 → roles consolidados con puntero a `.opencode/agents/`.
- **`README.md`**: §Documentos → puntero al índice; estado post-MVP (ADR-031..039, Fases 0→4) y recuento de pruebas actualizados.
- **`docs/CONVENTIONS.md`**: bloque de comandos duplicado → puntero a README §Inicio rápido (conservados los comandos docker únicos).
- **Nuevo `opencode.jsonc`**: guardrails de AGENTS §6 → `git push/merge/cherry-pick/rebase/reset/clean` piden confirmación.
- **Nuevos `.opencode/agents/{developer,qa,architect}.md`**: subagentes con permisos acotados y formato de respuesta §9.

## 4. Agentes configurados

| Agente | Modo | Responsabilidad | Permisos |
|---|---|---|---|
| Coordinator | rol del agente primario | planifica, delega, integra (sin coste extra) | — |
| `developer` | subagent | implementa cambios acotados y valida lo mínimo | niega delegación |
| `qa` | subagent | ejecuta/diagnostica pruebas con resultados reales | niega edición |
| `architect` | subagent | revisa contratos/arquitectura, señala riesgos | solo lectura + `git *` |
| `explore` / `general` | integrados | exploración rápida / trabajo multipaso | los nativos |

## 5. Política de compilación (AGENTS §4.6)

Agrupar cambios → pruebas específicas del módulo primero → builds solo con justificación → compilación completa solo en cambios transversales → analizar el error antes de repetir un comando fallido.

## 6. Riesgos pendientes

- `E2eAppFixture.cs:49` recompila la web en cada arranque E2E: optimizarlo exige autorización (código funcional de pruebas).
- Métricas «después» aún no disponibles; el ahorro no puede cuantificarse todavía.
- Los perfiles nuevos quedaron validados con una delegación real (respuesta concisa, formato §9).
- La prueba E2E de edición/duplicado elegida previamente quedó implementada el 2026-11-16 (`tests/E2E/ContentFlowTests.cs`, E2E 4/4 en verde).

## 7. Comparación de métricas

| Indicador | Antes | Después |
|---|---|---|
| Tokens por tarea comparable | tabla §1 | medir |
| Lecturas documentales repetidas | 163 (.md) | medir |
| Compilaciones redundantes | 201 builds | medir |
| Transferencias de contexto | 0 subagentes | medir |
