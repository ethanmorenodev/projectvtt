# ProjectVTT — Plan maestro POST-MVP (v2 consolidada)

**Estado:** PROPUESTA DEFINITIVA — pendiente de aprobación del propietario para autorizar la Fase 1.  
**Fecha:** 2026-11-09. **Alcance de esta revisión:** solo documentación; sin código ni cambios de base de datos.  
**Fuentes:** plan integral original del propietario (v1, 2026-11-09) + su mensaje de revisión y consolidación (mismo día) + auditoría Fase 0 (`POST_MVP_AUDIT.md`).  
**Nota de referencias:** el informe de auditoría cita la numeración de la v1 (conservada en el historial de git). Esta v2 reorganiza el contenido manteniendo sus numeraciones donde el plan original sigue vigente.

**Documentos derivados (detalle operativo):**  
`POST_MVP_BACKLOG.md` (tareas y dependencias) · `docs/visual/` (guías, tokens, componentes, referencias) · `docs/architecture/` (contenido, biblioteca, personajes, ventanas) · `DECISIONS.md` ADR-031..035.

---

# 1. Objetivo

Transformar el MVP funcional en una **aplicación VTT y de creación de universos**: crear y gestionar campañas, diseñar personajes de forma satisfactoria, crear razas/clases/habilidades/objetos/criaturas y contenido propio, construir universos y organizar su información, relacionar personajes, facciones, acontecimientos, lugares y objetos, jugar en una mesa cómoda, reutilizar contenido entre campañas y mundos, y personalizar profundamente sin complicar.

**Regla rectora:** la complejidad debe estar en las posibilidades que ofrece ProjectVTT, no en la dificultad de utilizarlo. Potente funcionalmente, sencilla de entender, agradable de usar.

# 2. Principios fundamentales

1. No sacrificar funcionalidad para simplificar visualmente.
2. Complejidad funcional alta con complejidad visual controlada.
3. Crear contenido debe resultar intuitivo y satisfactorio.
4. Personalización profunda sin obligar a utilizarla.
5. Diseñar primero los flujos y después sus interfaces.
6. Coherencia visual en toda la aplicación.
7. Sin laberintos de menús para tareas habituales.
8. La aplicación no impone estética a los universos de los usuarios.
9. La arquitectura no se limita a D&D 5e (es el primer sistema, no el último).
10. Conservar y mejorar lo existente; no reemplazar arbitrariamente.
11. **Sin catálogos provisionales**: ninguna funcionalidad se construye sobre datos provisionales que después haya que sustituir (decisión del propietario).
12. **Nada de pantallas por interpretación textual**: prototipo + captura + aprobación antes de integrar.

# 3. Conservación del MVP (obligatoria)

Se conservan sin reescritura: servidor autoritativo, permisos y validaciones, contratos, tiempo real, tiradas, combate, chat, mapas, niebla, notas, historial, persistencia, backups y pruebas. Antes de tocar cualquier parte se identifica qué se **reutiliza**, qué se **adapta** y qué es **nuevo** (el backlog lo hace explícito por tarea). Las pruebas existentes (230 backend + 79 web + smoke del escritorio) son la red de seguridad y deben seguir en verde.

# 4. Dirección de diseño e interfaz

- **Identidad:** tema oscuro de carbón (sin negro puro), acento morado apagado y moderado, superficies por tonalidades, tipografía legible, iconografía coherente, espaciado/jerarquía consistentes, decoración moderada. **Neutralidad temática**: los usuarios crean fantasía, ciencia ficción, terror, anime o lo que quieran; ProjectVTT no impone estética a sus contenidos.
- **Referencias (principios, no copias):** Inkarnate (experiencia de creación, lienzo protagonista, herramientas contextuales), Linear (jerarquía, claridad, keyboard-first), Unreal (organización de herramientas complejas, outliner + inspector + ventanas), Baldur's Gate 3 (personaje, inventario y equipamiento visuales), Alchemy (atmósfera durante la partida). Análisis extraído en `docs/visual/REFERENCE_LIBRARY.md`.
- **Proceso visual obligatorio (por pantalla):** analizar función → buscar referencias → proponer distribución → prototipo visual limitado → **capturas reales para revisión** → **aprobación del propietario antes de integrar** → corrección según captura real → registro de la decisión en `docs/visual/screens/`.
- **Sistema visual y componentes:** `docs/visual/PROJECT_VISUAL_GUIDELINES.md`, `DESIGN_TOKENS.md`, `COMPONENT_RULES.md` (incluyen la regla de **miniaturas cuadradas y compactas** para todo el contenido).

# 5. Navegación y organización

Se consolida la propuesta de la auditoría en esta arquitectura:

```text
├── Inicio/dashboard: recientes · propias · compartidas · invitaciones · actividad · crear campaña
├── Campañas: lista compacta con búsqueda (nombre, miniatura, estado, participantes, última actividad)
├── Espacio de campaña: Resumen · Mesa de juego · Escenas y mapas · Personajes/NPC/Criaturas ·
│     Biblioteca de campaña · Contenido homebrew · Historia y lore (facciones, localizaciones,
│     cronología) · Participantes y permisos · Configuración
├── Mesa de juego (workspace): tablero a sangre + herramientas en ventanas (§6)
├── Biblioteca (global) y Creación de contenido (§7)
├── Mundos reutilizables (§8)
└── Gestión: cuenta, preferencias, mis mundos
```

- **URLs reales con historial del navegador** (recarga y "atrás" funcionan) y **búsqueda global** (paleta de comandos Ctrl+K).
- **Revelación progresiva**: lo esencial siempre visible; lo contextual en el inspector; lo profundo en ventanas/biblioteca. Sin esconder funcionalidades importantes tras navegación profunda.
- El espacio de campaña incluye las capacidades ya existentes en API que hoy no tienen interfaz (renombrar/archivar campañas, invitaciones, roles y miembros) — ver auditoría §2.3.

# 6. Sistema general de ventanas

Único sistema para todas las herramientas (Character Hub, fichas de objeto, biblioteca, inspectores, chat, combate, editor…): ventanas arrastrables/redimensionables/minimizables, capas y orden por foco, posiciones/tamaños recordados, distribuciones guardables, interacción entre herramientas y **la mesa siempre accesible**. Extensible por registro de herramientas (nuevas herramientas sin rehacer el sistema). Detalle: `docs/architecture/WINDOWS.md`; decisión: **ADR-033**.

# 7. Biblioteca universal, contenido y homebrew

Pilar principal del producto. Detalle: `docs/architecture/CONTENT_MODEL.md` y `docs/architecture/LIBRARY.md`; decisiones **ADR-031** (modelo) y **ADR-035** (imágenes).

- **Sistema flexible por sistema de juego**: tipos, categorías, subcategorías, variantes, relaciones y herencia — las jerarquías del plan original son ejemplos, no estructuras fijas.
- **Editor universal** adaptado al tipo (nombre, descripción, categoría/relaciones, imagen, propiedades tipadas, estadísticas/bonificaciones, pasivas y habilidades, efectos y activaciones, requisitos y restricciones, opciones elegibles, usos/duración/costes, visibilidad y permisos, versionado).
- **Referencias visuales**: biblioteca de imágenes compartida (subir, organizar, buscar, reutilizar sin duplicar); **miniaturas cuadradas y compactas** como representación habitual de todo el contenido (razas, clases, habilidades, objetos, criaturas…); ampliación solo bajo petición.
- **Homebrew funcional**: habilidades/pasivas estructuradas (tipo, activación, efecto, duración, usos, costes, requisitos, restricciones, condiciones, automatización o resolución manual) y **elecciones configurables** (cantidad, obligatoriedad, requisitos, exclusiones, momento). El motor de reglas existente se **reutiliza y amplía**; sin reglas de raza/clase embebidas en código.
- **Permisos y versionado**: ámbitos global/usuario/mundo/campaña/DM; el DM crea, publica, concede edición, acepta propuestas, aprueba/rechaza y mantiene privacidad; el versionado **nunca cambia en silencio** el contenido que usan los personajes (detección de usos, diff, actualizar o conservar).

# 8. Mundos reutilizables

Modelo **híbrido** (preferencia del propietario): contenido compartido por referencias y versiones + modificaciones locales (overrides) + independizar/copiar explícito + control explícito de actualizaciones. Modificar una campaña no altera a otras (por construcción). Alternativas y consecuencias estudiadas en **ADR-032**.

# 9. Creador de personajes, Character Hub e inventario

Detalle: `docs/architecture/CHARACTERS.md`; decisión **ADR-034**.

- **Creador visual por pasos** (no un formulario de números): raza y variantes, clase/subclase, trasfondo, habilidades y rasgos, atributos, equipo inicial, descripción/historia/imagen, **consulta de los efectos de cada elección**, revisión y guardado con continuación posterior. Las opciones proceden del contenido publicado en la campaña y de los permisos del jugador.
- **Framework configurable por sistema** (D&D 5e es la primera implementación, no la arquitectura definitiva): pasos, tipos de contenido y validaciones se definen por datos. **Se construye sobre el modelo de contenido real** (puente previo, sin catálogos provisionales).
- **Character Hub** (ventana, sin abandonar la mesa) con Perfil, Equipo, Relaciones e Historia, coexistiendo y sincronizado con la ficha clásica; la distribución de perfil previamente aprobada se conserva como referencia; los **secretos del DM nunca se filtran** por relaciones, aconteciones ni contenido compartido.
- **Inventario y equipamiento visuales** (referencia Baldur's Gate 3): espacios de equipamiento **configurables por sistema** (sin hardcodear), miniaturas en cada hueco, ficha/inspector del objeto (imagen, tipo, rareza, estadísticas, efectos, requisitos, peso…), mochila con contenedores/bolsillos adaptada a la capacidad real, interacciones (equipar, desequipar, comparar, transferir, inspeccionar, utilizar, soltar) y **el equipamiento afecta a las reglas y estadísticas** cuando procede.

# 10. Mesa de juego

Rediseño con el tablero protagonista (distribución flexible por zonas: central/laterales/inferior, probada visualmente), herramientas del mapa auditadas y completadas (hoy: zoom/pan, tokens con snap, grid cuadrado/hex, niebla, mapa; faltan: medición, dibujo, anotaciones, capas, visibilidad, elementos interactivos) y paneles organizables **dentro del sistema de ventanas** (colapsar, redimensionar, recordar posiciones, distribuciones guardadas). El diseño exacto sale del proceso visual obligatorio (§4).

# 11. Roadmap consolidado (8 fases + puente)

Se mantienen las 8 fases con las dependencias corregidas (§13 del propietario). Detalle de tareas: `docs/POST_MVP_BACKLOG.md`.

| Fase | Contenido | Entregable |
|---|---|---|
| **0. Auditoría y plan** ✅ | Auditoría + plan consolidado + arquitectura + ADRs propuestos | Este plan (pendiente de aprobación) |
| **1. Arquitectura de experiencia** | Sistema visual (tokens+componentes), navegación con URLs reales, **sistema de ventanas**, paleta de comandos, shell de aplicación | Prototipos y arquitectura de interfaz **aprobados** |
| **2. Rediseño de la aplicación** | Inicio, campañas, espacio de campaña (incluye las capacidades ocultas de API), mesa de juego, paneles/ventanas, estados vacíos/confirmaciones/errores | Aplicación navegable y visualmente coherente (funcionalidad MVP intacta) |
| **Puente 2→3** | **Contratos y modelo mínimo de contenido** (tipos race/class/background/ability/item, revisiones, usos, imágenes básico) — antes o durante la preparación de la Fase 3 | API de contenido real validada por pruebas (sin catálogos provisionales) |
| **3. Creación de personajes** | Creador por pasos, blueprint del sistema, ficha ampliada, validaciones, Character Hub, integración con campañas | Flujo completo de creación y gestión de personajes |
| **4. Biblioteca universal** | Explorador, jerarquías/variantes, editor universal, búsqueda, relaciones, imágenes completas | Biblioteca utilizable |
| **5. Homebrew y motor de reglas** | Habilidades/pasivas estructuradas, elecciones, efectos automatizados/manuales, permisos del DM, versionado con diferencias | Contenido personalizado funcional y publicable |
| **6. Inventario y equipamiento** | Objetos como contenido, ficha de objeto, equipamiento visual, contenedores, efectos en reglas | Inventario visual e interactivo |
| **7. Mundo y herramientas avanzadas** | Facciones, relaciones, cronología, lore, localizaciones, mundos reutilizables (ADR-032), import/export, herramientas de mapa (medición, dibujo, capas) | Entorno avanzado de construcción de universos |
| **8. Calidad y lanzamiento** | Pruebas integrales, rendimiento, seguridad, accesibilidad, consistencia visual, documentación | Versión post-MVP estable (criterios §13 verificados) |

**Justificación del orden:** la Fase 1 es infraestructura de experiencia de la que depende todo el rediseño; el puente de contenido garantiza que el creador (Fase 3) nunca se apoye en datos provisionales; la biblioteca completa (Fase 4) amplía el modelo ya en producción; homebrew (5) e inventario (6) lo consumen; mundo (7) cierra la reutilización; calidad (8) remata.

# 12. Proceso de trabajo

- **Antes de tocar código:** leer la documentación, identificar reutilización/adaptación/novedad por tarea, preparar propuesta.
- **Durante:** trabajo por módulos, sin cambios masivos sin validación; pruebas siempre en verde; persistencia y permisos comprobados; estados de error revisados; decisiones documentadas; compatibilidad mantenida; sin sistemas duplicados.
- **Dirección visual:** ningún pantalla se implementa sin su propuesta aprobada con capturas (§4).
- **Multiagente (si aplica):** coordinador (arquitectura/prioridades/consistencia), UX/UI (navegación, referencias, propuestas), frontend (componentes), backend (modelos/servicios/persistencia), QA (pruebas/regresiones); especificaciones compartidas y una sola dirección visual/arquitectónica.

# 13. Criterios de aceptación del post-MVP

Los del plan original se mantienen íntegros: navegación coherente en toda la app; espacio de campaña completo; mesa cómoda y configurable; crear/gestionar personajes y consultar/editar fichas; biblioteca organizada con categorías y variantes; contenido homebrew estructurado; permisos correctos; versionado que protege el contenido existente; referencias visuales en los elementos; inventario interactivo con equipamiento que afecta a las reglas; Character Hub integrado; módulos que comparten datos; **funcionalidad del MVP conservada**; aplicación estable y cómoda; D&D 5e funcionando como primer sistema; arquitectura preparada para más sistemas.

# 14. Riesgos y compatibilidad (resumen)

Riesgo de alcance (fases con hitos aprobados), subjetividad visual (proceso de aprobación obligatorio), regresión del MVP (pruebas como red + migración por pantallas), migración de datos (`ActorItem` y catálogo ADR-015 → nuevo modelo, con pruebas de equivalencia y conservación), rendimiento con imágenes (derivados + paginación + ADR-007), paridad del escritorio (decisión pendiente) y verificación sin CI remota (decisión pendiente). Detalle en `POST_MVP_AUDIT.md` §10 y en el backlog.

# 15. Assets y contenido externo (§17 — investigación)

ProjectVTT aprovecha recursos disponibles en Internet (iconos, ilustraciones, texturas, mapas, efectos, tipografías, audio, código OSS) y **contenido real de D&D 5e**, todo con licencia verificada, sin depender solo de contenido manual y sin incumplir derechos.

- **Contenido real de D&D:** el **SRD 5.1 y 5.2.1 están bajo CC BY 4.0** (comprobado 2026-11-09): primer paquete oficial `dnd5e-srd-5.1` con razas/clases/trasfondos/**hechizos reales** (nombre, nivel, escuela, componentes, tiempos, alcance, duración y descripción según la fuente)/objetos/criaturas/condiciones; `dnd5e-srd-5.2` como segundo paquete. **Nunca** contenido de suplementos ni arte protegido; uso nominativo de marcas. Decisión **ADR-037**.
- **Paquetes con procedencia:** cada conjunto registra fuente, versión, licencia y atribución; formato `.pvttpkg` importable con identidad sin duplicados y conflictos de versión resueltos explícitamente. Decisión **ADR-036**.
- **Biblioteca de assets integrada:** importar, buscar, filtrar, organizar (categorías/etiquetas/colecciones), miniaturas cuadradas compactas, reutilización por referencia, sustitución sin perder referencias y **gestión de atribuciones y licencias** (pantalla de Créditos + exportación).
- **Criterios de selección (§17.5):** licencia y derechos primero (CC0/CC BY/MIT/OFL/royalty-free con registro), luego calidad, coherencia estética, resolución/formato, rendimiento y facilidad. Nada protegido sin autorización; nunca eliminar atribuciones.
- **Investigación completa** (fuentes clasificadas, licencias verificadas, impacto arquitectónico y plan por fases): `docs/POST_MVP_ASSETS_RESEARCH.md`. **No se incorpora nada hasta su aprobación.**

# 15b. Dirección de experiencia (decisiones del propietario, 2026-11-17)

El propietario ha precisado la dirección de creación y gestión: **composiciones distintas para crear y para gestionar**; imagen del personaje aportada por el usuario, visible en todo el recorrido; elecciones en tarjetas comprensibles para principiantes; **creador guiado de clases homebrew para DM**; lenguaje visual extendido a toda la app; **sin referencias internas en la interfaz**; estados carga/error/vacío diferenciados; y proceso de prototipo → capturas → aprobación → recorridos completos. Detalle, contraste con este plan y huecos en [`POST_MVP_EXPERIENCE_DIRECTION.md`](POST_MVP_EXPERIENCE_DIRECTION.md). Las tareas EXP-1..EXP-5 se registran en `POST_MVP_BACKLOG.md` y **no se implementan sin prototipo aprobado**.

# 16. Decisiones que requieren aprobación del propietario

**Confirmadas el 2026-11-09** (mensaje de revisión del plan): ADR-031..035 aprobados como dirección arquitectónica (sujetos a validación técnica durante su implementación); orden de fases mantenido; **web como plataforma principal del rediseño** (el escritorio se adapta después reutilizando contratos); **español primero con arquitectura preparada para i18n desde el principio**; sin publicar el repositorio (CI privada si está disponible); contenido de D&D 5e **real, oficial y legalmente utilizable** (sin catálogos provisionales ni hechizos inventados). Además se pidió y entregó la **ampliación de la investigación de assets** (§15 y `docs/POST_MVP_ASSETS_RESEARCH.md`), con tarea específica en el backlog (Workstream §17).

**Siguen pendientes:**
1. ADR-036 (paquetes con procedencia) y ADR-037 (contenido D&D vía SRD): dirección respaldada; pendientes de tu validación formal y del resultado de la investigación ampliada.
2. Versión del SRD primero (propuesta: 5.1 primero, 5.2 como segundo paquete).
3. Traducción del SRD al español: ¿traducimos el paquete (trabajo derivado permitido con atribución) o mostramos el original con notas?
4. Política sobre arte generado por IA en la biblioteca de imágenes (etiquetado de origen).
5. **Aprobación visual de pantallas** (proceso obligatorio: prototipo + capturas reales por pantalla — la aprobación de la arquitectura NO aprueba automáticamente los diseños).
6. Proveedor cloud/object storage (ADR-007) y nombre comercial/identidad visual/licencia del producto (define además qué código OSS podemos incorporar).
