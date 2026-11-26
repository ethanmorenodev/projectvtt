import './ui.css'

/**
 * Chip de filtro (promovido de los prototipos PMB-2.1/2.3, decisión del
 * propietario 2026-11-09). Estados: normal/seleccionado; exclusión
 * múltiple gestionada por quien lo usa. Reglas: COMPONENT_RULES §14.
 */

export interface FilterChipProps {
  label: string
  selected?: boolean
  onClick?: () => void
}

export function FilterChip({ label, selected, onClick }: FilterChipProps) {
  return (
    <button
      type="button"
      className={`pv-chip${selected ? ' pv-chip--selected' : ''}`}
      aria-pressed={selected ?? false}
      onClick={onClick}
    >
      {label}
    </button>
  )
}
