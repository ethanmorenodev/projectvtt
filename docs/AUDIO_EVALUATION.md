# Evaluación de audio (PMB-8.8)

**Fecha:** 2026-11-17 · **Estado:** evaluado — **no se incorpora audio en este cierre**.

## 1. Contexto

`POST_MVP_ASSETS_RESEARCH.md` §17 identifica fuentes limpias: **Sonniss GDC Game Audio Bundle** (royalty-free comercial, sin atribución, uso en producto permitido, reventa no), **freesound** con filtro CC0 y **Kenney** (UI sounds, CC0). El propio research marca el audio como *«opcional, no debe frenar nada»*.

## 2. Criterios aplicados

| Criterio | Situación actual |
|---|---|
| ¿Existe infraestructura de audio? | **No.** No hay reproductor, mezclador, ni evento de dominio que pida sonido; los eventos SignalR son de estado, no de presentación. |
| ¿Hay superficie para controlarlo? | **No.** Volumen/mute/activación serían UI nueva (regla: prototipo + capturas + aprobación antes de integrar). |
| ¿Aporta al flujo central? | El flujo central (campañas, mesa, personajes, biblioteca) funciona y está verificado sin audio. Sonidos de dados/ambiente serían un extra, no un requisito. |
| Coste de incorporarlo | Curaduría de licencias por pieza, descarga de bundles grandes, registro de atribuciones, controles de UI, prueba en navegador y alternativas de accesibilidad (el audio requiere control visible y no puede depender de él la información). |
| Riesgo en el cierre | Medio-alto: cambiaría el alcance de Fase 8 justo al final y añadiría superficie de licencias y UI sin demanda comprobada. |

## 3. Decisión

**Diferir el audio.** No se añaden dependencias, archivos de sonido ni controles. La información que hoy se comunica visualmente (turnos, tiradas, resultados, eventos en vivo) sigue siendo accesible sin sonido, coherente con PMB-8.4.

**Alternativas consideradas:** (a) bundle Sonniss completo — licencia cómoda pero gran volumen y sin demanda; (b) unos pocos SFX de Kenney CC0 para UI — barato pero sigue exigiendo UI de control y beneficio marginal; (c) freesound CC0 por pieza — curaduría manual por archivo. Se descarta incorporar cualquiera ahora.

## 4. Qué haría cambiar la decisión

- Petición explícita del propietario o de jugadores de ambiente/efectos sonoros.
- Una pantalla de **Ajustes** aprobada donde vivan volumen, mute y activación (entonces el audio encaja sin inventar superficie nueva).
- Un caso de uso donde el sonido aporte accesibilidad real (con alternativa visual equivalente, nunca en exclusiva).

Si se retoma: registrar **fuente, licencia, URL y atribución** de cada archivo (ADR-036), mantener los sonidos fuera del repo si el bundle lo exige, y no usar audio automático sin gesto del usuario.

## 5. Verificación

No aplica: cambio documental, sin código ni assets. Se han consultado las fuentes citadas en `POST_MVP_ASSETS_RESEARCH.md` §17 y el criterio PMB-8.8 («solo si aporta valor»).
