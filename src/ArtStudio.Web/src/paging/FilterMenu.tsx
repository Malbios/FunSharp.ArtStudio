import { useEffect, useRef, useState } from 'react'
import type { FilteredItems } from './useListFilter'

export function FilterMenu<T>({ filter }: { filter: FilteredItems<T> }) {
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)
  const activeLabels = filter.options.filter((option) => filter.active.includes(option.key)).map((option) => option.label)
  const filtered = activeLabels.length > 0

  useEffect(() => {
    if (!open) return
    function closeOnOutsideClick(event: MouseEvent) {
      if (!menuRef.current?.contains(event.target as Node)) setOpen(false)
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', closeOnOutsideClick)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeOnOutsideClick)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [open])

  return (
    <div className="filter-menu" ref={menuRef}>
      <button
        type="button"
        className={filtered ? 'filter-button active' : 'filter-button'}
        aria-label="Filter"
        aria-expanded={open}
        title={filtered ? `Filter: ${activeLabels.join(' + ')}` : 'Filter'}
        onClick={() => setOpen((current) => !current)}
      >
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path d="M3 4h18l-7 8.5V19l-4 2v-8.5L3 4z" fill="currentColor" />
        </svg>
      </button>
      {open && (
        <div className="filter-dropdown" role="group" aria-label="Filter">
          <label>
            <input type="checkbox" checked={!filtered} disabled={!filtered} onChange={filter.clear} />
            All ({filter.totalCount})
          </label>
          {filter.options.map((option) => (
            <label key={option.key}>
              <input
                type="checkbox"
                checked={filter.active.includes(option.key)}
                onChange={() => filter.toggle(option.key)}
              />
              {option.label} ({option.count})
            </label>
          ))}
        </div>
      )}
    </div>
  )
}
