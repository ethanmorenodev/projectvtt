# Cambio de identidad visual — «Tinta y ámbar» (2026-11-25)

El propietario pidió explícitamente un cambio de estética completo, con libertad para saltarse el proceso habitual. Se sustituye la identidad carbón/morado frío por una identidad cálida de **tinta y ámbar**.

## Qué cambia (global, no por pantalla)
- **Paleta**: fondos tinta cálida (`#0F1113` → `#363C43`), texto pergamino (`#F5F1E8`, `#CDC6B8`, `#B0A89B`), acento ámbar/cobre (`#D9A566`) con texto oscuro encima. Semánticos recalibrados.
- **Tipografía**: nueva familia de display `Georgia` para títulos de página y héroes; se mantiene Inter para UI y JetBrains Mono para datos. Sin fuentes nuevas que descargar.
- **Carácter**: degradados de atmósfera muy tenues y fijos, tarjetas con esquina superior derecha recortada (mazo sobre la mesa), filete ámbar en selección, versalitas con más tracking.
- **Marca**: wordmark propio («✦ ProjectVTT») en el raíl y en el login, sustituyendo el logotipo morado que ya no encajaba. El arte del login (d20) se conserva.
- Los tokens se cambian en `theme/tokens.css` y su espejo en `styles.css`, por lo que **toda la app** cambia a la vez sin reescribir componentes.

## Qué se conserva
- Contraste AA verificado para los 18 pares de tokens (`accessibility.test.js` pasa).
- Foco visible de 2 px, movimiento reducido, objetivos de 28 px y ARIA existente.
- Contratos, permisos, rutas, reglas de negocio y contenido del SRD.

## Nota sobre imágenes con IA
Se intentó generar arte nuevo con IA; las peticiones fallaron por **credencial inválida** (`Invalid credential`). No se reintentó. El cambio se apoya en los assets CC0/CC BY ya acreditados y en CSS, sin nuevas dependencias.

## Capturas
`00-login`, `01-inicio`, `02-campanas`, `03-espacio`, `04-biblioteca`, `05-herramientas`, `06-creditos`, `07-mesa` en esta carpeta.

## Verificación
Web **252/252** (incluye la auditoría de contraste), build limpio. E2E y suites de backend sin cambios funcionales.
