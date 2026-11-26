# Cambios estructurales de compose (2026-11-25)

Tras el cambio de paleta, el propietario señaló que seguía siendo superficial. Se han hecho cambios de **forma**, no solo de color.

## 1. Raíl global
- Salen **«Espacio de campaña» y «Mesa de juego»**: son acciones que no existen sin una campaña abierta. Se acceden desde cada campaña (la tarjeta abre el espacio; desde ahí «Ir a la mesa»).
- El raíl queda con seis destinos y se agranda: 236 px, filas de 42 px, iconos de 20 px, texto 14 px y logo completo del propietario (sin cambiarlo).

## 2. Página de Campañas
- Cabecera con rótulo «TUS MUNDOS» y título de display serif de 42 px, con aire; antes era una línea simple y pegada.
- Fondo de cabecera con imagen **CC0 de OpenGameArt** (*Fantasy RPG Background*, AliHamieh), registrada en `public/art/LICENSES.md`.
- Las filas compactas se sustituyen por **portadas anchas** con arte, título superpuesto, rol, participantes y actividad, más acción de gestión en la esquina.

## 3. Inicio
- «Continuar jugando» usa portadas anchas con degradado y texto superpuesto.
- Nueva fila **«Tu grupo»** con retratos elegidos de forma estable por nombre (`portraitForName`), de modo que Vex/Grom/Kira no compartan imagen.

## 4. Biblioteca
- Franja superior con arte y cuatro **colecciones destacadas** (Razas, Criaturas, Hechizos, Objetos) que filtran por tipo desde un clic.

## Nota de imagen IA
La generación con IA falló dos veces por `Invalid credential`. No se reintentó: la imagen de la cabecera es CC0 descargada de OpenGameArt y atribuida. El propietario pidió «sácala de internet» y así se hizo, con licencia limpia.

## Capturas
`01-campanas.jpeg`, `02-inicio.jpeg` y `03-biblioteca.jpeg` en esta carpeta.

## Verificación
Web **253/253** (nueva prueba de retratos distintos), E2E **7/7** (incluye aserción de que el raíl ya no ofrece Espacio/Mesa), build limpio.
