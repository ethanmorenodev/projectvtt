# Mesa acoplada estilo Alchemy — propuesta (2026-11-19)

Estado: **aprobado por el propietario e integrado** (mesa acoplada completa,
panel derecho en ESCENAS y ventanas solo para herramientas opcionales).
Prototipo de referencia: `#prototipo-mesa-alchemy`. Capturas finales de la app
real: `proyecto-completo/06-mesa.jpeg` y `06-mesa-story.jpeg`.

## Evaluación honesta

No podemos igualar el **brillo artístico** de Alchemy: es un producto comercial
con años de desarrollo, arte propio, animaciones y audio. Podemos igualar su
**claridad estructural**, que es lo que hoy nos falla. En la captura del
propietario todo está acoplado y en su sitio; en nuestra mesa las funciones
importantes viven en ventanas flotantes que hay que descubrir y colocar.

## Comparación

| Zona | Alchemy | ProjectVTT actual | Prototipo |
|---|---|---|---|
| Centro | Escenario protagonista, sin paneles flotantes | Escenario con ventanas flotantes encima | Escenario limpio, sin ventanas |
| Izquierda | Chat con input abajo | Diario/Notas acoplado (mejor) + ventana Chat duplicada | Diario/Notas acoplado, sin duplicado |
| Derecha | Panel DM: SCENES / STORY / TOOLS + lista de escenas | Grupo / PNJs / Inspector; escenas en un desplegable arriba | Panel DM con pestañas, filtro, lista y escena activa |
| Abajo | Bandeja de dados (d4…d20) + audio | Dock de acciones (Tirar/Anotar/Medir/Niebla) | Bandeja de dados real + dock de acciones |
| Arriba | STORY / TACTICAL | Selector de escena y Enfocar | STORY / TACTICAL + Enfocar |
| Herramientas | Tira vertical de iconos | Lanzador en cabecera con etiquetas | Tira vertical junto al panel DM |

## Qué propone integrar

1. **Panel derecho del DM** con pestañas ESCENAS / HISTORIA / HERRAMIENTAS,
   lista de escenas con miniatura, filtro, indicador de escena activa y botón
   «+». Grupo/PNJs/Inspector se reubican dentro de esas pestañas (nada se
   elimina).
2. **Panel izquierdo** sería solo Diario/Notas; se retira la ventana flotante
   de Chat duplicada (el chat ya está acoplado ahí).
3. **Bandeja de dados** abajo con d4/d6/d8/d10/d12/d20 que publica la tirada
   en el diario mediante el endpoint real `/api/v1/campaigns/{id}/rolls`.
4. **Alternancia STORY / TACTICAL**: la vista story muestra la escena actual
   con arte y texto; la táctica conserva el tablero PixiJS intacto.
5. **Ventanas flotantes** quedan solo para herramientas opcionales (Character
   Hub, herramientas de mapa), nunca para diario/escenas/notas.

## Riesgos y decisiones

- ADR-033 permite ventanas flotantes; acoplar lo esencial **no las elimina**,
  pero cambia la composición aprobada en PMB-2.8b. Requiere aprobación.
- El panel derecho necesita los mismos datos que hoy tiene el selector de
  escena (`sceneList`), más `tokenCount` (ya viene en `SceneSummary`).
- La bandeja de dados usa el servicio de reglas existente; no se duplica lógica.
- En pantallas estrechas el panel derecho puede colapsar bajo un botón.

## Preguntas

1. ¿Aprobar la integración completa de la mesa acoplada?
2. ¿El panel derecho debe abrir por defecto en ESCENAS (como Alchemy) o en
   GRUPO (como ahora)?
3. ¿Conservar las ventanas flotantes para Character Hub y herramientas de mapa,
   o acoplarlas también?
