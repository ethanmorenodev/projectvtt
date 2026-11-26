# PMB-4.4 — Prototipo del editor universal de contenido

**Fecha:** 2026-11-11 · **Estado:** ✅ **aprobado por el propietario 2026-11-11** («Aprobado — construir el editor»); implementación real a continuación.
**Base:** arquetipo «Editor» de `docs/architecture/LIBRARY.md` §3 + frame de tres zonas aprobado en PMB-4.1 (`pmb-4.1-prototipo.md`). **Ruta dev:** `#prototipo-editor[-error|-nuevo|-solo-lectura]`.

## 1. Referencias y principios extraídos

- **Unreal Engine** (`REFERENCE_LIBRARY.md` §3): edición en contexto — el outliner (aquí, el árbol de tipos) permanece visible mientras se edita; los paneles laterales cambian según la selección.
- **Linear** (§2): superficies tranquilas y jerarquía clara para densidad alta; cabecera con ruta («Biblioteca / Razas / Hill Dwarf de Scaldar»).
- **Inkarnate** (§1): modo edición explícito frente a modo uso — chip de estado «Editando / Nuevo / Solo lectura» en cabecera y controles desactivados cuando no corresponde editar.
- `COMPONENT_RULES.md` §3/§8: etiqueta encima + ayuda/error debajo, **una acción primaria por superficie** (Guardar borrador; Publicar vive en el panel derecho), inspector derecho para estado/contexto.

## 2. Composición (tres zonas, guía visual §3)

- **Izquierda — árbol de tipos (contexto)**: mismos 3 tipos del explorador (`Razas 19` — incluye el homebrew en edición —, `Clases 12`, `Trasfondos 13`), jerarquía Dwarf → Hill/Mountain Dwarf con el item abierto seleccionado. El árbol no se pierde al editar (principio Unreal).
- **Centro — formulario por secciones** (las 7 de LIBRARY §3):
  1. **Identidad** — nombre (≤200), resumen, descripción, etiquetas e imagen principal (campo honesto desactivado: PMB-4.7).
  2. **Organización** — categoría/variante (padre) con el árbol de razas real, relaciones (desactivado: PMB-4.6).
  3. **Propiedades** — **se adapta al TypeSchema del tipo**; para `race`: bonificaciones de atributo (listas por fila), velocidad (número), tamaño (texto), idiomas y rasgos (listas). Cada campo lleva su **control de herencia** (Heredar · Sobrescribir · Extender) cuando hay padre, con caja heredada y ayudante de qué hereda del padre. La cabecera recuerda que el servidor valida contra el esquema.
  4-6. **Capacidades · Requisitos y restricciones · Opciones elegibles** — secciones futuras visibles pero desactivadas, con su hito (Fase 5 / PMB-5.2 / PMB-5.3).
  7. **Visibilidad y permisos** — ámbito (select en creación con las opciones reales desactivadas según biblioteca; chip bloqueado al editar porque la API no lo permite cambiar), «Solo DM» + campos privados, con la nota de filtrado en servidor (ADR-031 §6).
  - **Pie pegajoso** con «Cancelar» + única primaria (Guardar borrador / Crear borrador) e indicador de cambios sin guardar.
- **Derecha — «Estado y versionado»**: previsualización en vivo (miniatura con inicial + nombre + resumen + chips tipo/ámbito/estado/rev./etiquetas), **procedencia** (paquete `dnd5e-srd-5.1` · v5.1 · CC BY 4.0 + atribución completa para SRD; «homebrew de campaña · autor DM» para el propio; «biblioteca personal» al crear), **revisiones** (historial con fecha y mensaje) y **acciones**: Publicar (con explicación de revisiones, ADR-031 §7) y Duplicar / Ver usos / Proponer desactivados con su hito (PMB-4.5 / PMB-4.8).

## 3. Datos honestos

Todo sale del seed real `Seed/dnd5e-srd-5.1.json` y de los contratos vigentes:

- **Esquema real de `race`**: `abilityBonuses · speed · size · languages · traits` (todos obligatorios en el seed).
- **Herencia real**: `speed`/`size` sin modo propio → **heredan por defecto** (`ContentResolver`: modo por defecto `inherit`); `abilityBonuses/traits/languages → extend` igual que Hill Dwarf en el seed; valores mostrados del padre Dwarf reales (Constitution +2, 25 ft, Medium, Common · Dwarvish, 5 rasgos).
- **Errores reales del servidor**: la variante `error` elimina `speed` y `size` (claves ausentes → el servidor rechaza con «Falta la propiedad obligatoria 'speed'/'size'», `ContentSchema`), enfocando el primer campo inválido (COMPONENT_RULES §3).
- **Solo lectura real**: `CanEdit` devuelve `false` para alcance `global` → el SRD oficial se muestra desactivado («Duplicar llega con PMB-4.5»).
- **Sin promesas**: imagen/relaciones/duplicar/usos/proponer/capacidades aparecen pero desactivados con su hito; sin datos inventados de esos flujos.
- Único item ficticio: el homebrew **«Hill Dwarf de Scaldar»** (etiquetado como homebrew de campaña en árbol, cabecera y procedencia).

## 4. Variantes y capturas

| Ruta dev | Variante | Captura |
|---|---|---|
| `#prototipo-editor` | Homebrew editable con herencia y panel de estado completo | `pmb-4.4-editor-normal-2026-11-12.jpeg` |
| `#prototipo-editor-error` | Fallo de guardado: banner de alerta + mensajes reales de `ContentSchema` + foco | `pmb-4.4-editor-error-2026-11-12.jpeg` (campos) y `pmb-4.4-editor-error-banner-2026-11-12.jpeg` (banner) |
| `#prototipo-editor-nuevo` | Creación: título «Nuevo contenido», ámbito personal (único posible en biblioteca global), sin revisiones | `pmb-4.4-editor-nuevo-2026-11-12.jpeg` |
| `#prototipo-editor-solo-lectura` | SRD oficial (`Hill Dwarf`, ámbito global): controles desactivados, procedencia CC BY 4.0, «Publicar» desactivado | `pmb-4.4-editor-solo-lectura-2026-11-12.jpeg` |

Capturas reales (Chromium 1440×900) servidas por el dev server (`import.meta.env.DEV`: las rutas de prototipo no existen en `vite preview`). **Nota de proceso:** la herramienta de navegador no está disponible en esta sesión; se usó Playwright (mismo motor que el proyecto E2E) mediante un test temporal, borrado tras capturar.

**Ajuste 2026-11-12 (petición del propietario):** el pie pegajoso dejaba ver el contenido por debajo al hacer scroll. Causa: `padding-bottom` en `.editor-form` — el `sticky` está limitado por la caja de contenido del scroller, así que el pie se detenía 16 px por encima del borde y esa franja mostraba el contenido. Corrección: sin padding inferior en el scroller (el aire lo dan el `gap`, el `border-top` y el padding del propio pie) y `background` opaco a sangre. Medición tras la corrección: pie de `bottom = 900` = fondo del scroller (sin franja).

## 5. Verificación

- `EditorPrototype.test.tsx`: **6 pruebas** — secciones de LIBRARY §3 + esquema real de `race`; herencia por defecto y cambio de modo por campo con su ayudante; previsualización viva + «cambios sin guardar»; variante error (banner, mensajes exactos del servidor, foco); variante nuevo (ámbito `user`, procedencia y revisiones vacías); variante solo lectura (controles desactivados, sin primaria de escritura).
- Suite web **161/161**, `npx tsc --noEmit` limpio y `npm run build` limpio.

## 6. Brecha de contrato detectada (al implementar tras la aprobación)

- ~~**`isPrivate` / `privateFields` no están en `ContentItemDto`**~~ → **resuelto:** aditivo aprobado por el propietario («Aprobar el aditivo») e implementado: campos en `ContentContracts.cs`, mapeo en `ContentService.ToDtoAsync` (claves privadas solo hacia quien ve los valores) y prueba de no-filtración en `tests/Integration/ContentPrivacyTests.cs`.
- Sin endpoints aún (pertenecen a otros hitos): imágenes (PMB-4.7), relaciones (PMB-4.6), duplicar/usos (PMB-4.5), propuestas (PMB-4.8), capacidades/ChoiceGroups (Fase 5). El prototipo los muestra desactivados y etiquetados.

## 7. Decisión del propietario

✅ **2026-11-11 — Aprobado** («Aprobado — construir el editor»): composición de tres zonas con formulario por secciones adaptativo al esquema, control de herencia por campo y panel de estado/versionado es la dirección aceptada; no se pidieron ajustes. **Siguiente:** implementación del editor real sobre la API, coordinando antes el aditivo `isPrivate`/`privateFields` de `ContentItemDto` (§6).

📌 **2026-11-12 — Ajuste visual del pie pegajoso** (petición del propietario: el contenido se veía a través del pie): corregido en `views/editor.css` (§4) y capturas regeneradas; la composición aprobada no cambia.
