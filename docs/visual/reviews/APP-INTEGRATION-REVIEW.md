# Revisión de la aplicación real — integración PMB-2.2/2.4

**Fecha:** 2026-11-09 · **Estado:** revisión técnica del agente — **pendiente la revisión conjunta con el propietario** antes de pasar a Character Hub y creación de personajes.  
**Alcance:** recorrido funcional real por la aplicación integrada (UI completa, datos reales del servidor, navegador real), no solo pruebas unitarias.

---

## 1. Recorrido funcional verificado (UI real, datos reales)

| Flujo | Resultado |
|---|---|
| **Login** por el formulario real (pantalla con logotipo) | ✅ sesión restaurada; usuario visible en el raíl |
| **Inicio**: destacadas por actividad real, con portada/miniatura, rol, participantes y «hace X» | ✅ datos del servidor (portada cargando, alternativas con inicial donde no hay) |
| **Actividad reciente** multi-campaña (`GET /activity`) | ✅ eventos reales («Escena 'La ciudadela' creada»…) |
| **Campañas**: lista densa con descripción, badge de estado, participantes y última actividad | ✅ todo real (`memberCount`, `lastActivityAt`) |
| **Diálogo de gestión** (ruleta por fila): General/Invitaciones/Participantes/Reglas | ✅ secciones cargadas; **ruleset real listado** («D&D 5e / 5.5e») |
| **Crear código de invitación** | ✅ código real aparece en la lista con badge «Activo» |
| **Renombrar + descripción** («Guardar cambios») | ✅ persistido y reflejado en la lista |
| **Búsqueda sin resultados** + «Limpiar búsqueda» | ✅ «Sin resultados para «…»» y recuperación |
| **Filtros** (Todas/Propias/Compartidas/Archivadas) con estado vacío por filtro | ✅ «Sin campañas en este filtro» |
| **Archivar con confirmación destructiva** | ✅ diálogo de confirmación → badge «Archivada» en el filtro correspondiente |

## 2. Bugs reales encontrados y corregidos durante esta revisión

1. **Las portadas no se mostraban** (ALTA): `CampaignThumb` no recibía la portada — las vistas no la cargaban. Corregido: el componente descarga la portada autenticada (con caché) y solo usa la inicial cuando no hay. *Verificado: 1 portada real + 2 iniciales correctas.*
2. **Los clics en las acciones de fila burbujeaban a la fila** (ALTA): la ruleta «Gestionar» navegaba a la campaña en lugar de abrir el diálogo (y «→» abría dos veces). Corregido en `ListRow`: el contenedor de acciones detiene la propagación. *Verificado: el diálogo abre y navega correctamente.*

Ambos demuestran el valor de la revisión sobre la app real: las pruebas unitarias y la E2E no los cubrían.

## 3. Observaciones menores (no bloqueantes)

- `Dialog` reutiliza la clase de título de `ConfirmDialog` (`pv-confirm__title`): funciona, pero conviene separar estilos cuando crezca la biblioteca.
- El código de invitación se muestra como cadena hexadecimal larga (formato del backend); futura mejora: código corto + botón «Copiar» (PMB-2.7).
- La subida de portada por el selector de archivos de la UI no se automatizó (input oculto); el endpoint está probado (4 pruebas de integración) y la carga de imagen verificada. Se probará manualmente en la revisión conjunta.

## 4. Estado de las pruebas

- Web: **119/119** · `tsc + vite build` sin errores (tras los dos arreglos).
- Backend: **234/234** (incluye la E2E del vertical slice con el diseño nuevo y las 4 pruebas de portadas/descripción/actividad).
- MVP conservado: el flujo E2E (login → campañas → escena → mover token entre dos usuarios) pasa sobre las pantallas rediseñadas.

## 5. Capturas

Pendientes: el capturador de pantalla exige la **ventana de OpenCode visible** (bloqueo del entorno, no de la aplicación). Las pantallas están verificadas funcionalmente por DOM con datos reales (este documento) y las capturas de prototipo aprobadas viven en `docs/visual/screens/`. En cuanto la ventana esté visible se archivan: `login`, `inicio`, `campanas`, `gestion-campana`.

## 6. Checklist para la revisión conjunta con el propietario

- [ ] Login con logotipo (identidad visual).
- [ ] Inicio: ¿orden de las destacadas, densidad de «Actividad», bloque «Invitaciones»?
- [ ] Campañas: miniaturas/portada, iniciales, badges, metadatos y acciones por fila.
- [ ] Diálogo de gestión: secciones, permisos (solo Owner edita), confirmaciones destructivas.
- [ ] Flujo completo: crear → describir → portada → invitar → archivar.
- [ ] Estados vacíos y «sin resultados».
- [ ] Ajustes de densidad pendientes (filas sin miniatura, decididos para después de ver la integración).
