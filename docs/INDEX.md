# Índice documental

Política de lectura (eficiencia de contexto):

1. Consultar primero este índice.
2. Identificar los documentos relevantes y leer **solo las secciones necesarias** (grep + offset; no leer archivos grandes completos por defecto).
3. Reutilizar la información vigente ya disponible en el contexto; no releer lo que no ha cambiado.
4. No cargar documentación de otras áreas sin una dependencia justificada.
5. Al mover o renombrar un documento, actualizar sus referencias aquí.

**Carga automática:** en cada sesión solo se carga [`../AGENTS.md`](../AGENTS.md) (5 KB). Todo lo demás es lectura bajo demanda.

## Documentos

Estado: *vigente* = en uso · *histórico* = fase cerrada (consultar solo para investigación) · *en curso* = trabajo activo.

| Documento | Tamaño | Estado | Leer cuando |
|---|---|---|---|
| [`../README.md`](../README.md) | 12 KB | vigente | arranque, estado global, API, arranque rápido y guía de primeros pasos |
| [`../PROJECT_SPEC.md`](../PROJECT_SPEC.md) | 7 KB | vigente (MVP) | requisitos y criterios del MVP |
| [`../ARCHITECTURE.md`](../ARCHITECTURE.md) | 7 KB | vigente | límites entre componentes, arquitectura base |
| [`../DECISIONS.md`](../DECISIONS.md) | 76 KB | vigente | **grep por número de ADR**; nunca leer completo por rutina |
| [`../ROADMAP.md`](../ROADMAP.md) | 3 KB | histórico (MVP) | fases del MVP |
| [`../BACKLOG.md`](../BACKLOG.md) | 30 KB | histórico (MVP) | estado de tareas MVP cerradas |
| [`POST_MVP_BACKLOG.md`](POST_MVP_BACKLOG.md) | 61 KB | **en curso** | solo la fase/tarea activa (grep `PMB-`) |
| [`POST_MVP_PLAN.md`](POST_MVP_PLAN.md) | 18 KB | vigente | dirección de diseño; solo la sección necesaria |
| [`POST_MVP_AUDIT.md`](POST_MVP_AUDIT.md) | 25 KB | histórico (Fase 0) | referencia de auditoría |
| [`POST_MVP_ACCEPTANCE.md`](POST_MVP_ACCEPTANCE.md) | 7 KB | vigente | verificación uno a uno de los criterios §13 (PMB-8.7) |
| [`AUDIO_EVALUATION.md`](AUDIO_EVALUATION.md) | 3 KB | histórico | evaluación PMB-8.8 previa; ADR-049 (2026-11-26) aprobó el audio y dejó sin vigor el diferimiento |
| [`POST_MVP_ASSETS_RESEARCH.md`](POST_MVP_ASSETS_RESEARCH.md) | 23 KB | vigente | fuentes y licencias de assets (PMB-4.7) |
| [`CONVENTIONS.md`](CONVENTIONS.md) | 6 KB | vigente | versiones, migraciones, convenciones de código y pruebas |
| [`DEPLOY.md`](DEPLOY.md) | 5 KB | vigente | despliegue self-hosted (compose ejecutado y verificado el 2026-11-25, EXP-38) |
| [`architecture/`](architecture/) | 32 KB | vigente | modelo de contenido, biblioteca, personajes, ventanas |
| [`visual/`](visual/) | — | vigente | dirección visual: por pantalla, ficha o regla concreta (nunca el directorio completo; incluye `tour-guiado/`, `audio-ajustes/` y `importacion-paquetes/` de EXP-43..45) |
| [`../packages/contracts/README.md`](../packages/contracts/README.md) | 5 KB | vigente | contratos compartidos |
| [`../AGENTS.md`](../AGENTS.md) | 5 KB | vigente (auto-cargado) | reglas de trabajo; no releer: ya está en contexto |
| [`AGENT_EFFICIENCY_AUDIT.md`](AGENT_EFFICIENCY_AUDIT.md) | 6 KB | histórico (2026-11-16) | baseline de métricas de agentes y cambios de configuración |

Tareas/planeación: el trabajo activo vive en `POST_MVP_BACKLOG.md`; los roadmaps/backlogs raíz son histórico del MVP. Ante contradicciones entre un documento histórico y el backlog activo, manda el backlog activo; si no hay evidencia, documentar la cuestión sin inventar decisiones.
