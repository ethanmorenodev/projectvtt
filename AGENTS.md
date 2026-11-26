# AGENTS.md — Instrucciones para agentes de ProjectVTT

## 1. Misión

Trabaja como parte de un equipo de ingeniería que construye ProjectVTT. Tu prioridad es entregar cambios pequeños, correctos, comprobables y fáciles de mantener. No optimices para producir mucho código; optimiza para producir software que funcione y pueda evolucionar.

## 2. Antes de modificar código

1. Consulta [`docs/INDEX.md`](docs/INDEX.md) y lee **solo las secciones necesarias** de los documentos relevantes para la tarea (lectura bajo demanda; no cargues archivos grandes completos como `DECISIONS.md` o `BACKLOG.md` por rutina).
2. Inspecciona el estado real del repositorio. No presupongas que la estructura o las dependencias descritas ya existen.
3. Identifica el módulo y los contratos afectados.
4. Comprueba si existe una decisión arquitectónica que gobierne el cambio.
5. Si falta una decisión esencial, detente y pregunta al coordinador/propietario o registra la cuestión como pendiente. No inventes una decisión irreversible.

## 3. Reglas obligatorias

- No cambies contratos compartidos sin coordinarlo con los demás componentes.
- No dupliques reglas de negocio en web, escritorio y backend.
- El servidor valida permisos, reglas y cambios de estado compartido.
- No confíes en validaciones hechas únicamente en el cliente.
- No introduzcas microservicios para el MVP.
- No añadas dependencias sin explicar por qué son necesarias.
- No actualices versiones principales de dependencias sin autorización.
- No guardes secretos, tokens, claves ni credenciales en el repositorio.
- No ejecutes código arbitrario de plugins de terceros en el backend.
- No borres ni sobrescribas trabajo ajeno.
- No reformatees archivos completos si el cambio no lo requiere.
- No afirmes que una prueba ha pasado si no la has ejecutado.
- No ocultes errores, limitaciones ni tareas incompletas.

## 4. Flujo de trabajo

Para cada tarea:
1. Resume el objetivo y los archivos que esperas modificar.
2. Revisa el código y patrones existentes.
3. Propón un plan breve.
4. Implementa el cambio mínimo completo.
5. Añade o actualiza pruebas.
6. Valida agrupando: primero las pruebas específicas del módulo afectado; `dotnet build`/`npm run build` solo cuando el alcance o el riesgo lo justifique, y compilación completa solo en cambios transversales. No hay lint configurado (`npm run build` ya ejecuta `tsc --noEmit`). Si un comando falla, analiza el error antes de repetirlo.
7. Revisa el diff buscando regresiones, secretos o cambios accidentales.
8. Actualiza documentación si cambia el comportamiento o un contrato.
9. Informa de archivos modificados, comandos ejecutados, resultados y riesgos pendientes.

## 5. Definición de terminado

Una tarea solo está terminada cuando:
- Cumple todos sus criterios de aceptación.
- Compila/build correctamente en el ámbito afectado.
- Las pruebas relevantes pasan.
- Los errores se manejan de forma explícita.
- La autorización y validación están implementadas donde corresponda.
- La documentación/contratos están actualizados.
- No quedan `TODO`, mocks o datos ficticios en el camino funcional, salvo que estén claramente etiquetados y aceptados.
- El resumen final distingue lo probado de lo que no se ha podido probar.

## 6. Coordinación multiagente

- Un agente coordinador mantiene el backlog, define interfaces y resuelve conflictos.
- Asigna a cada agente un módulo o conjunto de archivos claramente delimitado.
- Evita que dos agentes editen simultáneamente el mismo archivo.
- El agente que modifica contratos compartidos debe anunciar el cambio y obtener aprobación.
- Integra cambios mediante ramas/commits pequeños y revisables.
- No hagas cherry-pick, merge, push ni operaciones destructivas sin autorización explícita del propietario, salvo que el flujo del repositorio lo autorice claramente.

## 7. Roles y agentes

- **Coordinator**: lo ejerce el agente primario de la sesión. Mantiene el backlog (`docs/POST_MVP_BACKLOG.md`), contratos y prioridades; descompone tareas en pasos pequeños; integra resultados; no implementa grandes funcionalidades en paralelo con los demás agentes.
- **Perfiles delegables** (`.opencode/agents/`): `developer` (implementa cambios acotados), `qa` (ejecuta y diagnostica pruebas; no edita código) y `architect` (revisión arquitectónica de solo lectura). Para explorar código sin delegar razonamiento está el agente integrado `explore`.
- **Delegación**: no crear subagentes para tareas que el agente primario resuelva eficientemente; priorizar ejecución secuencial cuando el paralelismo genere conflictos o duplicación; transferir solo resultados breves (§9), nunca documentos completos ni logs extensos.

### Reparto por dominio (referencia)

- **Backend** (`backend/`): API, identidad, campañas, permisos, persistencia y tiempo real; mantiene el servidor como autoridad del estado compartido; añade pruebas unitarias e integración.
- **Web** (`apps/web/`): React/TypeScript/PixiJS, interfaz, mapa, interacción y accesibilidad; consume contratos; no implementa reglas autoritativas.
- **Escritorio** (`apps/desktop/`): Godot + C# sobre los mismos contratos y servicios que la web; no crea una segunda implementación de reglas de negocio.
- **QA**: diseña casos, automatiza E2E y busca regresiones (permisos, reconexión, concurrencia, estados límite); no modifica arquitectura sin coordinarlo.

## 8. Seguridad

- Verifica autorización por recurso y por acción.
- Valida tamaño, formato y contenido de archivos subidos.
- Evita path traversal, inyección, IDOR y exposición de información entre campañas.
- Usa hashing de contraseñas con una solución mantenida y estándar; nunca inventes criptografía.
- Mantén secretos fuera de logs y commits.
- Añade rate limits a endpoints sensibles cuando corresponda.
- No confíes en IDs enviados por el cliente como prueba de autorización.

## 9. Comunicación del agente

Al terminar, responde con:
- **Resultado:** qué se ha implementado.
- **Archivos:** lista de archivos modificados.
- **Verificación:** comandos y pruebas ejecutados con resultado real.
- **Pendiente:** limitaciones, decisiones necesarias y riesgos.
- **Siguiente tarea:** propuesta concreta, sin empezar trabajo adicional no solicitado.
