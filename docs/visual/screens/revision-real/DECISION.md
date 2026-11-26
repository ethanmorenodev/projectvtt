# Revisión de las páginas reales con el director de diseño (2026-11-25)

## Hallazgo
El propietario señaló que las páginas seguían siendo «feas» y pidió quitar el **modo burbuja** de la barra lateral. El director confirmó que el problema era de **contención**, no de color: una lengüeta `›` casi invisible, un aviso flotante en todas las capturas, un emblema diminuto compitiendo con las migas, y acciones primarias blancas/azules repetidas en cada página.

## Decisión aplicada
1. **Raíl acoplado y siempre visible** (210 px): wordmark completo, ocho destinos planos en tres grupos (Sesión / Biblioteca / Sistema), icono + etiqueta, activo con franja morada de 2 px y fondo tonal. Sin cápsula, flecha, pin, «Más» ni pista flotante.
2. **Cabecera con superficie**: migas alineadas al contenido, buscador con placeholder «Buscar (Ctrl+K)…», sin logo duplicado. Desaparecieron «Crear ▾» y el botón «?»; la ayuda sigue disponible con la tecla `?`.
3. **Una sola acción primaria por página**: CTAs blancos convertidos a morado (hero de Inicio, vista de historia de la mesa), avatar con fondo neutro y inicial, chip de escena y «Enviar» atenuados, modo «Táctico» sin relleno de acento, «Abrir →» clonados convertidos en acciones de icono, y los seis «Próximamente» del subnav eliminados.
4. **Últimos ajustes del director**: wordmark reducido a 96 px, fila de usuario con avatar/nombre reales y punto de estado, y una sola acento vivo en la mesa («+ Añadir token»).

## Capturas
Antes y después de Inicio, Campañas, Espacio de campaña, Biblioteca, Imágenes, Herramientas, Créditos y Mesa en esta carpeta (`01-`…`08-`). Las `*-antes.jpeg` son las originales; las actuales han sobrescrito los mismos nombres tras la integración.

## Verificación
Web **252/252**, E2E **7/7**, build limpio. `juego-gatos/` queda intacto y fuera de los commits.
