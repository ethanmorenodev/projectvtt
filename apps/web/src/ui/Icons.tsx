import type { ReactNode } from 'react'

/**
 * Iconografía de línea de la aplicación (maqueta objetivo 2026-11-10,
 * docs/visual/REFERENCE_LIBRARY.md §7). Set único de trazo fino,
 * monocromo y tintable; los iconos definitivos de contenido vendrán con
 * la fuente aprobada (PMB-1.10 / §17).
 */

interface IconProps {
  size?: number
  className?: string
}

function Stroke({ size = 20, className, children }: IconProps & { children: ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {children}
    </svg>
  )
}

export function IconHome(props: IconProps) {
  return (
    <Stroke {...props}>
      <path d="M4 11.5 12 4l8 7.5" />
      <path d="M6 10.5V20h12v-9.5" />
      <path d="M10 20v-5h4v5" />
    </Stroke>
  )
}

export function IconCampaigns(props: IconProps) {
  return (
    <Stroke {...props}>
      <rect x="5" y="4" width="14" height="16" rx="2" />
      <path d="M9 8h6M9 12h6M9 16h4" />
    </Stroke>
  )
}

export function IconParty(props: IconProps) {
  return (
    <Stroke {...props}>
      <circle cx="9" cy="9" r="2.5" />
      <path d="M5 18c0-2.2 1.8-4 4-4s4 1.8 4 4" />
      <circle cx="16.5" cy="9.5" r="2" />
      <path d="M14 14.5c2.2 0 4 1.6 4 3.5" />
    </Stroke>
  )
}

export function IconTable(props: IconProps) {
  return (
    <Stroke {...props}>
      <rect x="4" y="5" width="16" height="14" rx="2" />
      <path d="M4 10h16M12 10v9" />
    </Stroke>
  )
}

export function IconBook(props: IconProps) {
  return (
    <Stroke {...props}>
      <path d="M12 6.5C10.5 5 8 4.5 5 4.5v13c3 0 5.5.5 7 2 1.5-1.5 4-2 7-2v-13c-3 0-5.5.5-7 2Z" />
      <path d="M12 6.5v13" />
    </Stroke>
  )
}

export function IconGear(props: IconProps) {
  return (
    <Stroke {...props}>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 3.5v2.2M12 18.3v2.2M20.5 12h-2.2M5.7 12H3.5M18 6l-1.6 1.6M7.6 16.4 6 18M18 18l-1.6-1.6M7.6 7.6 6 6" />
    </Stroke>
  )
}

export function IconSearch(props: IconProps) {
  return (
    <Stroke {...props}>
      <circle cx="10.5" cy="10.5" r="5.5" />
      <path d="m15 15 4.5 4.5" />
    </Stroke>
  )
}

export function IconChevronDown(props: IconProps) {
  return (
    <Stroke {...props}>
      <path d="m7 10 5 5 5-5" />
    </Stroke>
  )
}
