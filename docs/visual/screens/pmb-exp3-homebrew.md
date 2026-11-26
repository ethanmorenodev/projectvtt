# EXP-3 — Prototipo del creador guiado de clases homebrew (DM)

**Fecha:** 2026-11-18 · **Estado:** ⏳ prototipo listo, **pendiente de aprobación del propietario**. No se ha tocado la app real.
**Ruta dev:** `#prototipo-clase-homebrew`.

## 1. Decisiones del propietario recogidas

- Opción visible **«Crear clase»** para DM (permisos intactos, servidor como autoridad).
- **Desde cero o duplicar** una clase existente.
- Guiado y visual: nombre, imagen, concepto y estilo de juego; atributos y recursos; competencias; equipo inicial; habilidades y pasivas; progresión y especializaciones; elecciones del jugador.
- Reutilizar contenido de la biblioteca o crearlo desde contexto; **sin programar ni editar JSON**.
- Borradores, previsualización y **publicación por mundos/campañas**; jugadores solo contenido permitido.
- **Diferenciar efectos automatizados de resolución manual**.
- Publicar/actualizar **no modifica silenciosamente** personajes que ya usan la clase.

## 2. Estructura del prototipo

- **Izquierda:** 7 pasos (Origen → Identidad → Atributos y recursos → Equipo inicial → Habilidades → Progresión → Revisar y publicar), con estado *hecho/activo*, y resumen vivo de la clase.
- **Centro:** el contenido del paso. En *Origen*, tarjetas para duplicar Guerrero/Mago/Explorador o empezar de cero. En *Habilidades*, tarjetas con distintivo **Automático** (verde) / **Resolución manual** (ámbar) y selector para añadir una u otra; botón «Añadir habilidad de la biblioteca». En *Equipo inicial*, chips con miniaturas y botón «Añadir objeto de la biblioteca». En *Publicar*, vista previa como la verán los jugadores y casillas de mundo/campaña.
- **Derecha:** vista previa de la imagen de la clase y resumen (nombre, origen, nº de habilidades, borrador guardado).

Los datos son de **muestra**; no hay llamadas al servidor ni persistencia.

## 3. Capturas

| Estado | Archivo |
|---|---|
| Origen (desde cero / duplicar) | `pmb-exp3-homebrew-origen-prototipo-2026-11-18.jpeg` |
| Habilidades (automático vs manual) | `pmb-exp3-homebrew-habilidades-prototipo-2026-11-18.jpeg` |
| Revisar y publicar | `pmb-exp3-homebrew-publicar-prototipo-2026-11-18.jpeg` |

## 4. Pendiente de aprobación

1. ¿Aprobar la estructura de 7 pasos y la tarjeta de efectos **Automático / Resolución manual**?
2. ¿Aprobar la publicación por mundos/campañas con el aviso de **no modificar personajes existentes**?
3. ¿Aprobar el uso de la biblioteca para reutilizar habilidades y objetos desde el propio flujo?

**Hasta la aprobación no se integra en la app real.** La integración reutilizará el editor universal (Fase 4), habilidades/elecciones (Fase 5), visibilidad y permisos del servidor, sin duplicar reglas de negocio.
