# Recorrido guiado de primer ingreso (2026-11-26)

UI nueva con prototipo aprobado por el propietario en esta sesión. El alcance es solo la web: no cambia contratos, permisos, router ni selectores E2E existentes en `App.tsx`.

## Decisión

- **Cinco pasos**, sobre áreas reales de la aplicación, en este orden: Campañas → Espacio de campaña → Mesa de juego → Biblioteca → Personajes. Cada paso muestra un título serif, una explicación breve en español y su posición (`n / 5`). Los textos viven en `i18n/es.ts`, en la nueva sección `tour` (español primero, sin nueva librería).
- **Bandera** `pv-tour-done` en `localStorage` con valor `'1'`. Se escribe al pulsar «Empezar» (último paso) o «Omitir». Con la bandera puesta el componente no se monta visualmente, así que el recorrido aparece solo la primera vez. Si `localStorage` falla o no está disponible, se lee como «no hecho» y la escritura se ignora: la app sigue funcionando y el recorrido puede cerrarse igual.
- **Overlay temporal y no bloqueante**: se monta en `App.tsx` dentro del `Shell` (usuario ya autenticado), como hermano de `AppShell`, por lo que nunca cubre el login. Tiene `role="dialog"`, `aria-modal="true"`, etiqueta accesible y foco al abrir; los botones usan `Button` y el anillo de foco de los tokens `--pv-*`. «Anterior» está deshabilitado en el primer paso; el último sustituye «Siguiente» por «Empezar». No hay navegación automática ni cambios de ruta.
- **Identidad «Tinta y ámbar»**: superficies y bordes `--pv-surface-*`, acento `--pv-accent`, tipografía `Georgia` (`--pv-font-display`) en el título y esquina superior derecha recortada con `clip-path`. Sin fuentes, imágenes ni dependencias nuevas.
- El patrón visual y de accesibilidad replica `Dialog`/`ConfirmDialog`: overlay oscuro, diálogo centrado, cabecera, cuerpo y pie de acciones.

## Capturas reales (tomadas y revisadas)

Con sesión autenticada y `localStorage` sin `pv-tour-done`:

1. `01-paso-1.jpeg` — primer paso sobre Campañas; «Anterior» deshabilitado.
2. `02-paso-2.jpeg` — segundo paso (Espacio de campaña).
3. `03-paso-3.jpeg` — tercer paso (Mesa de juego).
4. `04-paso-4.jpeg` — cuarto paso (Biblioteca).
5. `05-paso-5.jpeg` — quinto paso, con «Empezar» visible.
6. `06-fin-del-tour.jpeg` — cierre del recorrido.

`verificacion.txt` registra que, tras terminar el tour y recargar, el diálogo no vuelve a aparecer (`0`).

## Verificación

- `npm run build` y `npm test` en `apps/web`.
- Pruebas unitarias nuevas en `src/ui/OnboardingTour.test.tsx`: render accesible, avance de los cinco pasos, «Omitir» guarda la bandera, bandera previa oculta el tour y almacenamiento bloqueado no rompe la app.
- Las suites existentes de `App` (`App.navigation.test.tsx`, `App.session.test.tsx`) siguen pasando: el overlay es hermano del shell y no altera los selectores ni el router.

## Estado

Cerrado. Las capturas están revisadas y la carpeta aparece indexada en `docs/INDEX.md`. Sin trabajo pendiente.
