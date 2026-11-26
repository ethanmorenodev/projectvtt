# ProjectVTT — Creación de personajes, Character Hub e inventario (CHARACTERS.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación. Decide junto al ADR-034.  
**Relación con el modelo de contenido:** este documento consume `CONTENT_MODEL.md` (el creador se alimenta de contenido real publicado en la campaña — **sin catálogos provisionales**, decisión del propietario §13).

---

## 1. Framework de creación configurable por sistema

**No se construye un asistente de D&D 5e**: se construye un **framework de creación** que cada sistema de juego describe con datos (`CharacterBlueprint`). D&D 5e es la primera implementación.

### 1.1 CharacterBlueprint (datos por sistema)

```
CharacterBlueprint {
  system, key,
  steps: [                       // orden del asistente
    { key, title, kind,          // kind: identity | choice | attributes | equipment | text | review
      contentType?,              // para steps de elección (race/class/background…)
      choiceGroupRef?,           // elecciones configurables (CONTENT_MODEL §9)
      fields?,                   // campos propios del paso (identidad, descripción…)
      validation?,               // requisitos mínimos (p. ej. "elegir 1 raza")
      optional, skippable }
  ],
  derivedStats,                  // qué estadísticas deriva el motor del build
  startingEquipmentRules         // reglas de equipo inicial por sistema
}
```

El asistente es **genérico**: renderiza pasos desde la configuración, recoge selecciones, muestra el efecto de cada elección (bonificaciones, pasivas, requisitos), valida restricciones y permite **guardar y continuar después** (borrador persistente).

### 1.2 Flujo de creación (experiencia)

1. Elegir sistema (si la campaña tiene varios; si no, preseleccionado).
2. Identidad (nombre, retrato, descripción, personalidad).
3. Elecciones de contenido: raza/variante → clase/subclase → trasfondo… desplegando las **variantes disponibles en la campaña** (contenido publicado por el DM + contenido permitido al jugador), con sus efectos visibles.
4. Atributos (método definido por el sistema: manual, compra de puntos, generación…).
5. Habilidades y rasgos (incluye las **elecciones configurables** — p. ej. elegir 1 de 3 pasivas).
6. Equipo inicial (reglas del sistema + contenido tipo `item`).
7. Historia, descripción e imagen.
8. **Revisión**: resumen de decisiones con sus consecuencias; guardar y poder volver.

### 1.3 Reglas de permisos en la creación

- El jugador solo ve contenido **publicado en la campaña** y **permitido por permisos** (`grants`, §6 del modelo). Lo privado del DM nunca aparece.
- El DM crea/publica contenido y puede abrir "propuestas" de jugadores (aprobar/rechazar) — mismo flujo del modelo de contenido.
- Las validaciones de requisitos/restricciones ocurren en **servidor** (la UI solo guía).

## 2. Arquitectura de datos del personaje

- **`Actor` se amplía (aditivo)**: `kind` (`pc` | `npc` | `creature`), `portraitId`, `biography`, `personality`, `level`, `systemData` (snapshot tipado por sistema), `build` (borrador: paso actual + selecciones, para "guardar y continuar").
- **`ItemUsage`** fija revisiones de contenido (raza/clase/trasfondo/habilidades/objetos) con snapshot materializado (modelo §7): el personaje es estable aunque el contenido cambie.
- **Estadísticas derivadas**: el servidor calcula el resumen (`derivedStats`) al guardar y al actualizar usos; previsualización en vivo durante la creación vía endpoint de cálculo (`POST /characters/build/preview`) — **la regla vive solo en el servidor**, la UI no reimplementa el motor (mismo principio del MVP: la aleatoriedad y las reglas no viven en el cliente).
- **Eventos en vivo**: se reutilizan `ActorCreated`/`ActorUpdated`/`InventoryUpdated`/`EffectsUpdated`; se añaden los que hagan falta siguiendo el mismo envelope (ADR-010) y anunciándolo (contratos compartidos).

## 3. Character Hub (ventana tipo menú de RPG)

La **ficha clásica** (hoy `ActorPanel`) y el **Character Hub** coexisten y están **sincronizados**: ambos leen/escriben el mismo estado (`actorStore` ampliado) y se actualizan por eventos — no hay dos modelos ni doble verdad.

Secciones (distribución de perfil previamente aprobada por el propietario — referencia vinculante, `REFERENCE_LIBRARY.md` §7):

| Sección | Contenido |
|---|---|
| **Perfil** | Identidad, atributos, estadísticas derivadas, personalidad, estado (recursos actuales/máximos, condiciones) |
| **Equipo** | Ilustración 2D central (silueta con espacios de equipamiento), inventario, mochila, contenedores, objetos interactivos |
| **Relaciones** | Personajes, NPC, facciones, conexiones y relaciones entre campañas (grafo desde `ContentRelation`) |
| **Historia** | Historia personal, acontecimientos, cronología, lore compartido y **lore privado del DM** (solo visible para el DM) |

**Integración con el sistema de ventanas**: el Hub es una ventana (`WINDOWS.md`) — el jugador no abandona la mesa. Las secciones pueden abrirse también como ventanas hermanas (p. ej. solo Equipo).

**Privacidad (obligatoria):** los secretos del DM se filtran **en el servidor** al serializar; ninguna relación, acontecimiento ni contenido compartido puede exponerlos (pruebas de no-filtración en la suite).

## 4. Inventario y equipamiento visual (referencia Baldur's Gate 3)

- **Espacios de equipamiento configurables por sistema** (`EquipmentSlot` como datos: id, etiqueta, icono, qué tipos acepta) — **no hardcodeados** (requisito §9 del plan). Ejemplo 5e: arma principal, secundaria, armadura, casco, guantes, botas, accesorio 1/2.
- La **silueta/ilustración 2D central** del personaje muestra cada hueco con la miniatura del objeto equipado (`thumb-sm`, cuadrada).
- **Click en un hueco/objeto → ficha contextual** (inspector o ventana): nombre, imagen, tipo, rareza, descripción, estadísticas, efectos, requisitos, peso/volumen si el sistema los usa.
- **Mochila y contenedores**: el inventario organiza en contenedores/bolsillos (el tipo `item` tiene la capacidad `isContainer`); la mochila se adapta a la capacidad real del personaje (peso/volumen si el sistema lo define — §12.4 del plan, opcional por sistema).
- **Interacciones**: equipar, desequipar, comparar, transferir, inspeccionar, utilizar, soltar (las disponibles dependen del contexto y permisos).
- **Equipar afecta a las reglas**: las propiedades/efectos del objeto se suman a las estadísticas derivadas (hook del motor `ApplyEquipment` en la Fase 6); visible en el resumen del personaje y auditable.

## 5. Encaje con el MVP

| Hoy | Con este diseño | Riesgo de migración |
|---|---|---|
| `ActorPanel` (ficha básica) | Se conserva como **ficha clásica**; el Hub es una vista nueva sobre el mismo estado | Bajo (mismo store/eventos) |
| `ActorItem` (lista simple) | Inventario visual sobre `ContentItem` + `ItemUsage`; la lista simple queda como modo legado hasta la conversión (Fase 6) | Medio (migración de datos, con conservación) |
| `ActorEffect` + catálogo ADR-015 | Estructurado en `ability.effects` (Fase 5) | Medio (prueba de equivalencia obligatoria) |
| Tiradas (`RollService`) | Sin cambios; el creador y el Hub las usan tal cual | Ninguno |
