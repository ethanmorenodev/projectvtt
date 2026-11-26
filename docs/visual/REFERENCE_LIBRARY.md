# ProjectVTT — Biblioteca de referencias (REFERENCE_LIBRARY.md)

**Estado:** Propuesta (2026-11-09) — pendiente de aprobación del propietario.  
**Uso:** referencias de diseño del post-MVP, **qué problema resuelve cada una y qué principios adaptamos**. No copiamos interfaces; extraemos principios (§4.1 del plan). Cada pantalla nueva añadirá aquí sus referencias concretas.

---

## 1. Inkarnate — experiencia de creación

**Problema que resuelve:** crear mapas y contenido sin ser un diseñador; herramientas potentes alrededor de un lienzo grande.

**Principios que adaptamos:**
- **Lienzo protagonista**: el área de trabajo ocupa casi todo; las herramientas viven en raíles compactos alrededor.
- **Herramientas contextuales**: la barra lateral cambia según el modo/selección (crear, editar, decorar) en lugar de acumular todos los controles.
- **Flujos de creación con plantillas**: partir de una base (tipo de contenido, escena) acelera y educa.
- **Modo edición vs modo uso**: separar claramente "estoy construyendo" de "estoy jugando".

**No copiamos:** su estética fantástica (violentaría la neutralidad de ProjectVTT) ni la paleta cálida de sus paneles.

## 2. Linear — jerarquía y claridad

**Problema que resuelve:** mucha información densa y muchas acciones, sin sensación de caos.

**Principios que adaptamos:**
- **Jerarquía tipográfica estricta**: pocas variantes de tamaño/peso; el texto se escanea.
- **Superficies tranquilas**: grises carbón diferenciados por tono, color solo para significado.
- **Keyboard-first y command palette**: la acción rápida no requiere menús.
- **Listas densas y compactas** con acciones contextuales al hover/foco.
- **Movimiento mínimo y funcional**: confirmación de estado, nunca decoración.

**No copiamos:** su identidad de producto SaaS (marcas, ilustraciones, onboarding corporativo) ni su monocromo azulado.

## 3. Unreal Engine — organización de herramientas complejas

**Problema que resuelve:** decenas de editores especializados conviviendo en un mismo espacio de trabajo.

**Principios que adaptamos:**
- **Sistema de ventanas/paneles único**: cada herramienta es un panel del mismo sistema (acoplable, redimensionable, con layout guardado).
- **Panel de detalles/inspector** a la derecha: la edición ocurre en contexto, no en pantallas aparte.
- **Outliner/árbol a la izquierda**: la estructura del mundo (escenas, contenido, capas) siempre visible y navegable.
- **Barra de herramientas contextual por modo**: cada modo (mapa, combate, edición) muestra su conjunto.

**No copiamos:** la densidad extrema de sus editores ni su lenguaje visual técnico (terminología, iconografía industrial).

## 4. Baldur's Gate 3 — personaje, inventario y equipamiento

**Problema que resuelve:** presentar un personaje rico (stats, equipo, relaciones) de forma satisfactoria y tangible.

**Principios que adaptamos:**
- **Silueta/espacios de equipamiento visibles**: cada hueco (arma, armadura, casco…) se reconoce al instante; el objeto equipado se ve como miniatura en su hueco.
- **Ficha contextual de objeto**: click sobre una miniatura → panel con imagen, propiedades, efectos y requisitos.
- **Inventario visual con contenedores**: mochila, bolsillos y cajas como espacios organizados (no una tabla de texto).
- **Comparación e interacción directa**: equipar/desequipar/comparar desde el propio objeto.
- **Rasgos como primera clase**: las características del personaje se listan con su descripción, no escondidas en cálculos.

**No copiamos:** su estética de juego (marcos ornamentales, tipografía temática) ni su UI a pantalla completa.

## 5. Alchemy — atmósfera durante la partida

**Problema que resuelve:** que la mesa de juego se sienta inmersiva durante la sesión, no como un editor.

**Principios que adaptamos:**
- **Presentación primero durante el juego**: medios (mapa, arte, música si llega) al frente; la UI se aparta.
- **Paneles que se ocultan** para dejar el tablero limpio; reaparecen al necesitarlos.
- **Cohesión atmosférica sin saturar**: fondos oscuros translúcidos, contenidos del usuario como protagonistas.

**No copiamos:** sus efectos de atmósfera pesados (nieblas animadas, brillos) — ProjectVTT prioriza confort en sesiones largas.

---

## 6. Síntesis de principios (los que gobiernan ProjectVTT)

1. **El área de trabajo manda** (Inkarnate/Alchemy): la mesa y el editor de contenido ocupan el centro.
2. **Herramientas alrededor, no dentro** (Unreal): raíles, outliner, inspector, ventanas.
3. **Jerarquía tipográfica y superficies tranquilas** (Linear): el color significa, no decora.
4. **El contenido se reconoce por miniaturas compactas** (BG3): inventario, biblioteca y selectores usan imágenes cuadradas pequeñas.
5. **Interacción directa sobre el objeto** (BG3): click → inspector/acciones; sin navegación profunda.
6. **Modo edición y modo juego diferenciados** (Inkarnate/Alchemy): crear y jugar tienen climas distintos dentro de la misma app.

## 7. Referencia aprobada previamente por el propietario

La distribución del **perfil de personaje** seleccionada como referencia en sesiones anteriores (menú de personaje tipo RPG: perfil/equipo/relaciones/historia) se conserva como referencia vinculante del Character Hub (§8 del plan). Cuando el propietario aporte la captura/distribución concreta, se archiva en `docs/visual/screens/character-hub.md` como referencia oficial.

## 7. Maqueta objetivo del propietario (2026-11-10)

La maqueta compartida por el propietario («quiero que se acabe viendo así») es la **referencia norte del aspecto final**. Especificación extraída:

- **Marco de ventana (app de escritorio):** barra de título oscura con el emblema + «ProjectVTT» y controles nativos (minimizar/maximizar/cerrar). El icono y el nombre ya están configurados en Godot (`config/icon` + `config/name`); el marco personalizado se hará en el cliente de escritorio.
- **Raíl izquierdo (~240 px):** logo completo (emblema + wordmark) arriba; navegación con **iconos de línea + etiqueta** (Inicio, Campañas, Espacio de campaña, Mesa de juego, Biblioteca, Gestión) con la activa en **píldora morada saturada**; abajo, **chip de usuario con punto verde de estado**.
- **Barra superior:** buscador con icono y hint «Buscar (Ctrl+K): campañas, escenas, jugadores…» y **menú de usuario «Salir ▾»**.
- **Contenido:** los estados vacíos de área son **hero**: ilustración grande propia del área + título grande + descripción centrada. Texto de Biblioteca (literal): «La biblioteca universal de contenidos se construye en la base de datos de ProjectVTT (piezas, escenas, objetos, criaturas e imágenes).»
- **Paleta:** carbón con tinte azul-púrpura y acentos morados (compatible con los tokens aprobados).

**Implementado hasta ahora (2026-11-10):** raíl con logo completo e iconos de línea, píldora activa morada, chip de usuario con estado, buscador con icono y «Salir ▾», y empty states hero (con el emblema como arte provisional). **Pendiente de assets:** ilustraciones propias por área (libro de la Biblioteca, etc.) — el propietario puede aportarlas (carpeta tipo LOGO) o generarse cuando haya créditos de imagen.

## 8. Registro de decisiones por pantalla

A medida que se diseñen pantallas, cada entrada añadirá: pantalla, referencias usadas, principios extraídos, captura del prototipo aprobado y fecha de aprobación. Archivo: `docs/visual/screens/`.
