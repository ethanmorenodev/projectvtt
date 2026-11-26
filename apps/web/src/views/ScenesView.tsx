import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { apiFetch } from '../api/client';
import type { CampaignDetail, SceneDetail, SceneSummary } from '../api/types';
import { t } from '../i18n';
import { HistoryPanel } from './HistoryPanel';
import { NotesPanel } from './NotesPanel';
import { SceneThumb } from './presentation';
import './scenesView.css';

interface Props {
  campaign: CampaignDetail;
  onBack: () => void;
  onOpenScene: (scene: SceneDetail) => void;
  /** Incrustado en el Espacio de campaña (PMB-2.7): sin cabecera propia ni
   *  inspector duplicado — el inspector del espacio ya aloja notas/historial. */
  embedded?: boolean;
  /** Si se entrega, la fila selecciona en el inspector del padre en vez de
   *  abrir la mesa directamente (una sola acción dominante en el inspector). */
  onSelectScene?: (scene: SceneSummary) => void;
}

/**
 * Escenas de una campaña: crear (GM+) y abrir. Notas e historial son un
 * inspector ACOPLADO (una función, un hogar); ya no se lanzan como ventanas
 * flotantes que se solapan sobre la lista (criterio del consejo de diseño).
 */
export function ScenesView({ campaign, onBack, onOpenScene, embedded = false, onSelectScene }: Props) {
  const [scenes, setScenes] = useState<SceneSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newName, setNewName] = useState('');
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState<'notas' | 'historial'>('notas');

  const canManage = campaign.role === 'Owner' || campaign.role === 'GM';

  const load = useCallback(async () => {
    try {
      setScenes(await apiFetch<SceneSummary[]>(`/api/v1/campaigns/${campaign.id}/scenes`));
      setError(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campana.escenas.errors.load);
    }
  }, [campaign.id]);

  useEffect(() => {
    void load();
  }, [load]);

  const createScene = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await apiFetch<SceneDetail>(`/api/v1/campaigns/${campaign.id}/scenes`, {
        method: 'POST',
        body: { name: newName },
      });
      setNewName('');
      await load();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campana.escenas.errors.create);
    } finally {
      setBusy(false);
    }
  };

  const openScene = async (scene: SceneSummary) => {
    try {
      onOpenScene(await apiFetch<SceneDetail>(`/api/v1/scenes/${scene.id}`));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t.campana.escenas.errors.open);
    }
  };

  const list = (
    <>
      {error && <p className="error">{error}</p>}
      {scenes === null ? (
        <p>{t.campana.escenas.loading}</p>
      ) : scenes.length === 0 ? (
        <p>{t.campana.escenas.empty}</p>
      ) : (
        <ul className="item-list scenes-list">
          {scenes.map((scene) => (
            <li key={scene.id}>
              <button type="button" className="item"
                onClick={() => onSelectScene ? onSelectScene(scene) : void openScene(scene)}>
                <SceneThumb name={scene.name} sceneId={scene.id} hasCover={scene.hasCover} />
                <span>{scene.name}</span>
                <span className="muted">{scene.tokenCount} tokens</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {canManage && (
        <form onSubmit={createScene} className="row">
          <input
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
            placeholder={t.campana.escenas.namePlaceholder}
            maxLength={100}
            required
          />
          <button type="submit" disabled={busy}>
            {t.campana.escenas.create}
          </button>
        </form>
      )}
    </>
  )

  return (
    <section className="panel scenes-view">
      {!embedded && (
        <>
          <button type="button" className="link" onClick={onBack}>
            {t.campana.escenas.back}
          </button>
          <h2>
            {campaign.name} <span className="badge">{campaign.role}</span>
          </h2>
        </>
      )}

      {embedded ? list : (
        <div className="scenes-view__layout">
          <div className="scenes-view__main">{list}</div>
          <aside className="scenes-view__inspector" aria-label={t.campana.inspector}>
            <div className="scenes-view__tabs" role="tablist" aria-label={t.campana.inspector}>
              <button type="button" role="tab" aria-selected={tab === 'notas'}
                onClick={() => setTab('notas')}>
                {t.campana.inspectorTabs.notas}
              </button>
              <button type="button" role="tab" aria-selected={tab === 'historial'}
                onClick={() => setTab('historial')}>
                {t.campana.inspectorTabs.historial}
              </button>
            </div>
            <div className="scenes-view__body">
              {tab === 'notas' ? <NotesPanel campaignId={campaign.id} /> : null}
              {tab === 'historial' ? <HistoryPanel campaignId={campaign.id} /> : null}
            </div>
          </aside>
        </div>
      )}
    </section>
  );
}
