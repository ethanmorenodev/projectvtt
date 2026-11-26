# ProjectVTT — Arquitectura del contenido (CONTENT_MODEL.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación. Decide junto al ADR-031.  
**Por qué existe:** el creador de personajes (Fase 3) **no puede construirse sobre un catálogo provisional** (decisión del propietario, §13). Este documento define el modelo mínimo de contenido que el creador consume desde el principio, y que después la Biblioteca completa (Fase 4) edita y amplía sin rehacer nada.  
**Alcance de este documento:** tipos de contenido, jerarquía, herencia, relaciones, propiedades tipadas, ámbitos/permisos, versionado, imágenes, habilidades/efectos y elecciones. La UI está en `LIBRARY.md`; el consumo por personajes, en `CHARACTERS.md`.

---

## 1. Principios del modelo

1. **Los tipos de contenido son datos, no clases de código**: cada sistema de juego define sus tipos y relaciones (requisito §5). El motor almacena, valida, hereda y versiona de forma genérica.
2. **El contenido se valida contra un esquema**: cada tipo declara sus campos tipados; el editor universal se adapta al tipo y el servidor valida todo.
3. **Nada cambia en silencio**: los personajes fijan la versión del contenido que usan; las actualizaciones son explícitas (§7).
4. **Los secretos se filtran en el servidor**: la privacidad del DM se garantiza al serializar, nunca solo en la UI (§6).
5. **Compatibilidad con el MVP**: los agregados actuales (`Actor`, `ActorItem`, `ActorEffect`, catálogo ADR-015) siguen funcionando; la migración a este modelo es gradual y con plan (§10).

## 2. Conceptos

| Concepto | Qué es | Ejemplo |
|---|---|---|
| **TypeSchema** | Definición de un tipo de contenido (campos, capacidades, icono) para un sistema de juego | `race`, `class`, `background`, `item`, `creature`, `ability`, `faction`, `location`, `event` |
| **ContentItem** | Instancia de contenido | «Alta elfa», «Espada larga», «No-muerto (variante de Ghoul)» |
| **Categoría** | Item contenedor que organiza un tipo en árbol | Razas → Humanos → Humanos de Scaldar |
| **Variante** | Item hijo que hereda de su padre | «Nórdico» dentro de «Humanos» |
| **Relación** | Enlace tipado entre items | `race → grants → ability`, `faction → locatedIn → location` |
| **Revisión** | Versión publicada e inmutable de un item | `Espada larga#3` |
| **Uso** | Fijación de un item (y su revisión) por un personaje | El personaje de Ana usa `Alta elfa#2` |
| **ImageAsset** | Imagen reutilizable de la biblioteca | Retrato compartido por 3 NPCs |

## 3. TypeSchema (tipos por sistema)

Cada sistema de juego (ruleset) **publica un paquete de tipos** (datos semilla versionadas). Un TypeSchema declara:

- `key`, `name`, `icon`, `system` (p. ej. `dnd5e`).
- **Campos tipados** con secciones (editor adaptativo): `text`, `longText`, `number`, `boolean`, `enum`, `reference` (a otro tipo), `list<T>`, `roll` (fórmula), `statModifier`.
- **Capacidades** (flags que activan comportamientos del producto): `isPlayable` (aparece en el creador), `hasAbilities`, `hasEquipment`, `isContainer`, `hasStats`, `canBeToken`…
- **Relaciones permitidas** (p. ej. `race → grants → ability`).
- **Reglas de validación** (requeridos, rangos, exclusiones).

Los mundos/campañas pueden **añadir campos personalizados** a un tipo (extensibilidad) sin redefinir la semántica del motor.

**Alcance del "mínimo para el creador" (puente Fase 2→3):** `race` (+ variantes), `class` (+ subclases), `background`, `ability`, `item` (equipo inicial), `creature`/`npc` (marcados). El resto de tipos se añade en Fase 4–7 **sin tocar este modelo**.

## 4. ContentItem (forma)

```
ContentItem {
  id, system, typeId,
  name, summary, description,          // description: texto enriquecido futuro
  parentId,                            // jerarquía (categoría/variante); null = raíz del tipo
  tags[], imageId?, focal?,
  properties: { [fieldKey]: valor tipado },   // validado contra TypeSchema
  inheritance: { [fieldKey]: "inherit" | "override" | "extend" },
  scope, grants[],                     // §6
  workflowState,                       // draft | proposed | published
  currentRevision, createdBy, timestamps
}
```

- **Jerarquía:** el árbol es por tipo (`parentId` solo apunta a items del mismo tipo o de tipo "categoría"). La navegación "Razas → Humanos → variantes" es una consulta de árbol, no un modelo aparte.
- **Borrado:** eliminar un item con hijos está **bloqueado (409)** — `ParentId` no tiene FK y un padre borrado dejaría hijos colgantes e invisibles en el árbol. Primero se reorganiza (descolgar o eliminar los hijos). La profundidad máxima del árbol es de **64 nodos** contando al hijo (ciclos y límite validados al guardar).
- **Propiedades tipadas:** valores primitivos + referencias + listas + modificadores de estadística + fórmulas de tirada. La validación es del servidor (rangos, tipos, referencias existentes).

## 5. Herencia (variantes)

- Un item hijo hereda las propiedades efectivas de su padre según el modo por campo:
  - `inherit`: usa el valor del padre (si el padre cambia, la variante cambia — salvo que un personaje fije revisión, §7).
  - `override`: sustituye el valor.
  - `extend`: acumula (listas y modificadores se suman; p. ej. bonificaciones).
- La **resolución efectiva** se calcula al leer (padre → hijo) y se **materializa en un snapshot** cuando un personaje usa el contenido (§7). Así la edición del árbol nunca rompe personajes existentes.
- Ciclos prohibidos (validación al guardar).

## 6. Ámbitos, permisos y privacidad

**Ámbitos (scope):** `global` (oficial/compartido de solo lectura), `user` (personal), `world` (compartido por campañas de un mundo), `campaign`, `dm` (exclusivo del DM dentro de la campaña).

**Permisos:**
- Visibilidad/edición: ámbito + `grants[]` (usuario/rol → `view`/`edit`).
- Flujo del DM (§11): crear → **publicar** para la campaña → conceder edición o creación a jugadores → aceptar **propuestas** de jugadores → aprobar/rechazar → mantener información privada.
- Estado de publicación (`workflowState`): `draft` (solo autor/DM), `proposed` (espera revisión), `published` (disponible según scope/grants).
- **Privacidad del DM:** los campos/items marcados como privados se **eliminan en la serialización del servidor** hacia jugadores; ninguna relación, acontecimiento ni contenido compartido puede filtrarlos (requisito §8 del plan). Regla transversal: *la secrecía se decide en el mapeo a DTO, probado con pruebas de no-filtración*. El filtro cubre **toda la cadena de herencia**: los campos privados de cualquier ancestro se eliminan también de `effective` (revisiones) y de los snapshots de uso, incluida la respuesta inmediata de creación de uso.
- **Contención de audiencia en la jerarquía (PMB-4.3):** un hijo nunca puede ser menos restrictivo que sus ancestros, porque sus valores efectivos se materializan para la audiencia del hijo. Clases de audiencia (de mayor a menor): `everyone` (global publicado) ⊃ `members` (campaña publicada y no privada) ⊃ `gms` (ámbito `dm`, o campaña en borrador/privada) ⊃ `owner` (personal). Se valida **al enlazar** (`parentId` al crear/mover: toda la cadena contra el hijo y su subárbol), **al publicar** (borrador → miembros), **al marcar un padre como privado** (su subárbol) y **al quitar la marca de privado** (su cadena). La importación solo crea items globales publicados, así que la contención se cumple por construcción. Items de ámbito personal (`user`) no pueden ser padres de contenido de campaña/`dm`.

## 7. Versionado (obligatorio)

- Cada publicación crea una **revisión inmutable** (`ContentRevision{itemId, number, payload, author, createdAt}`) con mensaje de cambio opcional.
- **Los usos fijan revisión:** cuando un personaje elige contenido (raza, clase, objeto…) guarda `ItemUsage{actorId, itemId, revisionNumber, snapshot}`. El snapshot materializa las propiedades efectivas del momento (herencia resuelta).
- Cuando el item publica una revisión nueva:
  1. La app detecta qué personajes usan revisiones anteriores.
  2. Muestra **qué ha cambiado** (diff de propiedades afectadas).
  3. Ofrece **actualizar** (re-materializa el snapshot y recalcula) o **conservar** la versión anterior.
  4. **Nunca hay actualización silenciosa** (requisito §11 del plan).
- Los datos de personaje derivados del snapshot se recalculan en servidor al actualizar (auditable).

## 8. Relaciones

`ContentRelation{fromItemId, kind, toItemId, label?, metadata?}` con `kind` definido por el sistema (`grants`, `requires`, `excludes`, `memberOf`, `locatedIn`, `leadsTo`, `owns`…). Se usan para: requisitos/elecciones del creador, grafo del Character Hub (Relaciones), y navegación de lore (Fase 7). Se validan contra las relaciones permitidas por tipo.

## 9. Habilidades, efectos y elecciones (base de la Fase 5)

Los items de tipo `ability` (y las propiedades `hasAbilities`) declaran estructura real, no texto:

- `activation`: `action | bonusAction | reaction | passive | free`.
- `effects[]`: efectos **tipados** que el motor sabe resolver (ampliación del catálogo ADR-015 a datos: modos de tirada, modificadores, cambios de recurso, aplicación de condiciones) y efectos **`manual`** para lo no soportado (resolución asistida, sin fingir automatización).
- `duration`, `uses` (por descanso/escena/día), `costs`, `requirements`, `restrictions`, `conditions`.
- **ChoiceGroups** (elecciones configurables): `options[]` (referencias o inline), `count`, `mandatory`, `timing` (creación/subida de nivel/libre), `constraints` (requisitos/exclusiones), `changeable`. Ejemplo del plan: una raza concede una pasiva automática + elegir 1 entre 3.

Estos campos ya viven en el modelo desde el principio (el creador de la Fase 3 los muestra), aunque el **motor ampliado** que los resuelve se implementa en Fase 5 (ADR-031 deja abierto ese ADR específico).

## 10. Imágenes (biblioteca compartida)

- `ImageAsset{id, ownerId, fileName, contentType, sizeBytes, focal, tags[], albums[], variants}`. Los items referencian `imageId` (**sin duplicar archivos**, requisito §6).
- **Variantes cuadradas** generadas al subir: `thumb-sm 64`, `thumb-md 128`, `thumb-lg 256` + original (recorte `cover` con punto focal; ver `DESIGN_TOKENS.md` §11).
- Almacenamiento vía `IAssetStore` (ADR-028) bajo `images/{id}/{variante}`; S3/MinIO cuando se confirme ADR-007.
- **Generación de derivados en cliente** (canvas) con validación en servidor de cada variante (firma/tamaño/tipo) — decisión ADR-035: evita añadir dependencia de imágenes al servidor; alternativa server-side (SkiaSharp) documentada si aparece abuso.
- UI de selección: biblioteca de imágenes con subir / organizar (álbumes, etiquetas) / buscar / reutilizar (`LIBRARY.md`).

## 11. Encaje con el MVP (compatibilidad)

| Hoy (MVP) | Con este modelo | Migración |
|---|---|---|
| `Actor` (nombre + atributos + recursos) | `Actor` se amplía: `kind` (pc/npc/creature), `portraitId`, `biography`, `systemData`, usos de contenido | Aditiva; los campos actuales siguen siendo válidos |
| `ActorItem` (nombre/cantidad/notas) | Ítems del personaje referencian `ContentItem` de tipo `item` (con snapshot); se conserva la lista simple como modo legado | Fase 6: conversión con conservación de datos |
| `ActorEffect` + catálogo ADR-015 | Estructura `ability.effects` + motor ampliado; el catálogo actual se convierte en datos | Fase 5: conversión con prueba de equivalencia |
| Mapas, tokens, combate, chat, niebla, notas, historial | **Sin cambios** (conservación del MVP, §14) | — |

## 12. Cambios de datos necesarios (resumen para el ADR)

Nuevos agregados: `ContentType` (semilla por sistema), `ContentItem`, `ContentRevision`, `ItemUsage`, `ContentRelation`, `ImageAsset`, `Grant`, `ChoiceGroup` (como propiedad estructurada), `ActorExtension` (kind, portrait, bio, systemData), `EquipmentSlot` (definición por sistema, Fase 6). Ampliación de `Actor` y de la API (`/content`, `/images`, `/characters/build`). Nada de esto toca las tablas del MVP fuera de la extensión aditiva de `Actor`.

## 13. Paquetes de contenido y procedencia (§17)

Todo el contenido y los assets pueden agruparse en **paquetes** importables/exportables con metadatos de procedencia: fuente, versión, licencia, URL de licencia y atribución (`ContentPack`, ADR-036). Los elementos pertenecen opcionalmente a un paquete y heredan su licencia; las imágenes añaden `license/attribution/origin/author` (ADR-035/036). El formato `.pvttpkg` (manifest + contenido + assets + licencias) importa sin duplicar — identidad `(paquete, key)` — y resuelve conflictos de versión de forma explícita. El primer paquete oficial es `dnd5e-srd-5.1` (contenido real de D&D bajo CC BY 4.0, ADR-037): los tipos del sistema se **siembran** desde paquetes, no desde código. La pantalla de Créditos y licencias expone y exporta las atribuciones. Detalle: `docs/POST_MVP_ASSETS_RESEARCH.md`.

### Categorías en el servidor (PMB-4.3, 2026-11-11)

La alternativa «padre de tipo categoría» del apartado 4 se representa mediante un ContentType con key `category` dentro del sistema correspondiente. Se reutilizan TypeId, ParentId y el esquema existente; no hay campos HTTP nuevos ni migraciones. Una categoría organizativa puede declarar un esquema sin propiedades y contener otras categorías. Las variantes conservan su tipo propio. La misma comprobación de compatibilidad se aplica en CRUD e importación de paquetes: padre del mismo tipo o categoría, siempre del mismo sistema. Los permisos y la validación de ciclos del CRUD siguen aplicándose a toda la cadena.

Este registro resuelve la representación que quedó pendiente en las continuaciones previas. La creación y navegación visual de categorías corresponden al editor/explorador; este cambio entrega el soporte de servidor, sin añadir pantallas ni sembrar contenido ficticio.
