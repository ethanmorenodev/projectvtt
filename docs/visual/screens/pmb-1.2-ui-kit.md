# PMB-1.2 — Biblioteca de componentes base (capturas de revisión)

**Fecha:** 2026-11-09 · **Estado:** implementado — **pendiente de aprobación visual del propietario** (la aprobación de la arquitectura NO incluye la de los diseños).  
**Alcance:** PMB-1.1 (tokens de diseño) + PMB-1.2 (componentes base). No es una pantalla del producto: es la biblioteca que las pantallas usarán.  
**Verificación:** 91/91 pruebas web (12 nuevas de componentes) + `npm run build` (tsc + vite) en verde.

## Capturas (app real, `#ui-kit` en el dev server)

1. **`pmb-1.2-ui-kit-completo.jpeg`** — catálogo completo: paleta de color (carbón + morado apagado + semánticos), escala tipográfica, jerarquía de botones (primaria/secundaria/fantasma/peligro/peligro sólido/deshabilitado/icono), formularios (etiqueta + ayuda + error), lista densa con fila seleccionada, tarjeta compacta con miniatura cuadrada, estado vacío con guía, skeletons y confirmación destructiva.
2. **`pmb-1.2-ui-kit-toasts.jpeg`** — toasts abajo-derecha: éxito (borde verde), error persistente (borde rojo, no se cierra solo) y **máximo 3 apilados** (regla aplicada).
3. **`pmb-1.2-ui-kit-confirmacion.jpeg`** — modal de confirmación destructiva con fondo oscurecido, frase de verificación tecleable ("Escribe el nombre de la campaña para confirmar"), Cancelar (fantasma) y Eliminar (peligro sólido — el único caso de fondo rojo).

## Criterios aplicados (docs/visual/COMPONENT_RULES.md)

- Una acción primaria por superficie; jerarquía visible en botones.
- Miniaturas **cuadradas y compactas**; la ilustración grande solo bajo petición.
- Estados vacíos con guía y acción; errores persistentes en toasts; confirmación con frase para acciones irreversibles.
- Textos **siempre por props** (ADR-038: i18n-ready, sin cadenas embebidas en componentes).
- Accesibilidad: etiquetas asociadas, `aria-invalid`/`aria-describedby`, roles de diálogo/lista, foco visible.

## Decisión pendiente

Aprobación del propietario sobre estas capturas antes de usar los componentes en pantallas (Fase 2). Correcciones pedidas → se aplican aquí y se vuelven a capturar.
