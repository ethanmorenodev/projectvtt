# PMB-1.3 / PMB-1.5 — Shell de navegación (prototipo) y sistema de ventanas

**Fecha:** 2026-11-09 · **Estado:** prototipo implementado — **pendiente de aprobación visual del propietario antes de integrar** (PMB-1.4).  
**Alcance:** PMB-1.3 (prototipo del shell con capturas) + PMB-1.5 (sistema de ventanas implementado y probado). Datos estáticos; sin lógica de negocio.  
**Verificación:** 102/102 pruebas web (11 nuevas del sistema de ventanas) + `npm run build` en verde. El sistema de ventanas está implementado (no solo prototipado): reductor puro, gestor con persistencia, arrastre/redimensionado/minimizar/z-order.

## Capturas (app real, `#prototipo-shell` en el dev server)

1. **`pmb-1.3-shell-inicio.jpeg`** — Inicio: raíl de áreas, búsqueda global (Ctrl+K) y bloques "Continuar jugando" (tarjetas compactas), Invitaciones y Actividad reciente.
2. **`pmb-1.3-shell-campanas.jpeg`** — Campañas: búsqueda + filtros (Todas/Propias/Compartidas/Archivadas) y tarjetas compactas con miniatura cuadrada, rol, participantes y última actividad.
3. **`pmb-1.3-shell-espacio-campana.jpeg`** — Espacio de campaña: subnavegación con las 13 secciones del plan + Resumen (estadísticas, última sesión, participantes con gestión).
4. **`pmb-1.3-shell-mesa-ventanas.jpeg`** — Mesa de juego con el **sistema de ventanas (PMB-1.5)** en acción: 3 herramientas (Chat, Ficha, Inspector) flotando en cascada sobre el tablero, con cabecera arrastrable, minimizar/cerrar, tiradores de redimensionado y z-order por foco; bandeja de minimizadas incluida.
5. **`pmb-1.3-shell-biblioteca.jpeg`** — Biblioteca: explorador de tipos a la izquierda, rejilla de **miniaturas cuadradas compactas**, filtros por papel (Miniaturas/Iconos/Ilustraciones) y licencia, e inspector con procedencia (paquete/licencia, §17).
6. **`pmb-1.3-shell-gestion.jpeg`** — Gestión: cuenta, preferencias (idioma/densidad/movimiento), mundos, créditos y licencias, importar/exportar.

## Decisiones de composición (para revisar)

- Raíl fijo de 216 px con las 6 áreas; barra superior de búsqueda global (⌘K) permanente.
- El espacio de campaña usa **doble nivel** (raíl de app + subnavegación de campaña) para no agotar el raíl principal.
- La mesa mantiene el tablero a sangre; la barra de herramientas queda arriba-izquierda y las herramientas se abren como ventanas debajo (host con origen propio).
- Biblioteca con tres zonas (árbol / rejilla / inspector) — patrón "Explorador" de las guías visuales.

## Decisión pendiente

**Aprobación del propietario de estas capturas** (y de las de PMB-1.2). Tras la aprobación se integra el shell real con URLs reales (PMB-1.4) montando las vistas actuales dentro. Correcciones pedidas → se aplican al prototipo y se vuelven a capturar.
