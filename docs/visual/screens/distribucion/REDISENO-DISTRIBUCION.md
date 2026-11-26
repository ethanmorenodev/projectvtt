# Rediseño de distribución — propuesta (2026-11-19)

Estado: **aprobado por el propietario el 2026-11-19** (integrar por fases;
Fase A = shell + Herramientas; Fase B = mesa con modo Enfocar que oculta
paneles laterales). **Fases A, B y C integradas.** Prototipo de referencia:
`#prototipo-distribucion` (y `-mesa`, `-biblioteca`, `-herramientas`).

## 1. Diagnóstico (capturas «antes»)

Las capturas `antes-*.jpeg` de esta carpeta muestran cuatro problemas concretos:

1. **El raíl mezcla areas y estados.** «Espacio de campaña» y «Mesa de juego»
   aparecen desactivados hasta que hay campaña/escena, sin que el usuario sepa
   por qué. Seis entradas planas, sin agrupación.
2. **Funciones escondidas dentro de otras vistas.** Imágenes, Créditos, Flujo
   del DM, Lore, Inventario y Crear clase solo se alcanzan mediante botones
   internos. «Gestión» abre directamente *Créditos y licencias*: la etiqueta no
   describe el destino.
3. **Cada vista tiene su propio marco.** Unas tienen subtítulo y acciones a la
   derecha, otras no; la cabecera global solo contiene una búsqueda que
   parece un campo de texto pero es un botón. No hay migas de pan.
4. **Densidad mal repartida.** Inicio y Gestión dejan grandes vacíos mientras
   la Biblioteca concentra muchas Chip en una sola barra. El resultado es
   «lioso» por falta de jerarquía, no por falta de funciones.

## 2. Inspiración de Alchemy VTT (sin copiar su contenido)

De Alchemy se toman **principios de distribución**, no arte ni datos:

- El **escenario es el protagonista** y los paneles se sitúan en los bordes.
- **Diario/Notas** arriba-izquierda; **Grupo/PNJs/Detalles** arriba-derecha.
- **Barra inferior de acciones rápidas** (tirar, anotar, medir, niebla, ficha).
- **Búsqueda omni** con atajo de teclado (ya existe como paleta de comandos).
- **Modo enfoque** para limpiar la interfaz sin salir de la escena.

Se conserva la esencia de ProjectVTT: mismos colores y tokens `--pv-*`, mismo
logo, mismas ventanas flotantes del sistema de ventanas (ADR-033), mismos
flujos y reglas del servidor. No se usan assets externos nuevos: los iconos
son los SVG de game-icons.net CC BY 3.0 ya acreditados en *Créditos*.

## 3. Propuesta

### Shell global
- Raíl compacto de **68 px**, con icono + etiqueta accesible, agrupado en tres
  bloques: **Jugar** (Inicio, Campañas, Espacio, Mesa), **Recursos**
  (Biblioteca, Imágenes, Créditos) y **Herramientas** (Herramientas, DM, Lore).
  El bloque «Herramientas» incluye una página índice que deja de esconder
  funciones.
- Cabecera con **migas de pan** (`Campañas / La tumba de Saltmarsh / Mesa`),
  búsqueda global con `Ctrl K` y botón **Enfocar**.
- **Marco de página único**: título + subtítulo + acciones principales siempre
  en la misma posición.

### Espacios
| Espacio | Cambio | Sin cambios |
|---|---|---|
| Inicio | «Continuar jugando» + accesos rápidos + tarjetas de campaña | creación/unirse, actividad, invitaciones |
| Mesa | Paneles laterales (Diario/Notas · Grupo/PNJs/Detalles), escenario a sangre, dock inferior | mapa, tokens, niebla, tiempo real, ventanas |
| Biblioteca | Árbol de tipos + rejilla + inspector con barra de herramientas simplificada | filtros, relaciones, usos, editor, procedencia |
| Herramientas | Índice de 8 herramientas con descripción y acceso directo | todas las rutas y sus permisos |

### Accesibilidad
- Enlace **Saltar al contenido principal** (visible al enfocar).
- `aria-label`/`aria-current`/`role="tablist"` en raíl, pestañas y dock.
- Foco visible con el token `--pv-focus-ring` en todos los controles.
- Modo **Enfocar** oculta paneles laterales y deja el escenario a pantalla
  completa (útil también para `prefers-reduced-motion` y pantallas pequeñas).
- Objetivos táctiles de 40 px en el raíl; textos mínimos de 11 px.

### Qué NO se toca
Rutas, contratos, endpoints, permisos, reglas de negocio, persistencia,
ventanas flotantes, contenido del SRD ni pruebas existentes. Es solo
organización, distribución y presentación.

## 4. Decisiones que necesito del propietario

1. ¿Aprobar el **raíl agrupado de 68 px** y la **página índice de Herramientas**?
2. ¿Aprobar la **distribución de la mesa** (panel izquierdo Diario/Notas,
   derecho Grupo/PNJs/Detalles, dock inferior y modo Enfocar)?
3. ¿Integrar por fases **(A) shell + Herramientas → (B) mesa → (C) Biblioteca**,
   o todo junto?

## 5. Capturas
- `antes-inicio.jpeg`, `antes-campanas.jpeg`, `antes-biblioteca.jpeg`,
  `antes-imagenes.jpeg`, `antes-gestion.jpeg` — app real, distribución actual.
- `prototipo-inicio.jpeg`, `prototipo-mesa.jpeg`, `prototipo-biblioteca.jpeg`,
  `prototipo-herramientas.jpeg` — prototipo propuesto.
