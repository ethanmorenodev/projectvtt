/**
 * Shell de la aplicación (Épica D → PMB-1.4): sesión, navegación por áreas
 * con URLs reales e historial, y vistas del MVP montadas dentro del shell.
 * El mapa, la cuadrícula y los tokens viven en `views/SceneView`.
 */
import { useEffect, useRef, useState } from 'react'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { apiFetch } from './api/client'
import type { CampaignDetail, CampaignSummary, SceneDetail, SceneSummary } from './api/types'
import { CampaignsView } from './views/CampaignsView'
import { InicioView } from './views/InicioView'
import { LoginView } from './views/LoginView'
import { SceneView } from './views/SceneView'
import { CampaignSpaceView } from './views/CampaignSpaceView'
import { CharacterCreatorView } from './views/CharacterCreatorView'
import { LibraryView } from './views/LibraryView'
import { ImageLibraryView } from './views/ImageLibraryView'
import { CreditsView } from './views/CreditsView'
import { HerramientasView } from './views/HerramientasView'
import { AjustesView } from './views/AjustesView'
import { installAudioUnlock } from './audio/sound'
import { DmFlowView } from './views/DmFlowView'
import { InventoryView } from './views/InventoryView'
import { HomebrewClassView } from './views/HomebrewClassView'
import { LoreView } from './views/LoreView'
import { UiCatalog } from './ui/UiCatalog'
import { ShellPrototype } from './ui/prototype/ShellPrototype'
import { CampanasPrototype, InicioPrototype, PrototypeShell } from './ui/prototype/fase2/AreasFase2'
import { EspacioCampanaPrototype } from './ui/prototype/fase2/EspacioCampanaPrototype'
import { MesaPrototype } from './ui/prototype/fase2/MesaPrototype'
import { CreadorPrototype } from './ui/prototype/fase3/CreadorPrototype'
import { CharacterHubPrototype } from './ui/prototype/fase3/CharacterHubPrototype'
import { FichaPrototype } from './ui/prototype/fase3/FichaPrototype'
import { BibliotecaPrototype } from './ui/prototype/fase4/BibliotecaPrototype'
import { EditorPrototype } from './ui/prototype/fase4/EditorPrototype'
import {
  CreditsPrototype,
  ImageLibraryPrototype,
} from './ui/prototype/fase4/ImageAssetsPrototype'
import { DmFlowPrototype } from './ui/prototype/fase4/DmFlowPrototype'
import { ReplaceAssetPrototype } from './ui/prototype/fase4/ReplaceAssetPrototype'
import { InventoryPrototype } from './ui/prototype/fase6/InventoryPrototype'
import { MapToolsPrototype } from './ui/prototype/fase7/MapToolsPrototype'
import { VisualAssetsPrototype } from './ui/prototype/fase8/VisualAssetsPrototype'
import { ModernLayoutPrototype } from './ui/prototype/fase8/ModernLayoutPrototype'
import { CreatorVisualPrototype } from './ui/prototype/exp/CreatorVisualPrototype'
import { InventoryVisualPrototype } from './ui/prototype/exp/InventoryVisualPrototype'
import { HomebrewClassPrototype } from './ui/prototype/exp/HomebrewClassPrototype'
import { DistributionPrototype } from './ui/prototype/exp/DistributionPrototype'
import { AlchemyHomePrototype } from './ui/prototype/exp/AlchemyHomePrototype'
import { AlchemyV2Prototype } from './ui/prototype/exp/AlchemyV2Prototype'
import { AlchemyTablePrototype } from './ui/prototype/exp/AlchemyTablePrototype'
import { CampaignSpacePrototype } from './ui/prototype/exp/CampaignSpacePrototype'
import { AppShell } from './shell/AppShell'
import type { ShellArea } from './shell/AppShell'
import type { PaletteGroup } from './ui/CommandPalette'
import { navigate, useLocation, useNavigate } from './routing/router'
import type { ParsedRoute } from './routing/router'
import { Button, EmptyState, ToastProvider, useToast } from './ui'
import { Icon } from './ui/Icon'
import { OnboardingTour } from './ui/OnboardingTour'
import { SESSION_EXPIRED_EVENT } from './api/client'
import { t } from './i18n'
import './views/presentation.css'

/** Estado de la ruta actual: objetos ya cargados (o null mientras hidratan). */
interface Workspace {
  campaign: CampaignDetail | null
  scene: SceneDetail | null
  loading: boolean
  error: string | null
}

function areaFor(route: ParsedRoute, workspace: Workspace): ShellArea {
  switch (route.kind) {
    case 'inicio':
      return 'inicio'
    // El espacio de campaña y la mesa viven DENTRO de cada campaña; en el
    // raíl global permanece activo «Campañas» para no perder el contexto.
    case 'campaign':
    case 'inventario':
    case 'clase-crear':
    case 'scene':
    case 'table':
      return 'campanas'
    case 'biblioteca':
      return 'biblioteca'
    case 'imagenes':
      return 'imagenes'
    case 'creditos':
      return 'creditos'
    case 'herramientas':
    case 'ajustes':
      return 'herramientas'
    case 'dm':
      return 'dm'
    case 'lore':
      return 'lore'
    case 'gestion':
      return 'gestion'
    default:
      return workspace.campaign ? 'campana' : 'campanas'
  }
}

/** Migas de pan de la cabecera: la ruta real, sin duplicar la navegación. */
function breadcrumbFor(
  route: ParsedRoute,
  workspace: Workspace,
): string {
  const campaign = workspace.campaign?.name ?? 'Campaña'
  switch (route.kind) {
    case 'inicio': return 'Inicio'
    case 'campaign': return `Campañas / ${campaign}`
    case 'scene':
    case 'table': return `Campañas / ${campaign} / Mesa`
    case 'character-create':
    case 'character-draft': return `Campañas / ${campaign} / Personaje`
    case 'biblioteca': return 'Biblioteca / Explorador'
    case 'imagenes': return 'Biblioteca / Imágenes'
    case 'creditos': return 'Recursos / Créditos'
    case 'herramientas': return 'Herramientas'
    case 'ajustes': return 'Herramientas / Ajustes de sonido'
    case 'dm': return 'Herramientas / Flujo del DM'
    case 'lore': return 'Herramientas / Historia y lore'
    case 'inventario': return 'Personajes / Inventario'
    case 'clase-crear': return 'Personajes / Clase propia'
    case 'gestion': return 'Gestión'
    default: return 'Campañas'
  }
}

function Shell() {
  const { user, loading, logout } = useAuth()
  const toast = useToast()
  const { route } = useLocation()
  const go = useNavigate()
  const [workspace, setWorkspace] = useState<Workspace>({
    campaign: null,
    scene: null,
    loading: false,
    error: null,
  })
  const lastUserId = useRef<string | undefined>(undefined)
  const [campaignList, setCampaignList] = useState<CampaignSummary[]>([])

  // Campañas a mano para la paleta de comandos (búsqueda global, PMB-1.7).
  useEffect(() => {
    if (!user) {
      setCampaignList([])
      return
    }
    let cancelled = false
    void apiFetch<CampaignSummary[]>('/api/v1/campaigns')
      .then((campaigns) => {
        if (!cancelled) {
          setCampaignList(campaigns)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setCampaignList([])
        }
      })
    return () => {
      cancelled = true
    }
  }, [user?.id])

  // Al cambiar de sesión (logout → login de otro usuario) la ruta anterior
  // apuntaba al usuario anterior: se restablece para no montar vistas con
  // datos obsoletos. Solo al entrar un usuario: al salir manda el flujo de
  // logout (→ /login) y la primera restauración conserva el deep-link.
  useEffect(() => {
    if (lastUserId.current !== undefined && lastUserId.current !== user?.id) {
      setWorkspace({ campaign: null, scene: null, loading: false, error: null })
      if (user) {
        navigate('/campaigns', true)
      }
    }
    lastUserId.current = user?.id
  }, [user?.id])

  // Clave estable de la ruta: `parseRoute` devuelve un objeto nuevo en
  // cada render y sin esto el efecto de hidratación se relanzaría siempre.
  const routeKey = JSON.stringify(route)

  // Hidratación de deep-links: al entrar por URL directa (o recargar) se
  // cargan la campaña y la escena que la ruta pide.
  useEffect(() => {
    if (!user) {
      return
    }
    let cancelled = false

    async function hydrate() {
      if (route.kind === 'campaign' || route.kind === 'scene' || route.kind === 'table'
        || route.kind === 'character-create' || route.kind === 'character-draft') {
        setWorkspace((current) => ({
          ...current,
          loading: true,
          error: null,
          // Cambiar de campaña descarta la escena anterior.
          scene: current.campaign?.id === route.campaignId ? current.scene : null,
        }))
        try {
          const campaign = await apiFetch<CampaignDetail>(`/api/v1/campaigns/${route.campaignId}`)
          if (cancelled) {
            return
          }
          if (route.kind === 'scene') {
            const scene = await apiFetch<SceneDetail>(`/api/v1/scenes/${route.sceneId}`)
            if (cancelled) {
              return
            }
            setWorkspace({ campaign, scene, loading: false, error: null })
          } else if (route.kind === 'table') {
            // Entrar en «Mesa de juego» abre la primera escena de la campaña;
            // desde la propia mesa se puede cambiar de escena.
            const scenes = await apiFetch<SceneSummary[]>(
              `/api/v1/campaigns/${route.campaignId}/scenes`)
            if (cancelled) {
              return
            }
            const first = scenes[0]
            const scene = first
              ? await apiFetch<SceneDetail>(`/api/v1/scenes/${first.id}`)
              : null
            if (cancelled) {
              return
            }
            setWorkspace({ campaign, scene, loading: false, error: null })
          } else {
            setWorkspace({ campaign, scene: null, loading: false, error: null })
          }
        } catch (cause) {
          if (!cancelled) {
            setWorkspace({
              campaign: null,
              scene: null,
              loading: false,
              error: cause instanceof Error ? cause.message : 'No se pudo cargar la ruta.',
            })
          }
        }
      } else if (route.kind === 'campaigns' || route.kind === 'home' || route.kind === 'inicio') {
        setWorkspace((current) =>
          current.campaign || current.scene
            ? { campaign: null, scene: null, loading: false, error: null }
            : current,
        )
      }
    }

    void hydrate()
    return () => {
      cancelled = true
    }
  }, [routeKey, user?.id])

  // Sesión caducada (PMB-2.9): aviso único y vuelta al login. El logout se
  // completa antes de navegar para que el redirect de /login no compita.
  useEffect(() => {
    function onExpired() {
      void (async () => {
        toast.show({
          title: t.comunes.sessionExpiredTitle,
          text: t.comunes.sessionExpiredText,
          tone: 'warning',
        })
        await logout()
        navigate('/login')
      })()
    }
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired)
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired)
  }, [logout, toast])

  // Sesión activa sobre /login: se redirige al área de campañas.
  useEffect(() => {
    if (user && route.kind === 'login') {
      navigate('/campaigns', true)
    }
  }, [user, route.kind])

  if (loading) {
    return (
      <main>
        <h1>ProjectVTT</h1>
        <p>Cargando sesión…</p>
      </main>
    )
  }

  if (!user) {
    return (
      // Fondo temático del propietario (neutro/oscuro/claro según el SO)
      // con el logotipo y el formulario sobre panel translúcido.
      <div className="login-screen">
        <div className="login-panel">
          <img
            className="pv-logo pv-logo--hero"
            src="/logo-projectvtt.png"
            alt="Logotipo de ProjectVTT"
          />
          <h1 className="pv-visually-hidden">ProjectVTT</h1>
          <LoginView />
        </div>
      </div>
    )
  }

  if (route.kind === 'login') {
    return null
  }

  const { campaign, scene, loading: workspaceLoading, error } = workspace

  const content = (() => {
    if (error) {
      return (
        <main>
          <p className="muted">{error}</p>
          <button type="button" className="link" onClick={() => go({ kind: 'campaigns' })}>
            Volver a campañas
          </button>
        </main>
      )
    }
    if (workspaceLoading) {
      return (
        <main>
          <p className="muted">Cargando…</p>
        </main>
      )
    }

    switch (route.kind) {
      case 'inicio':
        return (
          <InicioView
            onOpenCampaign={(next) => go({ kind: 'campaign', campaignId: next.id })}
            onOpenAll={() => go({ kind: 'campaigns' })}
            onOpenLibrary={() => go({ kind: 'biblioteca' })}
          />
        )
      case 'biblioteca':
        return <LibraryView />
      case 'herramientas':
        return <HerramientasView campaignId={campaign?.id} />
      case 'ajustes':
        return <AjustesView />
      case 'imagenes':
        return <ImageLibraryView />
      case 'dm':
        return <DmFlowView />
      case 'inventario':
        return <InventoryView />
      case 'clase-crear':
        return <HomebrewClassView />
      case 'lore':
        return <LoreView />
      case 'gestion':
      case 'creditos':
        return <CreditsView />
      case 'campaign':
        return campaign ? (
          <CampaignSpaceView
            campaign={campaign}
            onOpenLore={() => go({ kind: 'lore' })}
            onOpenScene={(next) =>
              go({ kind: 'scene', campaignId: campaign.id, sceneId: next.id })
            }
            onCreateCharacter={() =>
              go({ kind: 'character-create', campaignId: campaign.id })
            }
            onOpenActor={(actorId) => go({ kind: 'inventario', actorId })}
            onOpenDraft={(actorId) =>
              go({ kind: 'character-draft', campaignId: campaign.id, actorId })
            }
            onChanged={() => {
              // Tras gestionar (renombrar/archivar): refresca el contexto.
              void apiFetch<CampaignDetail>(`/api/v1/campaigns/${campaign.id}`)
                .then((next) =>
                  setWorkspace((current) => ({ ...current, campaign: next })),
                )
                .catch(() => undefined)
            }}
          />
        ) : null
      case 'scene':
        return campaign && scene ? (
          <SceneView
            key={scene.id}
            campaign={campaign}
            scene={scene}
            onBack={() => go({ kind: 'campaign', campaignId: campaign.id })}
            onOpenScene={(next) =>
              go({ kind: 'scene', campaignId: campaign.id, sceneId: next.id })
            }
            onOpenLibrary={() => go({ kind: 'biblioteca' })}
          />
        ) : null
      case 'table':
        if (campaign && scene) {
          return (
            <SceneView
              key={scene.id}
              campaign={campaign}
              scene={scene}
              onBack={() => go({ kind: 'campaign', campaignId: campaign.id })}
              onOpenScene={(next) =>
                go({ kind: 'scene', campaignId: campaign.id, sceneId: next.id })
              }
              onOpenLibrary={() => go({ kind: 'biblioteca' })}
            />
          )
        }
        return campaign ? (
          <div className="table-empty">
            <EmptyState
              icon={<Icon name="gamepad" />}
              title={t.mesa.noScenes}
              text={t.mesa.noScenesText}
              action={
                <Button variant="primary"
                  onClick={() => go({ kind: 'campaign', campaignId: campaign.id })}>
                  {t.mesa.manageScenes}
                </Button>
              }
            />
          </div>
        ) : null
      case 'character-create':
        return campaign ? (
          <CharacterCreatorView
            campaign={campaign}
            onDone={() => go({ kind: 'campaign', campaignId: campaign.id })}
            onExit={() => go({ kind: 'campaign', campaignId: campaign.id })}
          />
        ) : null
      case 'character-draft':
        return campaign ? (
          <CharacterCreatorView
            campaign={campaign}
            draftActorId={route.actorId}
            onDone={() => go({ kind: 'campaign', campaignId: campaign.id })}
            onExit={() => go({ kind: 'campaign', campaignId: campaign.id })}
          />
        ) : null
      default:
        return <CampaignsView onOpen={(next) => go({ kind: 'campaign', campaignId: next.id })} />
    }
  })()

  return (
    <>
    <AppShell
      active={areaFor(route, workspace)}
      breadcrumb={breadcrumbFor(route, workspace)}
      userName={user.userName}
      campaignId={campaign?.id}
      sceneId={scene?.id}
      onNavigate={go}
      onSignOut={() => {
        void (async () => {
          await logout()
          navigate('/login')
        })()
      }}
      palette={{
        placeholder: t.shell.searchPlaceholder,
        emptyText: t.shell.paletteEmpty,
        groups: paletteGroups(go, campaignList, campaign),
      }}
    >
      {content}
    </AppShell>
    <OnboardingTour />
    </>
  )
}

/** Grupos de la paleta: navegación, campañas y acciones del contexto actual. */
function paletteGroups(
  go: (route: ParsedRoute) => void,
  campaigns: CampaignSummary[],
  campaign: CampaignDetail | null,
): PaletteGroup[] {
  const p = t.shell.palette
  const groups: PaletteGroup[] = [
    {
      id: 'acciones',
      title: p.actions,
      items: [
        { id: 'go-inicio', label: 'Ir a Inicio', onRun: () => go({ kind: 'inicio' }) },
        { id: 'go-campanas', label: 'Ir a Campañas', onRun: () => go({ kind: 'campaigns' }) },
        { id: 'go-biblioteca', label: 'Ir a Biblioteca', onRun: () => go({ kind: 'biblioteca' }) },
      ],
    },
  ]

  if (campaign) {
    groups.push({
      id: 'aqui',
      title: `${p.here} · ${campaign.name}`,
      items: [
        {
          id: 'camp-space', label: p.campaignSpace,
          onRun: () => go({ kind: 'campaign', campaignId: campaign.id }),
        },
        {
          id: 'new-character', label: p.newCharacter,
          onRun: () => go({ kind: 'character-create', campaignId: campaign.id }),
        },
        { id: 'lore', label: p.lore, onRun: () => go({ kind: 'lore' }) },
        { id: 'dm', label: p.dm, onRun: () => go({ kind: 'dm' }) },
      ],
    })
  }

  groups.push({
    id: 'herramientas',
    title: p.tools,
    items: [
      { id: 'imagenes', label: p.images, onRun: () => go({ kind: 'imagenes' }) },
      { id: 'creditos', label: p.credits, onRun: () => go({ kind: 'creditos' }) },
      { id: 'clase', label: t.shell.createClass, onRun: () => go({ kind: 'clase-crear' }) },
    ],
  })

  groups.push({
    id: 'campanas',
    title: p.campaigns,
    items: campaigns.map((item) => ({
      id: `camp-${item.id}`,
      label: item.name,
      hint: item.role,
      onRun: () => go({ kind: 'campaign', campaignId: item.id }),
    })),
  })

  return groups
}

export function App() {
  // ADR-049: el contexto de audio se crea/reanuda solo tras un gesto real
  // (clic o tecla), nunca al cargar la aplicación.
  useEffect(() => installAudioUnlock(), [])

  // Catálogo de componentes (PMB-1.2, solo desarrollo): `#ui-kit`. No es
  // una pantalla del producto; sirve para revisar y capturar el sistema
  // visual antes de aprobar pantallas.
  if (import.meta.env.DEV && window.location.hash === '#ui-kit') {
    return <UiCatalog />
  }

  // Prototipo del shell de navegación (PMB-1.3, solo desarrollo):
  // `#prototipo-shell`. El shell real ya existe (PMB-1.4); el prototipo
  // queda como referencia del proceso de aprobación visual.
  if (import.meta.env.DEV && window.location.hash === '#prototipo-shell') {
    return <ShellPrototype />
  }

  // Prototipo del editor universal (PMB-4.4, solo desarrollo):
  // `#prototipo-editor[-error|-nuevo|-solo-lectura]`. Regex propio porque
  // sus variantes no comparten las de la Fase 2. Pendiente de aprobación.
  const editorProto = import.meta.env.DEV
    ? window.location.hash.match(
        /^#prototipo-editor(-(error|nuevo|solo-lectura))?$/,
      )
    : null
  if (editorProto) {
    return (
      <EditorPrototype
        variant={(editorProto[2] ?? 'normal') as 'normal' | 'error' | 'nuevo' | 'solo-lectura'}
      />
    )
  }

  // Prototipo del explorador de Biblioteca (PMB-4.1/4.5/4.6, solo desarrollo):
  // `#prototipo-biblioteca[-vacio|-extremo|-acciones|-relaciones|-relaciones-vacio]`.
  // Regex propio porque sus variantes no comparten las de la Fase 2.
  const bibliotecaProto = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-biblioteca(-(vacio|extremo|acciones|relaciones|relaciones-vacio))?$/)
    : null
  if (bibliotecaProto) {
    return (
      <BibliotecaPrototype
        variant={(bibliotecaProto[2] ?? 'normal') as 'normal' | 'vacio' | 'extremo' | 'acciones' | 'relaciones' | 'relaciones-vacio'}
      />
    )
  }

  // Prototipos de experiencia (EXP-1/EXP-2/EXP-3, solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-clase-homebrew') {
    return <HomebrewClassPrototype />
  }
  if (import.meta.env.DEV && window.location.hash === '#prototipo-creador-visual') {
    return <CreatorVisualPrototype />
  }
  if (import.meta.env.DEV && window.location.hash === '#prototipo-inventario-visual') {
    return <InventoryVisualPrototype />
  }

  // Prototipo de rediseño moderno (solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-rediseno-moderno') {
    return <ModernLayoutPrototype />
  }

  // Prototipo del consejo de diseño para el espacio de campaña (solo desarrollo).
  if (import.meta.env.DEV && window.location.hash.startsWith('#prototipo-espacio-campana')) {
    return <CampaignSpacePrototype />
  }

  // Prototipo de mesa acoplada estilo Alchemy (solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-mesa-alchemy') {
    return <AlchemyTablePrototype />
  }

  // Prototipo V2: raíl oculto y menos opciones (solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-alchemy-v2') {
    return <AlchemyV2Prototype />
  }

  // Prototipo D1+D2: Inicio editorial inspirado en Alchemy (solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-alchemy') {
    return <AlchemyHomePrototype />
  }

  // Prototipo de reorganización global inspirado en Alchemy (solo desarrollo).
  if (import.meta.env.DEV) {
    const distribucion = window.location.hash.match(/^#prototipo-distribucion(?:-(mesa|biblioteca|herramientas))?$/)
    if (distribucion) {
      return <DistributionPrototype variant={(distribucion[1] ?? 'inicio') as 'inicio' | 'mesa' | 'biblioteca' | 'herramientas'} />
    }
  }

  // Prototipo de mejora visual con iconografía (solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-mejora-visual') {
    return <VisualAssetsPrototype />
  }

  // Prototipo de herramientas de mapa (PMB-7.5, solo desarrollo).
  const mapToolsProto = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-mapa-herramientas(-vacio)?$/)
    : null
  if (mapToolsProto) {
    return <MapToolsPrototype variant={mapToolsProto[1] ? 'vacio' : 'normal'} />
  }

  // Prototipo de inventario visual (PMB-6.3, solo desarrollo).
  const invProto = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-inventario(-vacio)?$/)
    : null
  if (invProto) {
    return <InventoryPrototype variant={invProto[1] ? 'vacio' : 'normal'} />
  }

  // Prototipo de sustitución de recursos (PMB-4.10, solo desarrollo).
  const replaceProto = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-sustituir(-vacio)?$/)
    : null
  if (replaceProto) {
    return <ReplaceAssetPrototype variant={replaceProto[1] ? 'vacio' : 'normal'} />
  }

  // Prototipo del flujo del DM (PMB-4.8, solo desarrollo).
  const dmProto = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-dm(-vacio)?$/)
    : null
  if (dmProto) {
    return <DmFlowPrototype variant={dmProto[1] ? 'vacio' : 'normal'} />
  }

  // Prototipo de biblioteca de imágenes y créditos (PMB-4.7, solo desarrollo).
  if (import.meta.env.DEV && window.location.hash === '#prototipo-creditos') {
    return <CreditsPrototype />
  }
  const imageProto = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-imagenes(-vacio)?$/)
    : null
  if (imageProto) {
    return <ImageLibraryPrototype variant={imageProto[1] ? 'vacio' : 'normal'} />
  }

  // Prototipos de contenido de la Fase 2 (PMB-2.1/2.3/2.6, solo
  // desarrollo), montados en el shell real para revisión. Variantes:
  // normal, `vacio`, `extremo` y `sin-resultados`. Pendientes de aprobación.
  // `creador` es el prototipo de la Fase 3 (PMB-3.4).
  const fase2 = import.meta.env.DEV
    ? window.location.hash.match(/^#prototipo-(inicio|campanas|espacio|mesa|creador|hub|ficha)(?:-(vacio|extremo|sin-resultados))?$/)
    : null
  if (fase2) {
    const variant = (fase2[2] ?? 'normal') as 'normal' | 'vacio' | 'extremo' | 'sin-resultados'
    if (fase2[1] === 'ficha') {
      return <FichaPrototype variant={variant === 'sin-resultados' ? 'normal' : variant} />
    }
    if (fase2[1] === 'hub') {
      return <CharacterHubPrototype variant={variant === 'sin-resultados' ? 'normal' : variant} />
    }
    if (fase2[1] === 'creador') {
      return <CreadorPrototype variant={variant === 'sin-resultados' ? 'normal' : variant} />
    }
    if (fase2[1] === 'espacio') {
      return <EspacioCampanaPrototype variant={variant === 'sin-resultados' ? 'normal' : variant} />
    }
    if (fase2[1] === 'mesa') {
      return <MesaPrototype variant={variant === 'sin-resultados' ? 'normal' : variant} />
    }
    return (
      <PrototypeShell active={fase2[1] === 'inicio' ? 'inicio' : 'campanas'}>
        {fase2[1] === 'inicio' ? (
          <InicioPrototype variant={variant} />
        ) : (
          <CampanasPrototype variant={variant} />
        )}
      </PrototypeShell>
    )
  }

  return (
    <AuthProvider>
      <ToastProvider>
        <Shell />
      </ToastProvider>
    </AuthProvider>
  )
}
