# ProjectVTT — Informe de auditoría POST-MVP (Fase 0)

> **Revisión 2026-11-09 (post-consolidación):** las decisiones del propietario
> sobre este informe se han incorporado en `docs/POST_MVP_PLAN.md` (v2) y en
> los ADR-031..035. Dos puntos de este informe quedan **superados por decisión
> del propietario**: (1) la propuesta de «catálogo semilla» provisional para el
> creador de personajes se sustituye por el **puente de contratos de contenido**
> real antes de la Fase 3 (sin catálogos provisionales); (2) el sistema de
> ventanas pasa a la Fase 1 como arquitectura de experiencia (ya estaba así
> propuesto aquí y se confirma). El diagnóstico técnico se mantiene íntegro.

**Estado:** ENTREGADO — pendiente de revisión y aprobación del propietario (`docs/POST_MVP_PLAN.md` §15 Fase 0 y §18).  
**Alcance:** solo auditoría y plan. **Sin cambios de código** (cumple §18: "No modificar todavía el código").  
**Fecha:** 2026-11-09 · verificado contra el código real (commit `f4bd060`).  
**Método:** inspección de código y documentación (58 endpoints del backend, 58 ficheros de la web, 13 del escritorio, 19 entidades, suites de prueba). Todo lo afirmado aquí está comprobado; lo que no se ha podido comprobar se marca como tal.

---

## 0. Dictamen en una página

1. **El MVP funciona y tiene buena base técnica**: servidor autoritativo, contratos versionados, permisos validados en servidor, tiempo real con control de versiones y 312 pruebas automáticas (230 backend + 79 web + smoke del escritorio). **La reutilización es la norma**: no hay que reescribir módulos funcionales.
2. **El mayor hallazgo funcional**: hay **7 capacidades completas en la API que la interfaz no expone** (renombrar/archivar campañas, gestionar invitaciones, cambiar roles, expulsar miembros, listar rulesets…). Son "casi gratis" de poner en la UI y mejoran mucho la percepción del producto (§2.3).
3. **El mayor hueco de producto**: todo el ecosistema de **contenido** (biblioteca, razas/clases/trasfondos, criaturas, objetos reales, homebrew, lore/facciones, mundos) **no existe**; la ficha de personaje es hoy nombre + números. Es la parte de construcción (Fases 3–7 del plan) y donde está el grueso del trabajo.
4. **La interfaz es de ingeniería, no de producto**: sin sistema de diseño, sin navegación profunda (3 vistas), sin ventanas, sin estados vacíos ni confirmaciones, sin onboarding. Todo el §4 del plan es material nuevo (Fases 1–2).
5. **Orden recomendado**: confirmado el del plan con dos ajustes de dependencia: (a) el creador de personajes (Fase 3) consume un **catálogo de sistema** que el ruleset aporta de forma mínima, para no bloquear con la biblioteca completa; (b) el **sistema de ventanas** (§13) debe nacer dentro de la Fase 1 como pieza de la arquitectura de experiencia, no como módulo tardío.

---

## 1. Estado actual

- **Stack real** (decidido, ADR-001..008): monolito modular ASP.NET Core 10 + EF Core (SQLite en dev/pruebas, PostgreSQL como destino, ADR-004), SignalR para tiempo real, React 19 + TypeScript + PixiJS en web, Godot 4.7.2 .NET en escritorio (vertical slice), Playwright para E2E.
- **Pruebas ejecutadas el 2026-11-09**: backend **230/230** (48 unitarias + 178 integración + 3 de rendimiento + 1 E2E con dos usuarios reales), web **79/79** (Vitest), escritorio **SMOKE OK** (end-to-end con vistas montadas).
- **Backend**: 58 endpoints REST v1 + hub `/hubs/campaign` con 12 eventos y 2 comandos; 19 entidades; backups/restauración por CLI; monitorización (`/health`, `/health/ready`, `/metrics`); logs estructurados sin secretos.
- **Web**: login, campañas, escenas, mesa de juego (mapa con zoom/pan, tokens con snap cuadrado/hex, niebla manual), panel de personaje (ficha, inventario, efectos, tiradas), chat + mensajes privados, combate/iniciativa, notas del GM e historial.
- **Escritorio**: vertical slice confirmado (login, campañas, escenas, mapa, tokens, movimiento en vivo) sobre los mismos contratos.
- **Documentación**: `PROJECT_SPEC`, `ARCHITECTURE`, `ROADMAP`, `BACKLOG` (63/63 del MVP), `DECISIONS` (ADR-001..030), `docs/CONVENTIONS`, `docs/DEPLOY`.

---

## 2. Funcionalidades existentes (clasificación §2.1)

Leyenda: ✅ implementada y funcional · 🟡 incompleta · 🔒 implementada pero **inaccesible desde la interfaz** · ⚠️ con problemas de usabilidad · ❌ no implementada.

### 2.1 Tabla de clasificación (áreas de §2.2)

| Área | Estado | Detalle verificado |
|---|---|---|
| Autenticación y cuentas | ✅ / 🟡 | Registro, login, logout, `/me`, sesiones revocables y rate limit (API+UI). Falta: cambio de contraseña, perfil de cuenta, recuperación por email |
| Campañas | ✅ / 🔒 | Crear, listar, detalle (API+UI). **Renombrar y archivar: API sí (`PATCH /campaigns/{id}`, `POST .../archive`), UI no** |
| Usuarios y participantes | 🔒 | `GET members` lo muestra el chat; **cambiar rol (`PATCH members/{id}`) y expulsar (`DELETE members/{id}`) solo existen en la API** |
| Roles y permisos | ✅ / ⚠️ | Owner/GM/Player con validación en servidor y pruebas IDOR. Sin UI de gestión de roles ni permisos finos (p. ej. por personaje/token) |
| Escenas | 🟡 | Crear, listar, abrir, ajustes de grid (`PATCH`). **Sin renombrar, eliminar, duplicar ni ordenar escenas (ni siquiera en API)** |
| Mapas | ✅ / 🟡 | Subir/reemplazar/eliminar con validación de tipo/tamaño/firma, evento `MapUpdated`, zoom/pan en web. Falta: zoom en escritorio, capas, miniaturas, varias imágenes |
| Tokens | 🟡 | Crear (con actor vinculado), mover con predicción+versión, snap cuadrado/hex, sincronización viva. **Sin editar nombre, borrar, tamaño/color/imagen, ni re-vincular (ni en API)** |
| Personajes (Actors) | 🟡 | CRUD de ficha: nombre, atributos (números), recursos actuales/máximos, tiradas con desglose auditable. **Sin raza, clase, trasfondo, nivel, descripción, imagen, categoría PC/NPC/criatura** (`ActorDto` = name + `attributes` + `resources`) |
| Fichas | ⚠️ | Editables desde el panel de la mesa (`ActorPanel`): correctas funcionalmente, en formato de formulario crudo |
| Tiradas | ✅ / 🟡 | Servidor autoritativo, reproducible, ventaja/desventaja 2d20 + modificadores por catálogo de efectos (ADR-015, **catálogo confirmado**), historial. Falta: fórmulas/macro, daño/sanación, dados personalizados en UI (solo "Extra") |
| Combate | ✅ / 🟡 | Iniciativa, turnos, combatientes, condiciones por escena (ADR-018). Falta: orden automático, indicador de turno en el mapa, duración de efectos por turnos |
| Chat | ✅ / 🟡 | Chat de campaña + mensajes privados 1:1 con eventos en vivo. Falta: editar/borrar mensajes, susurros, búsqueda, dados embebidos |
| Inventario | 🟡 | Ítems por actor (nombre, cantidad, notas) con CRUD. **Sin visual (miniaturas/equipamiento/contenedores), sin peso, sin ficha de objeto** |
| Objetos | ❌ | Solo como `ActorItem` suelto. No existe biblioteca de objetos ni propiedades/rareza/efectos |
| Criaturas | ❌ | No existe el concepto (solo `Actor`) |
| Biblioteca | ❌ | No existe (§9 del plan entero) |
| Creación de contenido | ❌ | No existe (§10: homebrew, habilidades, elecciones, versionado) |
| Gestión de campañas | 🔒 / 🟡 | Ver §2.3; falta además configuración de campaña (descripción, imagen, portada) |
| Guardado y persistencia | ✅ | Versionado de tokens/actores, idempotencia de comandos (`processed_commands`), transacciones, backups/restauración |
| Comunicación entre usuarios | ✅ | Chat, DMs y 12 eventos en vivo (tokens, actores, inventario, efectos, tiradas, combate, niebla, mapas, mensajes) |
| Conexión y reconexión | ✅ | Reconexión automática del hub + `SyncScene` + regla "la versión más nueva gana" (probada en E2E y en el smoke) |
| Errores y estados de carga | ⚠️ | ProblemDetails consistente y mensajes por panel. **Sin patrón unificado: no hay toasts, estados vacíos diseñados, skeleton loaders ni confirmaciones** (ver §5) |

### 2.2 Reutilizable tal cual (no tocar)

Servidor autoritativo completo (permisos, validación, versionado, idempotencia), contrato de tiempo real, motor de tiradas + catálogo de efectos, mapas persistidos, niebla por celdas, combate por escena, chat/DMs, notas, historial, monitorización, backups, suite de pruebas, snap/grid hex, token store. **Conclusión del §2 del plan: no hay que reescribir módulos funcionales; la base es sólida.**

### 2.3 Implementado pero inaccesible desde la interfaz (hallazgo mayor)

Capacidades completas y probadas en API sin botón en la web (comprobado: ninguna llamada desde `apps/web/src`):

1. Renombrar campaña — `PATCH /api/v1/campaigns/{id}`.
2. Archivar campaña — `POST /api/v1/campaigns/{id}/archive`.
3. Crear invitaciones — `POST /api/v1/campaigns/{id}/invites` (la UI solo sabe *canjear*).
4. Revocar invitaciones — `DELETE .../invites/{id}`.
5. Cambiar rol de miembro — `PATCH .../members/{id}`.
6. Expulsar miembro — `DELETE .../members/{id}`.
7. Listar rulesets — `GET /api/v1/rulesets`.

**Oportunidad**: integrarlas en la Fase 2 (espacio de campaña) con bajo coste y gran efecto.

### 2.4 Funcionalidades incompletas (resumen)

Tokens sin editar/borrar · escenas sin renombrar/eliminar · personajes sin identidad de juego (raza/clase/trasfondo/imagen) · inventario sin objetos reales · tiradas sin fórmulas · combate sin duración de efectos · chat sin edición/búsqueda · mapa sin medición/dibujo/capas · escritorio sin zoom/paridad amplia.

---

## 3. Funcionalidades ausentes (frente al plan)

Todo el bloque de **construcción de universos**: biblioteca universal con jerarquía y variantes (§9), editor universal + herencia (§9.3–9.4), homebrew estructurado con habilidades/pasivas/elecciones (§10), versionado de contenido (§10.4), referencias visuales/miniaturas/galerías (§11), inventario tipo BG3 con equipamiento y contenedores (§12), mundo/narrativa (facciones, localizaciones, cronología, lore, secretos del DM, §6/§14), mundos reutilizables con scopes de contenido (§6), creador de personajes asistido (§8.1), Character Hub (§8.2), sistema de ventanas (§13), herramientas de mapa avanzadas (medición, dibujo, anotaciones, capas, elementos interactivos, §7.3), gestión de contenido (import/export, plantillas, etiquetas, §14), calidad de producto (tutorial, estados vacíos, confirmaciones, accesibilidad, resoluciones, §14) y actividad reciente/miniaturas en campañas (§5).

---

## 4. Problemas de usabilidad (verificados en código)

1. **Sin confirmaciones destructivas**: no existe ni una llamada a `confirm()` en la web; "Borrar" (notas, ítems, efectos) se ejecuta al primer clic.
2. **Sesión caducada sin manejo global**: `ApiError` lleva el estado HTTP, pero cada panel muestra su propio error; no hay redirección unificada al login ni reintento (comprobado en `api/client.ts` + `AuthContext`).
3. **Sin estados vacíos ni de carga diseñados**: listas en blanco sin guía (p. ej. "crea tu primera escena"); sin skeletons.
4. **Navegación sin URL**: la app cambia de vista por estado interno (`App.tsx`); recargar en la mesa vuelve a campañas y el botón "atrás" del navegador no funciona.
5. **Gestión de campaña invisible** (§2.3): el usuario no puede hacer cosas que el servidor sí permite.
6. **Mesa saturada de controles fijos**: chat, combate, ficha, notas e historial conviven en asides siempre presentes; sin colapsar/redimensionar (el plan §7.2 ya lo pide).
7. **Ficha de personaje sin estructura de juego**: atributos como cuadros de número sueltos; crear un personaje "con identidad" (raza/clase/trasfondo) no es posible.
8. **Sin jerarquía visual ni identidad**: tema por defecto del navegador, sin tokens de diseño (§4.2 lo exige).
9. **Sin búsqueda** en campañas, escenas, personajes ni mensajes.
10. **Token sin identidad editable**: nombre fijado al crear ("Token N"), sin color/imagen/tamaño.

---

## 5. Problemas arquitectónicos y deuda técnica

### 5.1 Sólido (reutilizar)

- Monolito modular (ADR-001) con límites claros por dominio (`Identity`, `Campaigns`, `Scenes`, `Actors`, `Rules`, `Chat`, `Combats`, `Fog`, `Notes`, `History`, `Assets`, `Realtime`) — buena base para las fases nuevas.
- Servidor autoritativo + contratos versionados (JSON Schema en `packages/contracts/events/`, 14 esquemas).
- Semántica de estado versionado (tokens/actores) probada ante reordenación y reconexión.
- Permisos validados en servidor con pruebas de aislamiento.

### 5.2 Corregir (deuda)

| Deuda | Impacto | Propuesta |
|---|---|---|
| DTOs triplicados a mano (C# backend, TS web, C# escritorio) | Riesgo de divergencia al crecer el modelo | Generar tipos desde los JSON Schema o desde OpenAPI; mínimo: test de contrato |
| `SceneView.tsx` monolítico (718 líneas) con todo el estado de la mesa | Frena el rediseño §7 | Descomponer en la Fase 2 (stores + paneles + shell) dentro del rediseño, no antes |
| Divergencia de esquema SQLite (`EnsureCreated`, solo dev) vs migraciones Npgsql (producción) | Trampa conocida (BD demo desactualizada → 500) | Un único camino de migraciones; documentado ya en `docs/DEPLOY.md` |
| Duplicación de semántica web/escritorio (snap, token store) | Mantenimiento doble | Aceptada por diseño (ADR-030); proteger con casos de prueba compartidos |
| Sin i18n (cadenas en español embebidas) | Bloquea localización | Decidir en Fase 8; no urgente |
| Backups solo SQLite (`pg_dump` documentado) | Operación con PostgreSQL manual | ADR-029 lo deja registrado; mejora post-MVP |
| Historial sin paginación | Escala mal | Paginar al crecer campaña |
| Sin presencia (quién está conectado) | UX de mesa | Evento nuevo en el hub (bajo coste) |
| Repo sin remoto → CI (`ci.yml`) nunca ejecutada | Calidad no verificada en nube | Decisión del propietario |

### 5.3 Construir (arquitectura nueva necesaria)

1. **Modelo de contenido**: agregados `LibraryNode`/`ContentItem` (jerarquía categoría→variante, etiquetas, propiedades tipadas, herencia, versionado, visibilidad, imágenes) y su alcance (global/universo/campaña/DM). Es el cambio de datos más grande; exige ADR previo (relación con `ActorItem`/`ActorEffect` actuales).
2. **Mundos reutilizables** (§6): capa entre usuario y campaña con reglas de copia-vs-referencia ("modificar una campaña no debe alterar otras"). Requiere ADR de estrategia (clonado con enlaces vs referencias con overrides).
3. **Sistema de ventanas/paneles** (§13): componente único arrastrable/redimensionable/acoplable con persistencia de distribución — un solo sistema para todas las herramientas (lo exige el plan).
4. **Sistema de diseño** (tokens + biblioteca de componentes) — ver §7.
5. **Motor de reglas ampliado** (§10.1): la base (`IRuleset.PlanRoll` + catálogo) sirve; faltan efectos con duración, costes, usos y resolución manual asistida.
6. **Almacenamiento de imágenes de contenido** (§11): reutilizar `IAssetStore` (ADR-028) + derivados/miniaturas; object storage cuando se confirme ADR-007.

---

## 6. Propuesta de navegación (§3 del plan)

### 6.1 Mapa de áreas

```text
├── Inicio (dashboard)
│   ├── Campañas recientes · propias · compartidas · invitaciones
│   ├── Actividad reciente
│   ├── Acceso a Mundos y Biblioteca
│   └── Crear campaña (acción primaria)
├── Espacio de campaña            ← nuevo contenedor (hoy no existe)
│   ├── Resumen
│   ├── Mesa de juego ────────────→ workspace (§7)
│   ├── Escenas y mapas
│   ├── Personajes · NPC · Criaturas
│   ├── Biblioteca de campaña
│   ├── Contenido homebrew
│   ├── Historia y lore (facciones, localizaciones, cronología)
│   ├── Participantes y permisos   ← aquí van las 7 capacidades ocultas (§2.3)
│   └── Configuración
├── Biblioteca (global)            ← nuevo (§9)
├── Gestión (cuenta, preferencias, mis mundos)
└── [Command palette / búsqueda global]   ← transversal
```

### 6.2 Principios de navegación

- **Una entrada por tarea**: desde el espacio de campaña todo está a 1 clic; la mesa conserva acceso rápido a lo que se usa jugando.
- **Revelación progresiva**: el panel de la mesa muestra lo esencial; el inspector contextual (derecha) absorbe la edición; la biblioteca se abre como ventana/inpector, no como huida de la mesa.
- **URLs reales con router** (corrige §4.4): `/`, `/campaigns/:id/...`, `/library/...`, `/play/:sceneId` — recarga y atrás funcionan.
- **Command palette** (Ctrl+K) para buscar campañas, escenas, personajes y contenido sin menús profundos.

### 6.3 Inventario de pantallas necesario (paso 7 del §18)

1. Login/registro (rediseño). 2. Inicio/dashboard. 3. Lista de campañas (compacta, con búsqueda). 4. Espacio de campaña (shell con secciones). 5. Participantes y permisos. 6. Configuración de campaña. 7. Escenas y mapas (gestión). 8. Mesa de juego (workspace). 9. Creador de personajes (asistente por pasos). 10. Character Hub (ventana). 11. Biblioteca (explorador jerárquico). 12. Editor universal de contenido (ventana/pantalla). 13. Ficha de objeto (inspector). 14. Gestión de cuenta. 15. Estados vacíos y onboarding (transversal).

---

## 7. Propuesta visual (§4 del plan)

- **Dirección**: herramienta creativa profesional (principios de Inkarnate/Linear/Unreal/BG3/Alchemy sin copiarlos). Carbón oscuro con superficies por tonalidades, **acento morado apagado**, tipografía legible, iconografía coherente, espaciado consistente. Evitar negro puro, saturación, decoración y estética SaaS/administrativa (tal como pide §4.2).
- **Sistema de diseño primero (Fase 1)**: `docs/visual/DESIGN_TOKENS.md` (color, tipo, espaciado, radios, elevación), `COMPONENT_RULES.md` (botones con jerarquía, tarjetas compactas, inputs, listas, paneles, estados), `PROJECT_VISUAL_GUIDELINES.md` (principios por tipo de pantalla) y `REFERENCE_LIBRARY.md` (referencias citadas con principios extraídos).
- **Composición por pantalla** (§4.3): biblioteca de componentes compartida, pero la mesa, la biblioteca y el Character Hub tienen layouts propios (workspace vs explorador vs ficha).
- **Proceso visual obligatorio** (§4.4) con capturas reales: propongo ejecutarlo con el navegador integrado sobre la app en marcha (`ProjectVTT-inicio`) y prototipos HTML/React aislados: prototipo → captura → corrección → **aprobación antes de integrar**. Cada pantalla documenta su decisión en `docs/visual/`.
- **Cada campaña puede tener estilo propio sin romper la identidad de la aplicación** (imágenes/miniaturas de usuario; la UI solo aporta marcos neutros).

---

## 8. Roadmap post-MVP (orden óptimo propuesto)

El orden del plan se respeta con dos ajustes de dependencia (§10 de este informe). Complejidad relativa y riesgo por fase; los rangos son hipótesis (regla de `ROADMAP.md`: no comprometer fechas).

| Fase | Contenido | Complejidad | Riesgo | Hitos verificables |
|---|---|---|---|---|
| **0. Auditoría** ✅ (este documento) | Inventario, clasificación, navegación, visual, roadmap | S | Bajo | Informe aprobado por el propietario |
| **1. Arquitectura de experiencia** | Design tokens + componentes, shell de navegación con router/URLs, sistema de ventanas/paneles único, command palette | L | Medio (subjetividad visual) | Prototipos aprobados con capturas reales; biblioteca de componentes con pruebas; ventanas arrastrables/redimensionables con persistencia |
| **2. Rediseño de la aplicación** | Inicio, campañas (tarjetas compactas, búsqueda, invitaciones), espacio de campaña (participantes/permisos/configuración — integra las 7 capacidades ocultas), mesa con nueva distribución, estados vacíos/confirmaciones/toasts, sesión caducada | XL | Alto (no romper el MVP) | Flujo E2E existente en verde sobre el diseño nuevo; las 7 capacidades accesibles por UI; capturas aprobadas por pantalla |
| **3. Creación de personajes** | Modelo de ficha ampliado (identidad, raza/clase/trasfondo vía catálogo del ruleset, descripción, imagen), asistente de creación, Character Hub (perfil/equipo/relaciones/historia), sincronización ficha↔hub | L | Medio-alto (modelo de datos) | Crear un personaje completo de D&D 5e por UI; hub y ficha sincronizados; pruebas de permisos por actor |
| **4. Biblioteca** | `ContentItem` jerárquico (categorías/subcategorías/variantes), editor universal, etiquetas, búsqueda/filtros, imágenes/miniaturas (§11), visibilidad y autoría | XL | Alto (nuevo agregado + alcances) | CRUD de jerarquías con herencia; búsqueda; imágenes con miniaturas; reutilizar contenido en personajes |
| **5. Homebrew y motor de reglas** | Habilidades/pasivas estructuradas (tipo, activación, costes, usos, requisitos), elecciones, integración con el motor (automatización donde aplique, manual asistida), permisos del DM, versionado con diferencias | XL | Alto (reglas + permisos) | Publicar contenido homebrew; una habilidad automatizada resuelta por el motor; el versionado nunca cambia personajes existentes sin aviso |
| **6. Inventario y equipamiento** | Objetos como contenido de biblioteca, ficha de objeto (§12.2), inventario visual con miniaturas/equipamiento/contenedores, efectos del equipamiento en las reglas | L | Medio | Equipar/desequipar con reflejo en estadísticas; inventario visual usable; transición desde `ActorItem` sin perder datos |
| **7. Mundo y herramientas avanzadas** | Facciones, localizaciones, relaciones, cronología, lore/secretos del DM, mundos reutilizables con scopes, import/export, medición/dibujo/anotaciones/capas en el mapa | XL | Alto (alcance) | Grafo de relaciones; cronología; dos campañas compartiendo un mundo sin contaminarse; medición y dibujo en la mesa |
| **8. Calidad y lanzamiento** | Pruebas integrales, accesibilidad, rendimiento con biblioteca grande, consistencia visual, i18n (decidir), tutorial, documentación | M-L | Bajo-medio | Criterios §17 del plan verificados uno a uno; suite completa en verde; auditoría de accesibilidad |

**Comentario de orden**: si se prefiere entregar valor visible antes, las Fases 1–2 ya incluyen las mejoras "rápidas y visibles" (§2.3 + usabilidad §4). La Fase 3 puede empezar su modelo de datos en paralelo a la 2 (no toca UI nueva hasta su fase).

---

## 9. Dependencias entre funcionalidades (paso 8 del §18)

- Fase 2 depende de Fase 1 (componentes/ventanas/shell).
- Fase 3 (creador) consume un **catálogo de sistema** mínimo (razas/clases/trasfondos de 5e) que hoy puede aportar el ruleset en forma de datos semilla; la Fase 4 lo convierte en contenido editable con herencia. Esto evita bloquear el creador de personajes a la espera de la biblioteca completa.
- Fase 5 (homebrew) depende de Fase 4 (tipos de contenido) y amplía el motor de reglas existente (ADR-015).
- Fase 6 (inventario) depende de Fase 4 (objetos como contenido) y de la integración de efectos (Fase 5) para "el equipamiento afecta a las reglas".
- Fase 7 (mundos) depende de Fase 4 (alcances de contenido) y exige un ADR propio (copia vs referencia).
- Imágenes/miniaturas (§11) atraviesan Fases 2–7: decidir pronto el almacenamiento (reutilizar `IAssetStore`; S3/MinIO pendiente de ADR-007).
- El **escritorio** consume los mismos contratos: el rediseño de la web marca la pauta; la paridad del escritorio se replanteará al final de la Fase 2 (decisión del propietario).

---

## 10. Riesgos globales

1. **Alcance**: el plan (§9–§13) es un producto entero; mitigación: fases con hitos aprobados y backlog revisable tras cada una.
2. **Subjetividad visual**: mitigación con el proceso obligatorio de §4.4 (prototipo + captura + aprobación antes de integrar).
3. **Romper el MVP** durante el rediseño: mitigación con la E2E actual como red de seguridad (debe seguir en verde) y migración por pantallas.
4. **Modelo de datos nuevo** (biblioteca/mundos): riesgo de migra-ción y de mezclar alcances; exige ADR antes de Fase 4.
5. **Rendimiento con imágenes** (miles de miniaturas): derivados + paginación + posible object storage (ADR-007).
6. **Paridad escritorio**: si la web avanza mucho, el escritorio quedará rezagado; decidir estrategia al final de Fase 2.
7. **Validación sin CI remota**: hoy todo se verifica localmente (230+79+smoke); si el proyecto crece, publicar el repo y ejecutar `ci.yml` reduciría riesgo (decisión del propietario).
8. **Herencia/versionado de contenido** (§9.4/§10.4): conceptos potencialmente complejos; propongo prototipo de modelo + casos de prueba antes de UI.

---

## 11. Preguntas para el propietario (para aprobar el plan)

1. ¿Apruebas el orden ajustado (§8) y los hitos por fase?
2. Mundos reutilizables: ¿preferible **copiar** contenido al crear una campaña (aislamiento total) o **referenciar** con overrides (actualizaciones compartidas)? ¿O dejarlo para el ADR de la Fase 7?
3. ¿El sistema de ventanas (§13) entra en la Fase 1 como propongo, o se mantiene como módulo independiente posterior?
4. Paridad del escritorio: ¿la web manda y el escritorio sigue después, o quieres avances paralelos?
5. i18n: ¿español primero y localización en Fase 8, o diseño multiidioma desde Fase 1?
6. Contenido de D&D 5e para el catálogo semilla (Fase 3): ¿un set mínimo (p. ej. 6 razas, 8 clases, 6 trasfondos) redactado por nosotros, o aportarás tú las listas?
7. ¿Publicamos el repo en un remoto para ejecutar la CI real? (afecta al riesgo 7)

---

*Fin del informe. No se ha modificado código; este documento + `docs/POST_MVP_PLAN.md` son el entregable de la Fase 0, pendiente de tu aprobación.*
