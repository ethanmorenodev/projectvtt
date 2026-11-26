import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import type { ParsedRoute } from '../routing/router'
import { CommandPalette } from '../ui/CommandPalette'
import type { PaletteGroup } from '../ui/CommandPalette'
import { ShortcutsHelp } from '../ui/ShortcutsHelp'
import { t } from '../i18n'
import './shell.css'

/**
 * Shell de la aplicación (dirección revisada por el consejo 2026-11-25):
 * raíl ACOPLADO y siempre visible, sin cápsula flotante, sin flecha, sin
 * «Más» ni pista emergente. Ocho destinos planos en tres grupos; cabecera
 * con migas alineadas al contenido y una sola acción primaria por página.
 */

export type ShellArea =
  | 'inicio' | 'campanas' | 'campana' | 'mesa' | 'biblioteca'
  | 'imagenes' | 'creditos' | 'herramientas' | 'dm' | 'lore' | 'gestion'

interface RailItem {
  area: ShellArea
  label: string
  icon: string
  route: ParsedRoute | null
}

export interface AppShellProps {
  active: ShellArea
  breadcrumb?: string
  userName: string
  campaignId?: string
  sceneId?: string
  onNavigate: (route: ParsedRoute) => void
  onSignOut: () => void
  palette: { placeholder: string; emptyText: string; groups: PaletteGroup[] }
  children: ReactNode
}

export function AppShell({
  active,
  breadcrumb,
  userName,
  onNavigate,
  onSignOut,
  palette,
  children,
}: AppShellProps) {
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [helpOpen, setHelpOpen] = useState(false)

  // La ayuda de atajos se abre con '?' desde cualquier parte (no compite en
  // la cabecera) y nunca cuando se está escribiendo en un campo.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      const typing = target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA'
        || target?.tagName === 'SELECT' || target?.isContentEditable
      if (event.key === '?' && !typing) {
        event.preventDefault()
        setHelpOpen(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const groups: Array<{ label: string; items: RailItem[] }> = [
    {
      label: t.shell.groups.jugar,
      items: [
        { area: 'inicio', label: t.shell.areas.inicio, icon: 'house', route: { kind: 'inicio' } },
        { area: 'campanas', label: t.shell.areas.campanas, icon: 'castle', route: { kind: 'campaigns' } },
      ],
    },
    {
      label: t.shell.groups.recursos,
      items: [
        { area: 'biblioteca', label: t.shell.areas.biblioteca, icon: 'open-book', route: { kind: 'biblioteca' } },
        { area: 'imagenes', label: t.shell.areas.imagenes, icon: 'gaze', route: { kind: 'imagenes' } },
      ],
    },
    {
      label: t.shell.groups.herramientas,
      items: [
        { area: 'herramientas', label: t.shell.areas.herramientas, icon: 'cog', route: { kind: 'herramientas' } },
        { area: 'gestion', label: t.shell.areas.gestion, icon: 'gears', route: { kind: 'gestion' } },
      ],
    },
  ]

  return (
    <div className="app-shell">
      <a className="app-shell__skip" href="#contenido-principal">{t.shell.skip}</a>

      <nav className="app-shell__rail" aria-label={t.shell.navLabel}>
        <img className="app-shell__brand" src="/logo-projectvtt.png" alt={t.shell.brandAlt} />
        {groups.map((group) => (
          <div className="app-shell__group" key={group.label}>
            <h2 className="app-shell__group-label" aria-hidden="true">{group.label}</h2>
            <div role="group" aria-label={group.label}>
              {group.items.map((item) => (
                <button
                  key={item.area}
                  type="button"
                  className={`app-shell__rail-item${active === item.area ? ' app-shell__rail-item--active' : ''}`}
                  aria-label={item.label}
                  aria-current={active === item.area ? 'page' : undefined}
                  title={item.route ? undefined : `${item.label} — ${t.shell.campaignRequired}`}
                  onClick={() => item.route && onNavigate(item.route)}
                >
                  <img src={`/icons/${item.icon}.svg`} alt="" aria-hidden="true" />
                  <span>{item.label}</span>
                </button>
              ))}
            </div>
          </div>
        ))}
        <button type="button" className="app-shell__user"
          title={`${t.shell.signOut} (${userName})`}
          aria-label={`${t.shell.signOut} (${userName})`} onClick={onSignOut}>
          <span className="app-shell__avatar" aria-hidden="true">
            {userName.trim().charAt(0).toUpperCase()}
            <span className="app-shell__status" />
          </span>
          <span className="app-shell__username">{userName}</span>
        </button>
      </nav>

      <div className="app-shell__main">
        <header className="app-shell__topbar">
          <div className="app-shell__crumb">
            {breadcrumb && <span aria-current="page">{breadcrumb}</span>}
          </div>
          <div className="app-shell__top-actions">
            <button type="button" className="app-shell__search"
              aria-label={t.shell.searchPlaceholder}
              onClick={() => setPaletteOpen(true)}>
              <img src="/icons/magnifying-glass.svg" alt="" aria-hidden="true" />
              <span>{palette.placeholder}</span>
            </button>
          </div>
        </header>
        <div className="app-shell__content" id="contenido-principal">{children}</div>
      </div>

      <ShortcutsHelp open={helpOpen} onClose={() => setHelpOpen(false)} showMesa={active === 'mesa'} />

      <CommandPalette
        open={paletteOpen}
        placeholder={palette.placeholder}
        emptyText={palette.emptyText}
        groups={palette.groups}
        onOpen={() => setPaletteOpen(true)}
        onClose={() => setPaletteOpen(false)}
      />
    </div>
  )
}
