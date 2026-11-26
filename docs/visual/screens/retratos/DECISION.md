# Retratos de personaje (EXP-22, 2026-11-25)

El propietario pidió poder **ponerle una imagen a los personajes**. Ya existía la subida en el asistente de creación, pero una vez creado el actor no había forma de cambiarla desde la ficha ni desde el Character Hub: solo se mostraba la inicial del nombre.

## Cambio

- Componente reutilizable `src/ui/ActorPortrait.tsx` + `src/api/portraits.ts`.
  - `useImageUrl(id)` sirve la imagen como **blob autenticado** (`GET /api/v1/images/{id}/file`), sin credenciales en la URL; libera el `objectURL` al desmontar.
  - `uploadImage(file)` hace `POST /api/v1/images` con `FormData` y token Bearer.
  - El cambio usa `PATCH /api/v1/actors/{id}` con `{ version, portraitId }`: **el servidor valida formato/tamaño, permisos y concurrencia**; el cliente no duplica reglas.
- Integrado en **Ficha** (`ActorPanel`, solo propietario/GM) y en **Character Hub** (`CharacterHub`, mismo `onActorUpdated`). Ambos comparten el mismo `ActorDto`, así que un cambio se refleja al instante en el otro.
- «Tu grupo» de Inicio usa `actor.portraitId` y cae al retrato estable por nombre si no hay imagen.
- Affordance: botón circular con lápiz en la esquina del retrato, con `title` y `aria-label` («Cambiar imagen: Vex»). Sin franjas de texto que se compriman en retratos de 40 px.

## Reglas respetadas
- Sin segunda implementación de reglas de negocio: el PATCH pasa por el servicio de actores (ADR de autoridad del servidor).
- Sin arte protegido: la prueba usa un icono **CC BY 3.0** ya licenciado (game-icons.net). La imagen que suba el usuario queda registrada como «Propietario» por el servicio de assets.
- Errores explícitos: si falla la subida o el PATCH, se muestra `portrait.error`; el `input` se limpia para poder reintentar.

## Verificación
- Web **253/253**, build limpio.
- E2E **8/8**, con `PortraitFlowTests` (nueva prueba de regresión): sube un retrato desde la ficha, espera a que `GET /actors/{id}` devuelva `portraitId` y comprueba que la cabecera muestra `.actor-portrait__img`.
- Captura: `ficha-con-retrato.jpeg` en esta carpeta.

## Continuación
La subida de portada de campaña se implementó en EXP-23/EXP-24 reutilizando el contrato ya existente `PUT /campaigns/{id}/cover` (no `/images`). Ver `docs/visual/screens/portadas-campana/`.
