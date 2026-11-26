# PMB-3.1 — Mapa de pasos del creador de personajes

**Fecha:** 2026-11-10 · **Estado:** ⬜ **pendiente de aprobación del propietario** (tras aprobarlo, PMB-3.4 hace el prototipo visual por pasos → capturas → aprobación).
**Base:** `docs/architecture/CHARACTERS.md` §1 (ADR-034 confirmada el mismo día). **Referencia de estilo:** creación asistida paso a paso de Nivel20 (referencia UX del propietario; nuestro creador es **genérico por datos**, no un clon de su implementación).

## 1. Entradas al creador

| Desde | Acción | Notas |
|---|---|---|
| Espacio de campaña → **Personajes** | Botón **«Crear personaje»** | Entrada principal; el personaje pertenece **solo a esa campaña** (petición del propietario; `Actor.campaignId`). |
| Mesa de juego → ventana **Ficha** | «Crear personaje» | Mismo creador; no duplica lógica. |
| Character Hub (PMB-3.6) | «Nuevo personaje» | Fase 3 avanzada. |

**Contenedor propuesto:** ruta completa dentro del espacio de campaña (`/campaigns/{id}/characters/new`), no ventana — los pasos necesitan ancho y el borrador persiste al salir. *(Decisión a validar en la aprobación de este mapa.)*

## 2. Mapa de pasos (blueprint D&D 5e)

El orden real lo dicta `CharacterBlueprint` (datos); este mapa es el blueprint inicial propuesto para 5e. Cada paso muestra **el efecto de la elección** (bonificaciones, pasivas, requisitos) — el servidor calcula y valida (endpoint de previsualización); la UI solo guía.

| # | Paso (`kind`) | Qué recoge | Fuente de opciones | Efectos visibles | Validación (servidor) | ¿Saltable? |
|---|---|---|---|---|---|---|
| 0 | **Sistema** | Sistema de juego | Reglas de la campaña | — | La campaña tiene sistema | Solo si hay varios |
| 1 | **Identidad** (`identity`) | Nombre, retrato, descripción, personalidad | — | — | Nombre obligatorio | No |
| 2 | **Raza/variante** (`choice`) | Raza + variante | Contenido publicado en la campaña **y permitido al jugador** (grants) | Bonificaciones de atributos, rasgos, idiomas | Exactamente 1; requisitos cumplidos | No |
| 3 | **Clase/subclase** (`choice`) | Clase + subclase (al nivel elegido) | Ídem | Dados de golpe, salvaciones, habilidades | Exactamente 1 (+ subclase si aplica) | No |
| 4 | **Trasfondo** (`choice`) | Trasfondo + competencias/elegibles | Ídem | Competencias, herramientas, rasgo | Exactamente 1 + elecciones del grupo | No |
| 5 | **Atributos** (`attributes`) | Valores por método del sistema | Método definido en el blueprint (manual / compra de puntos / generación) | Modificadores en vivo | Rangos y puntos del método | No |
| 6 | **Habilidades y rasgos** (`choice`) | Elecciones configurables (p. ej. 1 de 3 pasivas) | Grupos de elección del contenido | Descripción + efecto de cada opción | Cuotas del grupo (1 de 3…) | Opcionales sí |
| 7 | **Equipo inicial** (`equipment`) | Equipo según reglas + `item` | Reglas del sistema + contenido tipo `item` | Peso, competencias | Reglas de equipo inicial | Parcialmente |
| 8 | **Historia e imagen** (`text`) | Historia, descripción final, imagen | — | — | — | Sí |
| 9 | **Revisión** (`review`) | Resumen de decisiones con consecuencias | — | Ficha derivada completa | Build completo válido | — |

## 3. Comportamiento transversal

- **Guardar y continuar**: el borrador (`Actor.build`) persiste en cada paso (paso actual + selecciones); salir y volver retoma donde estaba. Nunca se pierde lo elegido.
- **Previsualización**: `POST /characters/build/preview` devuelve las estadísticas derivadas y los errores por paso; la UI las muestra, no las calcula (ADR-034).
- **Permisos**: el jugador solo ve contenido publicado en la campaña y permitido (`grants`); lo privado del DM **no aparece ni se filtra** (pruebas de no-filtración, PMB-3.8). El DM puede crear el personaje de un NPC con las mismas herramientas (`kind: npc`).
- **Al terminar**: el personaje queda en la campaña (Personajes del Espacio) y disponible en la ventana Ficha de la mesa; el Hub (PMB-3.6/3.7) lo muestra sincronizado.
- **Contenido real obligatorio**: sin catálogos provisionales — el creador solo ofrece contenido existente publicado en la campaña (decisión del propietario §13).

## 4. Decisiones del propietario (2026-11-10 — **mapa aprobado**)

1. **Contenido semilla:** subconjunto amplio del SRD 5.1 — 9 razas + 12 clases + trasfondos del SRD (CC BY 4.0, con atribuciones registradas).
2. **Contenedor:** ruta completa dentro del espacio de campaña (`/campaigns/{id}/characters/new`).
3. **Atributos:** manual **+ compra de puntos** desde el día 1 (el servidor valida rangos y puntos).
4. **Equipo inicial:** sí, en el creador desde el día 1 (flujo completo de 9 pasos).

## 5. Siguiente (tras aprobación)

- **PMB-3.2/3.3** (backend aditivo: `CharacterBlueprint`, ampliación de `Actor`, endpoint de preview) → **PMB-3.4** prototipo visual del creador por pasos + capturas → nueva aprobación → **PMB-3.5** integración.
