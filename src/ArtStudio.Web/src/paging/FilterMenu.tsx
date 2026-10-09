import { useEffect, useRef, useState } from 'react'
import type { FilteredItems } from './useListFilter'

export function FilterMenu<T>({ filter }: { filter: FilteredItems<T> }) {
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)
  const activeOption = filter.options.find((option) => option.key === filter.active)
  const filtered = filter.active !== filter.options[0].key

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

  function choose(key: string) {
    filter.setActive(key)
    setOpen(false)
  }

  return (
    <div className="filter-menu" ref={menuRef}>
      <button
        type="button"
        className={filtered ? 'filter-button active' : 'filter-button'}
        aria-label="Filter"
        aria-expanded={open}
        title={filtered ? `Filter: ${activeOption?.label}` : 'Filter'}
        onClick={() => setOpen((current) => !current)}
      >
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path d="M3 4h18l-7 8.5V19l-4 2v-8.5L3 4z" fill="currentColor" />
        </svg>
      </button>
      {open && (
        <div className="filter-dropdown" role="radiogroup" aria-label="Filter">
          {filter.options.map((option) => (
            <label key={option.key}>
              <input
                type="radio"
                name="list-filter"
                checked={option.key === filter.active}
                onChange={() => choose(option.key)}
              />
              {option.label} ({option.count})
            </label>
          ))}
        </div>
      )}
    </div>
  )
}
