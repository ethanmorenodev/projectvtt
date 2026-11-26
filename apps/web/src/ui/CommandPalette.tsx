import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import './command-palette.css'

/**
 * Paleta de comandos (PMB-1.7, COMPONENT_RULES §10): búsqueda global con
 * Ctrl/Cmd+K. Resultados agrupados (acciones y contenido), navegación con
 * flechas y Enter, cierre con Escape. Sin textos embebidos en el núcleo:
 * los grupos llegan como props (ADR-038).
 */

export interface PaletteItem {
  id: string
  label: string
  hint?: string
  icon?: ReactNode
  onRun: () => void
}

export interface PaletteGroup {
  id: string
  title: string
  items: PaletteItem[]
}

export interface CommandPaletteProps {
  open: boolean
  placeholder: string
  emptyText: string
  groups: PaletteGroup[]
  onOpen: () => void
  onClose: () => void
}

export function CommandPalette({
  open,
  placeholder,
  emptyText,
  groups,
  onOpen,
  onClose,
}: CommandPaletteProps) {
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  // Atajo global: Ctrl+K / Cmd+K abre y cierra.
  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        if (open) {
          onClose()
        } else {
          onOpen()
        }
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onOpen, onClose])

  useEffect(() => {
    if (open) {
      setQuery('')
      setActive(0)
      inputRef.current?.focus()
    }
  }, [open])

  const flat = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return groups
      .map((group) => ({
        ...group,
        items: group.items.filter(
          (item) =>
            needle.length === 0 ||
            item.label.toLowerCase().includes(needle) ||
            item.hint?.toLowerCase().includes(needle),
        ),
      }))
      .filter((group) => group.items.length > 0)
  }, [groups, query])

  const total = flat.reduce((sum, group) => sum + group.items.length, 0)
  const runAt = useCallback(
    (index: number) => {
      const item = flat.flatMap((group) => group.items)[index]
      if (item) {
        item.onRun()
        onClose()
      }
    },
    [flat, onClose],
  )

  if (!open) {
    return null
  }

  return (
    <div className="pv-palette__overlay" onClick={onClose}>
      <div
        className="pv-palette"
        role="dialog"
        aria-modal="true"
        aria-label={placeholder}
        onClick={(event) => event.stopPropagation()}
      >
        <input
          ref={inputRef}
          className="pv-input pv-palette__input"
          placeholder={placeholder}
          value={query}
          role="combobox"
          aria-expanded="true"
          aria-controls="pv-palette-list"
          onChange={(event) => {
            setQuery(event.target.value)
            setActive(0)
          }}
          onKeyDown={(event) => {
            if (event.key === 'ArrowDown') {
              event.preventDefault()
              setActive((index) => (index + 1) % Math.max(total, 1))
            } else if (event.key === 'ArrowUp') {
              event.preventDefault()
              setActive((index) => (index - 1 + Math.max(total, 1)) % Math.max(total, 1))
            } else if (event.key === 'Enter') {
              event.preventDefault()
              runAt(active)
            } else if (event.key === 'Escape') {
              event.preventDefault()
              onClose()
            }
          }}
        />
        <ul className="pv-palette__list" id="pv-palette-list" role="listbox">
          {flat.length === 0 ? (
            <li className="pv-palette__empty">{emptyText}</li>
          ) : (
            flat.map((group) => (
              <li key={group.id}>
                <div className="pv-palette__group">{group.title}</div>
                <ul role="group">
                  {group.items.map((item) => {
                    const index = flat
                      .flatMap((entry) => entry.items)
                      .findIndex((candidate) => candidate.id === item.id)
                    return (
                      <li key={item.id}>
                        <button
                          type="button"
                          role="option"
                          aria-selected={index === active}
                          className={`pv-palette__item${index === active ? ' pv-palette__item--active' : ''}`}
                          onMouseEnter={() => setActive(index)}
                          onClick={() => runAt(index)}
                        >
                          {item.icon ? <span aria-hidden="true">{item.icon}</span> : null}
                          <span className="pv-palette__label">{item.label}</span>
                          {item.hint ? <span className="pv-palette__hint">{item.hint}</span> : null}
                        </button>
                      </li>
                    )
                  })}
                </ul>
              </li>
            ))
          )}
        </ul>
      </div>
    </div>
  )
}
