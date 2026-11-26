# PMB-1.7 — Paleta de comandos (captura de revisión)

**Fecha:** 2026-11-09 · **Estado:** implementada — **pendiente de aprobación visual del propietario**.  
**Alcance:** búsqueda global con Ctrl/Cmd+K (COMPONENT_RULES §10): grupos de acciones y campañas (en vivo), filtros por texto, navegación con flechas + Enter, cierre con Escape, `role=combobox/listbox/option`. Textos por props (ADR-038).

## Captura

- **`pmb-1.7-paleta.jpeg`** — paleta abierta sobre la aplicación real (atajo Ctrl+K o clic en el buscador de la barra superior): fondo oscurecido, campo de búsqueda, grupo «ACCIONES» con navegación a las áreas y elemento activo resaltado. El grupo «Campañas» aparece con las campañas del usuario cuando las hay (vacío en esta cuenta de captura).

## Verificación

4 pruebas nuevas (filtrado, Enter ejecuta y cierra, flechas cambian el activo, clic ejecuta + estado vacío) — suite web **112/112** + `npm run build` en verde; integrada en el shell real (PMB-1.4) sin romper la E2E (verificada antes de la integración).

## Decisión pendiente

Aprobación visual junto al resto de capturas (PMB-1.2/1.3/1.4). Futuro (Fase 2): añadir escenas, personajes y contenido a los resultados según el contexto.
