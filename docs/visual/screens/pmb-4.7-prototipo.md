# PMB-4.7 — Biblioteca de imágenes, atribuciones y Créditos

**Fecha:** 2026-11-16 · **Estado:** ✅ **aprobado («Aprobado — construir») e integrado 2026-11-16** (backend, vistas reales y migración aditiva; verificación en verde).
**Rutas dev:** `#prototipo-imagenes`, `#prototipo-imagenes-vacio`, `#prototipo-creditos`.

## 1. Referencias

- `POST_MVP_ASSETS_RESEARCH.md` §3.5: tres papeles — **icono** (SVG 16–24, tintable), **miniatura** (1:1, 64/128/256, recorte `cover`) e **ilustración** (original, solo bajo petición). Miniaturas cuadradas siempre en listas; la ilustración grande nunca satura rejillas.
- Ibíd. §5 y ADR-036: `ImageAsset` registra licencia, URL, autor, origen y atribución; pantalla **Créditos y licencias** con **exportación de la nota de atribución**; ninguna atribución obligatoria se elimina.
- ADR-037: solo contenido/arte CC0·CC BY; **nunca** arte de WotC fuera del SRD.

## 2. Composición

### Biblioteca de imágenes (variante normal / vacío)
- **Frame de tres zonas** coherente con el explorador PMB-4.1: filtros · rejilla · detalle.
- **Izquierda — facetas:** papel (icono/miniatura/ilustración), álbum, etiqueta y licencia, con «Limpiar filtros». Búsqueda por nombre en la toolbar.
- **Centro — rejilla:** tarjetas cuadradas con miniatura, nombre y badges papel/licencia; contador de resultados y estado vacío honesto.
- **Derecha — detalle:** etiquetas, autor, origen, licencia (con enlace) y atribución. Acciones:
  - **Ampliar** → lightbox bajo petición con la ilustración y su atribución.
  - **Usar en elemento** → reutilización **por referencia** (`imageId`, sin duplicar archivos).
  - **Sustituir imagen** → aviso explícito: conserva el `imageId`, no rompe referencias.

### Créditos y licencias
- Paquetes (D&D 5e SRD 5.1 · CC BY 4.0, con texto de atribución y enlace) e imágenes con papel/licencia/atribución.
- **Exportar nota de atribución** genera un texto listo para distribuir la campaña/publicación cumpliendo CC BY.
- Nota legal: los paquetes importados conservan sus metadatos.

## 3. Datos honestos

- Solo **dos recursos locales reales** (`/logo-projectvtt.png`, `/login-bg-neutral.jpg`) y cuatro marcadores de posición; **no se descarga nada externo**.
- Licencias/autores son ejemplos de metadatos (game-icons.net CC BY 3.0, recursos propios CC0, SRD CC BY 4.0), marcados como muestra en el código.

## 4. Contrato (aprobado e implementado 2026-11-16)

- `ImageAssetDto` (ya existe la entidad `ImageAsset`): `id, fileName, contentType, sizeBytes, role, albums[], tags[], license, licenseUrl, author, origin, attribution[], focal, variants, createdAt`.
- `GET /api/v1/images` con filtros `role|album|tag|license|query`; `GET /api/v1/images/{id}`; `POST /api/v1/images` (multipart, validación de tamaño/formato **en servidor**); `PATCH /api/v1/images/{id}` (álbumes/etiquetas/licencia/atribución/foco); `DELETE /api/v1/images/{id}`; `GET /api/v1/credits/export` (nota de atribución).
- La pantalla real de Créditos vivirá en **Gestión** (el prototipo la monta en Campañas por una limitación del shell de prototipos).

## 5. Capturas

| Estado | Captura |
|---|---|
| Rejilla + facetas + inspector con licencia | `pmb-4.7-imagenes-prototipo-2026-11-16.jpeg` |
| Estado vacío | `pmb-4.7-imagenes-vacio-prototipo-2026-11-16.jpeg` |
| Créditos y exportación | `pmb-4.7-creditos-prototipo-2026-11-16.jpeg` |
| **App real integrada** (biblioteca con facetas y license) | `pmb-4.7-imagenes-real-2026-11-16.jpeg` |
| **App real integrada** (Créditos bajo Gestión) | `pmb-4.7-creditos-real-2026-11-16.jpeg` |

## 6. Decisiones pendientes del propietario

1. **Composición:** ¿se aprueba el explorador de tres zonas + lightbox + Créditos, o se prefiere otro agrupamiento?
2. **Alta de imágenes:** ¿subida directa con validación estricta de tamaño/formato y variantes generadas en servidor, o variantes en cliente (como ADR-035 prevé)?
3. **Contrato:** ¿se aprueban los endpoints anteriores tal cual (aditivos)?
4. **Alcance:** ¿incluir desde ya «sustitución conservando `imageId`» o dejarla para PMB-4.10 (donde está planificada la sustitución con filtros por paquete/licencia)?

**Respuesta esperada:** «Aprobado — construir» (con alcance) o los cambios concretos. Hasta entonces no se modifica la app real ni el contrato.
