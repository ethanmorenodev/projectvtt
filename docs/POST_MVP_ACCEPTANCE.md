# Post-MVP — verificación de criterios de aceptación (§13)

**Fecha:** 2026-11-17 · **Alcance:** los 16 criterios de `docs/POST_MVP_PLAN.md` §13, verificados uno a uno (PMB-8.7).
**Fuente de verdad del estado:** `docs/POST_MVP_BACKLOG.md`. Este documento solo recoge la evidencia.

## Cómo se ha verificado

- Suites (PMB-8.1/8.2/8.3): Unit **59/59**, Integration **279/279**, E2E **4/4**, Performance **5/5**, Web **222/222**, builds limpios, sin drift de migraciones, smoke del escritorio `SMOKE OK`.
- Recorrido visual real de 11 rutas (PMB-8.5): `docs/visual/screens/pmb-8.5-consistencia.md` + capturas.
- Auditoría de accesibilidad (PMB-8.4): `docs/visual/screens/pmb-8.4-accesibilidad.md`.
- Seguridad (PMB-8.3): `tests/Integration/SecurityAuditTests.cs`, `ContentPrivacyTests.cs`, `DmFlowTests.cs`.

> ✅ = cubierto con evidencia · 🟡 = cubierto con salvedad documentada · ⬜ = no cubierto.

| # | Criterio (§13) | Estado | Evidencia | Salvedad |
|---|---|---|---|---|
| 1 | Navegación coherente en toda la app | ✅ | `apps/web/src/shell/AppShell.tsx`; recorrido PMB-8.5 (`pmb-8.5-*-real-*.jpeg`) | — |
| 2 | Espacio de campaña completo | 🟡 | `views/CampaignSpaceView.tsx`; escenas, personajes, participantes y lore reales | NPC, Criaturas, Biblioteca de campaña y Homebrew muestran catálogos reales (EXP-34); solo Facciones y Localizaciones conservan estados «próximamente» honestos; el lore ya enlaza con su vista real (PMB-8.5). |
| 3 | Mesa cómoda y configurable | 🟡 | `views/SceneView.tsx`: rejilla cuadrada/hex, tamaño de celda, zoom/pan, niebla, ventanas de herramientas (ADR-010/020/023/033) | La medición sigue siendo ayuda local; el dibujo y las anotaciones se persisten y comparten con `commandId` y evento en vivo (EXP-31, ADR-043). |
| 4 | Crear/gestionar personajes y consultar/editar fichas | ✅ | `views/CharacterCreatorView.tsx`, `views/ActorPanel.tsx`; `tests/E2E/CharacterFlowTests.cs` | — |
| 5 | Biblioteca organizada con categorías y variantes | ✅ | `views/LibraryView.tsx`; jerarquía y herencia (`tests/Integration/ContentModelTests.cs`, `HomebrewVersioningTests.cs`, ADR-032) | — |
| 6 | Contenido homebrew estructurado | ✅ | `tests/Integration/AbilityTests.cs`, `ChoiceGroupTests.cs`, `ActorEffectEndpointsTests.cs`; efectos como datos (ADR-040) | — |
| 7 | Permisos correctos | ✅ | `SecurityAuditTests.cs` (efecto real de grants y no escalada), `ContentPrivacyTests.cs`, `DmFlowTests.cs`, `WorldTests.cs` (aislamiento de mundos) | — |
| 8 | Versionado que protege el contenido existente | ✅ | revisiones, propuestas aprobar/rechazar (`DmFlowTests.cs`), import `.pvttpkg` con estrategias Keep/Update/Copy (`PackageTests.cs`, `Phase7QaTests.cs`) | — |
| 9 | Referencias visuales en los elementos | 🟡 | `views/ImageLibraryView.tsx`, `GET/PUT/POST /api/v1/images`, `imageId` en `ContentItem`, atribución y Créditos (ADR-036) | Las miniaturas se sirven con recorte CSS; el servidor no genera derivados (ADR-035) y se aceptan variantes subidas (PMB-4.10). Fallback con inicial cuando no hay imagen. |
| 10 | Inventario interactivo con equipamiento que afecta a las reglas | ✅ | `views/InventoryView.tsx`; espacios de equipamiento (ADR-041) y `tests/Integration/EquipmentEffectsTests.cs` | — |
| 11 | Character Hub integrado | ✅ | `views/CharacterHub.tsx` como ventana en `SceneView`, misma selección/datos que la ficha clásica; `CharacterHub.test.tsx` | — |
| 12 | Módulos que comparten datos | ✅ | contratos y esquemas en `packages/contracts/events/`; mismo DTO en web y escritorio; equivalencia ADR-015 verificada en `ActorEffectEndpointsTests.cs` | — |
| 13 | Funcionalidad del MVP conservada | ✅ | matriz PMB-8.1: E2E 4/4 (creación de personaje y flujo de contenido), Unit/Integration en verde, smoke del escritorio | — |
| 14 | Aplicación estable y cómoda | 🟡 | matriz integral en verde, guía de primeros pasos (README), estados vacíos guiados, accesibilidad AA, `git diff --check` limpio | «Cómoda» es subjetiva: la verificación responsive es solo visual y no hay lector de pantalla automatizado; el escritorio es vertical slice (ADR-030). |
| 15 | D&D 5e funcionando como primer sistema | ✅ | paquete `dnd5e-srd-5.1` (razas, subrazas, clases, trasfondos; ADR-037) sembrado e idempotente (`ContentSeedTests.cs`), blueprint `dnd5e.json` | — |
| 16 | Arquitectura preparada para más sistemas | 🟡 | `ContentType.System`, `CharacterBlueprint` por sistema, `World.System`, contenido/efectos como datos (ADR-040/042) | Solo D&D 5e se ha ejercitado de extremo a extremo; no se ha probado un segundo sistema real. |

## Conclusión

**16/16 criterios tienen evidencia; 11 ✅ y 5 🟡 con salvedades explícitas** (nº 2, 3, 9, 14 y 16). Tras EXP-31/34 los criterios nº 2 y 3 han reducido su salvedad (catálogos reales salvo facciones/localizaciones; dibujo y anotaciones compartidos, medición local), aunque conservan la marca 🟡 por las partes aún no cubiertas. No hay criterios ⬜. Las salvedades no bloquean el cierre de Fase 8: son deuda consciente, no fallos ocultos, y están reflejadas en el backlog.
