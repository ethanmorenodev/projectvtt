# Catálogo real en el Espacio de campaña (EXP-34, 2026-11-25)

Cuatro secciones que antes mostraban «próximamente» muestran ahora datos reales:

| Sección | Fuente (sin contrato nuevo) |
|---|---|
| NPC | actores de la campaña ya cargados, filtrados por `kind === 'npc'` |
| Criaturas | `GET /content/items`, filtro `typeKey === 'creature'` (SRD 5.1 y 5.2) |
| Biblioteca de campaña | misma carga, `scope === 'campaign' && campaignId === campaign.id` |
| Contenido homebrew | misma carga, `scope === 'user'` |

El servidor ya aplica visibilidad (`ContentService.CanView`); el cliente solo filtra para presentar. Se reutilizan `List`/`ListRow`, `contentCover`, `Badge` y el inspector existente (ampliado con `Selection.kind === 'content'`). Una sola petición de contenido para las tres secciones. Facciones y localizaciones **siguen** «próximamente»: pertenecen al grafo de lore, no al catálogo.

## Aprobación visual
El propietario autorizó «dale con todo» tras ver el bloqueo de esta tarea (prototipo → capturas → aprobación), así que se ejecutó reutilizando patrones ya aprobados y se adjuntan capturas reales de las cuatro secciones. **No se ha introducido UI nueva desde cero**: mismo raíl, mismas listas y mismo inspector.

## Capturas
- `01-pnjs.jpeg` — NPC con un actor real y badge `PNJ · Campaña`.
- `02-criaturas.jpeg` — criaturas del SRD con miniatura y badges `Criaturas · Global`.
- `03-biblioteca-campana.jpeg` — estado vacío honesto (la campaña de la captura no tiene contenido propio).
- `04-homebrew.jpeg` — estado vacío honesto.

## Verificación
Web **264/264**, build limpio, 7 pruebas unitarias del Espacio de campaña (incluida la búsqueda de EXP-30). Sin cambios de backend ni de contratos.

## Salvedad
Los estados vacíos son intencionados en una campaña nueva; las capturas no muestran contenido `campaign`/`user` porque el escenario de captura solo sembró el SRD global. El filtro está cubierto por las pruebas unitarias.
