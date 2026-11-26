# ProjectVTT — Investigación: assets y contenido externo (§17)

**Estado:** INVESTIGACIÓN ENTREGADA y **AMPLIADA** (2026-11-09, petición del propietario) — pendiente de revisión. **No se ha descargado ni incorporado ningún recurso**.  
**Alcance:** fuentes de contenido y assets, licencias, estrategia de contenido real de D&D 5e, inventario por categorías, tipología de recursos (iconos/miniaturas/ilustraciones), biblioteca de assets, paquetes importables, almacenamiento de licencias, impacto arquitectónico y propuesta de integración por fases.  
**Método:** búsqueda y verificación de fuentes y licencias reales (comprobadas el 2026-11-09); recuentos de contenido aproximados, a verificar contra los documentos fuente al importar.

---

## 1. Resumen ejecutivo

1. **Contenido real de D&D: es legal y es gratis.** El **SRD 5.1** y el **SRD 5.2.1** están publicados por Wizards of the Coast bajo **Creative Commons BY 4.0** (pueden usarse juntos en el mismo producto). Contienen razas/especies, clases con subclase, trasfondos, **hechizos reales** (≈316 en 5.1; **336** en 5.2.1) con sus nombres, niveles, escuelas, componentes, tiempos, alcance, duración y descripciones, más objetos, criaturas, condiciones y reglas. **Lo que no está en el SRD** (subclases de suplementos, criaturas "de marca" tipo Beholder o Illithid, nombres propios del entorno) **no es redistribuible** sin autorización (DM Guild o licencia directa con el editor).
2. **Assets visuales de calidad con licencias limpias** abundan: `game-icons.net` (4.180+ iconos de fantasía/RPG, **CC BY 3.0**), **Kenney** (40.000+ assets de juego y UI, **CC0**), **ambientCG** y **Polyhaven** (texturas/PBR/HDRIs, **CC0**), tipografías **OFL** (Google Fonts), iconografía UI MIT/ISC. Audio: bundles **Sonniss GDC** (royalty-free comercial, sin atribución) y freesound con filtro CC0.
3. **Todo se integra por "paquetes de contenido" con procedencia**: cada paquete registra fuente, versión, licencia y atribución; la aplicación enseña créditos y permite sustituir recursos sin romper referencias. La arquitectura del creador **no cambia** (un paquete es contenido con ámbito; requisito §17.2).
4. **Criterio de selección aplicado** (§17.5): licencia y redistribución primero; luego calidad, coherencia estética, rendimiento y facilidad. No se integra nada "porque sea fácil de descargar".

## 2. Contenido real de D&D 5e (§17.2)

### 2.1 Marco legal verificado

| Fuente | Licencia | Uso permitido | Notas |
|---|---|---|---|
| **SRD 5.1** (System Reference Document v5.1, Wizards of the Coast) | **CC BY 4.0** (desde enero de 2023) | Copiar, modificar, redistribuir y uso comercial **con atribución** | Texto completo utilizable, incluidas descripciones de hechizos |
| **SRD 5.2.1** (reglas 2024; publicado 22-04-2025) | **CC BY 4.0** | Ídem | **Compatible con el 5.1 en el mismo producto** (confirmado por Wizards) |
| Contenido **fuera del SRD** (suplementos: Xanathar's, Tasha's, Mordenkainen's…; criaturas y nombres propios de marcas) | **Protegido** (D&D Content / Product Identity) | **No redistribuible** sin autorización | Vías: DM Guild (licencia propia de WotC) o acuerdos directos con editores |
| Marcas "Dungeons & Dragons", "D&D" | Marca registrada | Uso nominativo | "Compatible con el SRD 5.x de D&D"; sin sugerir patrocinio |
| OGL 1.0 (fuentes antiguas tipo Open5e "other OGL sources") | OGL (cada tercero con sus términos) | Con atribución OGL | Preferir el material CC BY; los paquetes OGL registran su licencia exacta |

**Atribución obligatoria (CC BY 4.0)** que incluiremos en la pantalla de créditos y en los metadatos del paquete, estilo: *"Esta obra incluye material del System Reference Document 5.1/5.2.1 de Wizards of the Coast, disponible bajo CC BY 4.0 (creativecommons.org/licenses/by/4.0). Se han realizado adaptaciones de formato."*

### 2.2 Qué contiene el SRD (aprox.; verificar contra el documento fuente al importar)

| Categoría | SRD 5.1 | SRD 5.2.1 |
|---|---|---|
| Razas/especies | 9 (enano, elfo, semielfo, mediano, humano, gnomo, semi-orco, dracónido, tiefling) | + Goliath y Orco (2024) |
| Clases | 12 clases **con una subclase cada una** (p. ej. Campeón, Vida) | 12 clases (2024) con subclases revisadas |
| Trasfondos | 12 (Acólito, Charlatán, Criminal, Artista, Héroe popular, Artesano, Ermitaño, Noble, Forastero, Sabio, Marinero, Soldado, Pícaro) | Revisados 2024 (p. ej. Criminal, Sabio, Soldado) |
| Hechizos | ≈316 | **336** (+20: Chromatic Orb, Charm Monster, Dissonant Whispers…) |
| Objetos/arma/armaduras | Catálogo completo de tablas SRD | Revisado 2024 |
| Criaturas | ≈260–320 fichas (sin criaturas de marca) | Ampliado (+17 al menos) |
| Condiciones y reglas | Las 14 condiciones + acciones, combate, exploración | Revisadas 2024 |
| Dotes | Un puñado | Ampliado |

Los **hechizos reales** (requisito explícito del §17.2) se importan con nombre, nivel, escuela, componentes (V/S/M), tiempo de lanzamiento, alcance, duración, clases y **descripción literal del SRD** (permitida por CC BY con atribución).

### 2.4 Inventario por categorías (ampliación 2026-11-09)

Qué contenido real está disponible para el paquete `dnd5e-srd-5.1` (y su evolución en 5.2.1), con su estado legal. Todo **CC BY 4.0** salvo indicación; nada de esto incluye arte:

| Categoría | Disponible en el SRD | Contenido (detalle) | Fuente de datos | Nota legal |
|---|---|---|---|---|
| **Razas/especies** | Sí | 5.1: enano, elfo, semielfo, mediano, humano, gnomo, semi-orco, dracónido, tiefling (9, con subrazas de SRD) · 5.2.1: + Goliath y Orco | SRD oficial + JSON de 5e SRD API/Open5e | CC BY 4.0 |
| **Clases y subclases** | Sí (parcial) | 12 clases; **una subclase de SRD por clase** (p. ej. Campeón, Vida). El resto de subclases (Xanathar's, Tasha's…) **no está en el SRD** | Ídem | Las no-SRD requieren licencia (DM Guild/directa) |
| **Trasfondos** | Sí | 5.1: 12 (Acólito, Charlatán, Criminal, Artista, Héroe popular, Artesano, Ermitaño, Noble, Forastero, Sabio, Marinero, Soldado, Pícaro) · 5.2.1 revisados | Ídem | CC BY 4.0 |
| **Hechizos** | Sí | ≈316 (5.1) / **336** (5.2.1) — **hechizos reales** con todos sus campos y descripción | Ídem (texto verificable contra el SRD) | CC BY 4.0; descripciones literales permitidas con atribución |
| **Armas y armaduras** | Sí | Tablas completas de SRD (daño, peso, propiedades, precios; armaduras y escudos) | Ídem | CC BY 4.0 |
| **Objetos** | Sí | Equipo de aventurero + **objetos mágicos** de SRD (≈150–300) con rareza y propiedades | Ídem | CC BY 4.0 |
| **Criaturas** | Sí (parcial) | ≈260–320 fichas (5.1) ampliadas en 5.2.1. **Excluidas las criaturas de marca** (Beholder, Illithid, Displacer Beast, etc.) | Ídem | CC BY 4.0; las de marca son Product Identity |
| **Condiciones** | Sí | Las 14 condiciones con descripción oficial | Ídem | CC BY 4.0 |
| **Reglas y acciones** | Sí | Acciones de combate, coberturas, descansos, exploración, muerte/salvación | Ídem | CC BY 4.0 |
| **Dotes** | Sí (parcial) | Un puñado (5.1); ampliadas en 5.2.1 | Ídem | CC BY 4.0 |
| **Iconografía** | **No** (el SRD no incluye arte libre) | Se resuelve con fuentes CC (§3.1): iconos de contenido tipo game-icons.net | Propia / CC BY 3.0 | El arte de WotC nunca se usa |

### 2.3 Estrategia de incorporación (propuesta)

1. **Paquete oficial `dnd5e-srd-5.1`** como primer contenido del sistema (y `dnd5e-srd-5.2` como paquete adicional posterior — el modelo multi-paquete lo admite sin tocar la arquitectura del creador, requisito §17.2).
2. **Datos machine-readable + verificación contra la fuente oficial**: fuentes candidatas `5e SRD API`/`5e-bits` (JSON del SRD; código MIT, texto CC BY 4.0), **Open5e** (API con SRD + búsqueda) y transcripciones markdown del SRD 5.2.1; el **texto de referencia es siempre el SRD oficial de Wizards** (PDF/D&D Beyond) para validación de campos. El importador produce el paquete con `ContentItem` + `Ability` + `ImageAsset` (iconos propios, sin arte de WotC: **el arte del SRD no está bajo CC**).
3. **Metadatos de procedencia en cada paquete** (§5): fuente, versión del SRD, licencia, URL, atribución, fecha de importación, hashes.
4. **Posteriormente, paquetes oficiales/licenciados de terceros** (editores con licencia propia: Kobold Press, MCDM, DM Guild bajo su acuerdo…) como paquetes importables independientes (§4).
5. **Lo que nunca entra**: contenido de suplementos no SRD, arte de WotC, datos de D&D Beyond. El usuario puede crearlo como homebrew propio (o importar paquetes que él tenga derecho a usar).

## 3. Fuentes de assets clasificadas (§17.1/§17.3)

Clasificación por utilidad y licencia (criterios §17.5). "Redistribuible" = podemos incluirlo en la aplicación/paquetes.

| Fuente | Contenido | Licencia | Redistribuible | Utilidad para ProjectVTT |
|---|---|---|---|---|
| **game-icons.net** | 4.180+ iconos SVG/PNG de fantasía/RPG (armas, armaduras, hechizos, criaturas, condiciones) | **CC BY 3.0** (algunos PD) | Sí, con atribución | **Muy alta**: iconos de contenido (objetos, habilidades) e iconos de herramientas de la mesa |
| **Kenney** (kenney.nl) | 40.000+ assets: UI, tilesets, sprites, sonidos | **CC0** | Sí | **Alta**: assets de UI y de mapa (tilesets), sonidos de interfaz |
| **ambientCG** | Texturas PBR, HDRIs, modelos | **CC0** | Sí | **Media**: texturas/fondos para decoración y mapas |
| **Polyhaven** | Texturas/HDRIs/HDRI/modelos | **CC0** | Sí | **Media**: ídem |
| **Google Fonts / SIL OFL** (Inter, Cinzel, EB Garamond, Spectral, JetBrains Mono…) | Tipografías | **OFL 1.1** | Sí | **Alta**: UI (ya propuesta Inter/JetBrains Mono) + tipografías de contenido opcionales |
| **Iconografía UI** (Tabler, Phosphor, Lucide) | Iconos de interfaz | **MIT / ISC** | Sí | **Alta**: iconos de navegación/herramientas (set coherente, requisito de guía visual) |
| **Sonniss GDC Game Audio Bundle** (anual) | 600–1000+ efectos de sonido pro | **Royalty-free** (uso comercial, sin atribución) | Uso en producto sí; reventa no | **Media/baja** (valor a evaluar): ambiente de mesa, dados, UI |
| **freesound.org** | SFX/música | CC0/CC BY (filtrable) | Según pieza | **Baja/media**: apoyo puntual con filtro CC0 |
| **OpenGameArt** | Arte, audio, música variados | CC0/CC BY/GPL (por pieza) | Según pieza | **Media**: curation obligatoria por licencia por pieza |
| **Packs VTT de terceros** (2-Minute Tabletop, Czepeku, etc.) | Mapas y assets cartográficos | Licencias de uso VTT **con frecuencia sin redistribución** | **Normalmente no** | Como **contenido que el usuario importa** (la biblioteca del §17.3 lo soporta), no empaquetado por nosotros |
| **Fuentes oficiales no SRD** (D&D Beyond, suplementos, wiki de Forgotten Realms) | Contenido y arte | Protegidos | **No** | Solo referencia del usuario; nunca redistribuido |

**Criterios aplicados (§17.5):** prioridad a licencias CC0/CC BY/MIT/OFL con redistribución permitida; calidad y coherencia estética (los iconos de fantasía viven en el **contenido**; la chrome de la app usa iconografía UI neutra — regla de neutralidad temática); formatos SVG (escalan, se rasterizan al compilar) y WebP/PNG con derivados; rendimiento (paquetes por demanda, no cargas masivas); facilidad de integración (todo pasa por la biblioteca de assets y los paquetes).

### 3.1 Bibliotecas de iconografía (ampliación)

| Fuente | Contenido | Licencia | Valor para ProjectVTT |
|---|---|---|---|
| **game-icons.net** | 4.180+ iconos SVG de fantasía/RPG (armas, armaduras, hechizos, escuelas, condiciones, criaturas) | **CC BY 3.0** (algunos PD) | **Primario**: iconos de contenido (objeto/habilidad/hechizo/condición) e iconos de herramientas de mesa |
| **Tabler / Phosphor / Lucide** | Iconos de interfaz SVG (navegación, acciones, estados) | **MIT / ISC** | **Primario**: chrome de la aplicación (set único y coherente, regla de guías visuales) |
| **Kenney (Game Icons / UI Pack)** | Iconos y piezas de UI | **CC0** | Apoyo: piezas de UI y de mapa |
| **Iconify / Noun Project** | Agregadores con licencias **por icono** (MIT, CC BY, PD…) | Variable | Solo con filtro explícito por licencia y registro por icono; no como fuente masiva |
| **Font Awesome Free** | Iconos UI | **CC BY 4.0** (iconos) / SIL OFL (fuentes) | Alternativa al set MIT; no se mezclan sets |

Regla: **un solo set de chrome** (MIT/ISC) + **un solo set de contenido** (CC BY 3.0, con atribución global en Créditos). No mezclar estilos de icono dentro de una misma superficie.

### 3.2 Ilustraciones, retratos y avatares

| Fuente | Licencia | Uso previsto | Riesgo |
|---|---|---|---|
| **OpenGameArt** (selección CC0/CC BY) | Por pieza | Ilustraciones de contenido (fondos de ficha, decoración de categorías) | Curation por pieza; registrar autor |
| **Wikimedia Commons** (filtro CC0/CC BY/PD) | Por pieza | Retratos/arte de referencia | Ídem |
| **Packs gratuitos de autores** (itch.io/Gumroad "free") | **Por pack** (muchos son "uso personal o en juego", sin redistribución) | Solo si la licencia permite **redistribución en la app/paquetes**; si no, el usuario los importa | Alto: leer cada licencia; default = descartar |
| **Arte generado por IA** | Política propia del proveedor + decisión del propietario | Posible origen de miniaturas | **Pendiente de política** (etiquetado de origen obligatorio si se permite) |
| Arte de WotC/D&D Beyond/wikis | Protegido | **Nunca** | — |

### 3.3 Mapas, texturas, efectos y recursos de interfaz

- **Texturas/materiales/fondos:** ambientCG (**CC0**), Polyhaven (**CC0**), Kenney tilesets (**CC0**) — decoración de UI, patrones y assets de mapa.
- **Cartografía de terceros** (2-Minute Tabletop, Czepeku…): licencias de **uso en VTT por el usuario**, habitualmente sin redistribución → solo vía importación del usuario (§3).
- **Efectos y partículas:** implementación propia en PixiJS (sin dependencia) o **tsParticles** (MIT) si se justifica; sprites de partículas desde fuentes CC0 (Kenney/ambientCG).
- **Recursos de interfaz:** sonidos de UI de Kenney (CC0); cursotes/rejillas propias.
- **Audio (opcional, Fase 8):** Sonniss GDC (royalty-free, sin atribución), freesound con filtro CC0, OpenGameArt audio (por pieza).

### 3.4 Proyectos de código abierto de referencia

| Proyecto | Qué aporta | Licencia | ¿Integrable? |
|---|---|---|---|
| **5e SRD API / 5e-bits** (y su `5e-database`) | Datos JSON del SRD | Código **MIT**; texto **CC BY 4.0** | **Sí**: como fuente de importación de datos (no como dependencia en runtime) |
| **Open5e** | API/búsqueda de SRD (+ fuentes OGL) | Datos según fuente (SRD CC BY) | Sí, como fuente; preferir material CC BY |
| **Foundry VTT `dnd5e`** | Reglas automatizadas 5e | **GPL-3.0** (código) | **Código no incorporado**: licencia GPL incompatible con MIT (ADR-047); solo se reutilizan ideas y estructura de datos, reescritos. |
| **tsParticles / PixiJS (ya usado)** | Partículas/render | MIT | tsParticles solo si se justifica la dependencia |
| **ts-rest/similares** | — | — | No necesarios (contratos propios) |

Lección: **datos CC BY se importan; código GPL no se enlaza**; la licencia final de ProjectVTT (pendiente) determina qué código OSS podemos adoptar.

### 3.5 Tipología de recursos: iconos, miniaturas e ilustraciones (requisito §17.3)

La biblioteca **diferencia tres papeles** (un mismo archivo puede tener varios, pero el uso manda):

| Papel | Definición | Formato/tamaño | Uso habitual |
|---|---|---|---|
| **Icono** | Símbolo compacto y monocromo/tintable que identifica un tipo o acción (arma, hechizo, condición, herramienta) | SVG (16–24 px; tintable) | En listas, selectores, huecos de equipamiento, botones de contenido |
| **Miniatura** | Representación cuadrada reconocible de un elemento de contenido | 1:1 (64/128/256), recorte `cover` | **Representación habitual** de razas, clases, objetos, criaturas, NPC (regla visual fundamental) |
| **Ilustración** | Arte completo (vertical u horizontal) del elemento | Original (WebP/PNG) | Solo bajo petición (lightbox/inspector); **nunca** satura listas ni editores |

- **Asociación a cualquier elemento**: todo `ContentItem` (y `Actor`) acepta `imageId` + papel preferido (icono/miniatura/portada) + punto focal; el editor universal ofrece el selector según el papel.
- **Reutilización sin duplicar**: referencias por `imageId` (ADR-035/036); un mismo archivo sirve a N elementos; sustituir el archivo conserva todas las referencias.
- **Regla de interfaz**: miniaturas cuadradas y compactas siempre; la ilustración grande solo en el inspector/lightbox bajo demanda.

## 4. Paquetes de contenido importables (§17.4)

**Formato propuesto `.pvttpkg`** (zip): `manifest.json` (id del paquete, nombre, versión, sistema, autor, **fuente, licencia, URL de licencia, atribución**, fecha, hash de contenidos) + `content.json` (tipos, items, revisiones, relaciones, categorías) + `assets/` (imágenes e iconos con sus variantes) + `LICENSES/` (textos de licencia) + `images/` (opcional, metadatos de foco/etiquetas).

Reglas de importación:
- **Identidad sin duplicados**: cada elemento tiene clave `(paquete, key)`; reimportar la misma versión es no-op; versión mayor/ menor → resolución explícita (§17.4: "sin duplicar elementos existentes y resolver conflictos").
- **Conflictos de versión**: pantalla de diferencias (qué cambia, qué lo usa) con opciones *actualizar / conservar la versión actual / importar como copia nueva* — el mismo principio del versionado del ADR-031 (nunca silencioso).
- **Referencias seguras**: los assets se sustituyen por `imageId` (ADR-035): cambiar la imagen de un elemento no rompe referencias; exportar/importar conserva atribuciones.

**Pipeline de importación (definición operativa):** ① validar estructura del paquete (manifest, tipos, límites de tamaño — misma política que imágenes/mapas; sin ejecución de código) → ② resolver identidad `(paquete, key)` y conflictos de versión → ③ importar assets y generar variantes (miniaturas 64/128/256 + original; iconos SVG conservados) → ④ registrar licencia/atribución/procedencia (ADR-036) → ⑤ indexar para búsqueda → ⑥ informe de importación (creados/actualizados/omitidos/conflictos).

**Organización y búsqueda dentro de la biblioteca:** facetas por **papel** (icono/miniatura/ilustración), **tipo de contenido** (objeto, hechizo, criatura…), **paquete/procedencia**, **licencia**, **álbum/colección** y **etiquetas** libres; búsqueda por nombre y descripción; orden por nombre/recientes/uso; filtros combinables y guardables como colecciones. Un asset se arrastra o se asigna desde el selector a cualquier elemento (referencia, nunca copia).

## 5. Licencias y atribuciones (§17.6.6)

- **`ContentPack{key, name, version, system, sourceUrl, license, licenseUrl, attribution[], importedAt}`** — nuevo agregado (ADR-036); todo `ContentItem` e `ImageAsset` pertenece opcionalmente a un paquete.
- **`ImageAsset` ampliado**: `license`, `attribution`, `origin` (subido por el usuario / paquete X), `author`.
- **Pantalla "Créditos y licencias"** (Gestión): listado de paquetes y assets con su atribución y enlace; **exportación de la nota de atribución** (para distribuir la campaña/publicación cumpliendo CC BY).
- Regla: **no se elimina ninguna atribución obligatoria**; los paquetes importados conservan sus metadatos aunque el usuario edite copias.

## 6. Impacto en la arquitectura actual (§17.6.7)

| Área | Impacto | Detalle |
|---|---|---|
| Modelo de contenido (ADR-031) | **Aditivo** | `ContentPack` + campos de licencia/procedencia en items e imágenes; el creador **no cambia** (un paquete es contenido con ámbito) |
| Imágenes (ADR-035) | **Aditivo** | Colecciones/álbumes (ya previsto), sustitución conservando `imageId`, metadatos de licencia, operación de import por paquete |
| Biblioteca (LIBRARY.md) | Ampliación | Filtros por paquete/licencia, gestión de atribuciones, import de paquetes (Fase 4/7) |
| Motor de reglas | Sin cambios | Los hechizos del SRD son `Ability` con efectos tipados (`manual` hasta Fase 5) |
| Creador de personajes | **Sin cambios** (requisito) | Consume contenido del paquete SRD como cualquier contenido de campaña |
| Seguridad | Control | Importación valida estructura, tamaños y tipos (misma política que mapas/imágenes); sin ejecución de código en paquetes |
| Riesgo legal | Gestionado | Solo CC0/CC BY/MIT/OFL/royalty-free con registro; nada de arte ni texto de WotC fuera del SRD |

## 7. Propuesta de integración por fases (§17.6.8)

| Fase | Qué se integra | Por qué aquí |
|---|---|---|
| **Fase 1** | Selección de **iconografía** (game-icons.net para contenido + set UI MIT) y **tipografías** (OFL) con su archivo de atribución | El sistema visual nace con sus recursos definitivos (evita rehacer iconos después) |
| **Puente 2→3** | `ContentPack` + procedencia (ADR-036) + **paquete `dnd5e-srd-5.1`** importado y verificado (razas/clases/trasfondos/hechizos/objetos/criaturas/condiciones) | El creador de personajes se construye sobre **contenido real de D&D** (resuelve el contenido semilla: no se inventa nada) |
| **Fase 4** | **Biblioteca de assets completa** (importar, buscar, filtrar, colecciones, etiquetas, miniaturas, sustitución sin perder referencias, gestión de atribuciones + pantalla de créditos) | Es la fase de biblioteca/imágenes por definición |
| **Fase 5** | Hechizos del SRD con efectos automatizables (las partes que el motor resuelva) | El motor ampliado convierte texto SRD en reglas funcionales |
| **Fase 7** | **Formato de paquete importable** (`.pvttpkg`) con resolución de conflictos; texturas/fondos CC0 para decoración; paquete `dnd5e-srd-5.2` como segundo paquete oficial | Import/export es tarea de Fase 7; multi-paquete demuestra la arquitectura |
| **Fase 8** | Evaluación de **audio** (Sonniss/freesound CC0) si aporta valor; auditoría final de atribuciones | El audio es optativo y no debe frenar nada |

**No se descarga ni incorpora nada masivo hasta la aprobación de esta investigación** (requisito §17.6): tras tu visto bueno, la primera descarga será la selección concreta de iconos/tipografías (Fase 1) y el SRD (Puente), ambas con su registro de licencia.

## 8. Riesgos y decisiones pendientes

1. **Contenido no SRD**: jamás incluirlo; educación al usuario (su homebrew es su responsabilidad; la app muestra avisos de licencia al importar).
2. **Licencia definida**: MIT (ADR-047 / `LICENSE`). El código GPL (p. ej. `dnd5e` de Foundry VTT) no se incorpora; los datos CC BY sí, con atribución en Créditos.
3. **Arte generado por IA**: política pendiente del propietario (algunas licencias de uso comercial son complejas; la app debe permitir etiquetar el origen de las imágenes).
4. **Traducciones**: el SRD está en inglés; la traducción al español del contenido es trabajo derivado propio (permitido por CC BY indicando cambios). Decisión de alcance: ¿traducir el paquete o mostrar el original con notas?
5. **Calidad del contenido machine-readable**: verificar contra el SRD oficial (diferencias de transcripción conocidas en comunidades).
6. **Versionado de paquetes**: al publicar SRD 5.2.1 como paquete aparte, definir convivencia (un personaje usa un solo sistema/paquete de reglas a la vez).
