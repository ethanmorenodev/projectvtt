# PMB-2.6 — Prototipo del Espacio de campaña

**Fecha:** 2026-11-09 · **Estado:** prototipo implementado — **pendiente de aprobación visual del propietario** (capturas bloqueadas temporalmente por el capturador: requiere la ventana de OpenCode visible).  
**Alcance:** doble navegación (raíl de app + subnavegación de campaña con las 13 secciones del plan §3.1), contenido de muestra estático, variantes `normal` / `vacio` / `extremo`. Rutas dev: `#prototipo-espacio[-vacio|-extremo]`, montado en el shell real.

## 1. Análisis de función

El espacio de campaña es el **centro de gestión del universo de la campaña**: debe permitir pasar de la visión general (Resumen) a cualquiera de sus áreas en un clic, sin volver a la lista de campañas. La subnavegación izquierda fija las 13 secciones del plan; el contenido cambia a la derecha conservando el contexto (nombre de campaña + estado + acción «Gestionar»).

Secciones con contenido diseñado: **Resumen** (estadísticas + última sesión), **Escenas y mapas** (lista con miniatura, grid y tokens), **Personajes** (rejilla de miniaturas con jugador), **Participantes y permisos** (roles con badges), **Biblioteca de campaña** (rejilla compacta). El resto muestra su estado futuro con la fase asignada (Mesa → Fase 2; Homebrew → Fase 5; Historia/Facciones/Localizaciones → Fases 4–7).

## 2. Decisiones de composición a revisar

- **Doble nivel**: raíl de app + subnavegación de campaña (232 px) + contenido. Evita saturar el raíl principal.
- El título de sección encabeza el contenido; el contexto de campaña vive en la subnavegación («La caída de Scaldar»).
- Miniaturas: 48 px en listas, 64 px en rejillas (regla aprobada); alternativas con inicial donde no hay imagen.
- Estados vacíos por sección con CTA contextual («Crear escena», «Crear personaje», «Crear contenido»).
- Variante extrema: nombre de escena muy largo (elipsis + tooltip) y 12 escenas / 9 personajes para valorar densidad.

## 3. Verificación

3 pruebas nuevas (subnavegación + cambio de sección + variantes vacía/extrema) — suite web **123/123** · `npm run build` limpio. Capturas 2026-11-10 archivadas: `pmb-2.6-espacio-{normal,vacio,extremo}-2026-11-10.jpeg` (shell real con datos de la app).

## 4. Decisión pendiente

**Aprobación visual** de la composición (doble nivel, densidades, rejilla de personajes) una vez presentadas las capturas. Solo tras ella se integra el Espacio de campaña real (PMB-2.7, con los datos existentes: resumen real, escenas reales, participantes reales; las secciones futuras irán apareciendo según su fase).

**→ Aprobado por el propietario (2026-11-10) e integrado el mismo día (PMB-2.7):** `CampaignSpaceView` con las 13 secciones del subnavegador; datos reales (escenas, personajes/actores, participantes, actividad) y secciones futuras reservadas con estados honestos. Capturas de la integración archivadas: `pmb-2.7-espacio-{resumen,mesa,participantes}-2026-11-10.jpeg` (datos reales de la app demo).
