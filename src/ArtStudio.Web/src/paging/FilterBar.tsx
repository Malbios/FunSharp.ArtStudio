import type { FilteredItems } from './useListFilter'

export function FilterBar<T>({ filter }: { filter: FilteredItems<T> }) {
  return (
    <nav className="pager filter-bar" aria-label="Filter">
      {filter.options.map((option) => (
        <button
          key={option.key}
          type="button"
          className={option.key === filter.active ? 'current' : undefined}
          aria-pressed={option.key === filter.active}
          onClick={() => filter.setActive(option.key)}
        >
          {option.label} ({option.count})
        </button>
      ))}
    </nav>
  )
}
