# Audio opcional y ajustes de sonido (2026-11-26)

Superficie nueva en la web, derivada de **ADR-049** (`DECISIONS.md`). No cambia contratos compartidos, permisos ni selectores E2E del raíl. No se descargan samples ni se añaden assets: los efectos son tonos breves generados con WebAudio en el cliente.

## Decisión

- **Gestor único** en `src/audio/sound.ts`: un solo `AudioContext` creado/reanudado tras el primer gesto del usuario (`click`/`keydown`), con `setAudioSettings`, `play`, `isEnabled` y `getAudioSettings`. Cuatro tonos (`roll`, `token`, `message`, `confirm`) con envolvente de ganancia y volumen 0-100. Sin WebAudio, con contexto bloqueado o con `localStorage` no disponible, degrada a silencio sin lanzar excepciones. Preferencias en `pv-audio-settings`.
- **Acceso**: ruta `/ajustes` (`kind: 'ajustes'`), montada en `App.tsx` y enlazada desde la tarjeta «Ajustes de sonido» de Herramientas (grupo Configuración). El raíl global no se toca.
- **Interfaz**: interruptor `role="switch"` con `aria-checked`, slider `min=0 max=100` con `<output>` y botón «Probar sonido». Misma identidad «Tinta y ámbar» que Herramientas (superficies `--pv-surface-*`, acento `--pv-accent`, tipografía del sistema). Textos en la sección `ajustes` de `i18n/es.ts`.
- **Accesibilidad AA**: el sonido es complementario y explícitamente opcional; la pantalla indica que la información también es visual y que el navegador solo permite audio tras una interacción. Los eventos reales (`RollResolved`, `TokenMoved`, `MessagePosted`) siguen actualizando la UI exactamente igual: solo se añade `play(...)` cuando el audio está activo.
- los disparadores están en `src/views/SceneView.tsx` (donde se registran realmente los handlers de `CampaignRealtime`), no en `App.tsx`; `App.tsx` solo instala el gesto de desbloqueo. El coordinador autorizó tocar `SceneView.tsx` para este fin.

## Capturas reales (tomadas y revisadas)

Con sesión autenticada y `/ajustes` abierto:

1. `01-ajustes-desactivado.jpeg` — estado inicial: interruptor apagado, volumen al 60 %, botón de prueba visible y ambos avisos legibles.
2. `02-ajustes-activado.jpeg` — interruptor encendido y slider al 60 % con su `<output>`, ruta «Herramientas / Ajustes de sonido» en la cabecera y raíl sin etiquetas nuevas.

**Verificado en navegador real (Chromium, EXP-46):** con `AudioContext` instrumentado se creó **1 contexto** y **1 oscilador**, con estado `running`. La comprobación es reproducible en `tests/E2E/AudioFlowTests.cs`. Instrumentar el constructor permite observar la llamada, **no afirma emisión acústica**: prueba que la ruta de reproducción se ejecuta sin errores y que el contexto se reanuda tras el gesto. `audio-browser.txt` conserva la salida de la ejecución manual original.

## Verificación

- `npm run build` y `npm test` en `apps/web` (deben seguir ≥274/274).
- `src/audio/sound.test.ts`: `AudioContext` simulado; cubre gesto previo, único contexto, reanudación, volumen, persistencia, degradación sin WebAudio y `localStorage` bloqueado.
- `src/views/AjustesView.test.tsx`: toggle y slider actualizan el gestor; «Probar sonido» invoca `play('confirm')`.
- Las suites de router, `App` y `HerramientasView` siguen pasando: no se alteran selectores ni rutas existentes.

## Estado

Cerrado en EXP-45. Las capturas están revisadas y la carpeta aparece indexada en `docs/INDEX.md`. Riesgo conocido y aceptado: la emisión acústica no se mide (no hay micrófono en CI); sí se verifica en Chromium que la ruta WebAudio se ejecuta (`AudioFlowTests`), además de las pruebas unitarias en jsdom.
