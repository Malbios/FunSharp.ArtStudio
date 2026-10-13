import { FilterMenu } from './FilterMenu'
import { PAGE_LINK_SLOTS, pageNumbers } from './pagination'
import type { FilteredItems } from './useListFilter'

interface PageProps {
  page: number
  pageCount: number
  onChange: (page: number) => void
}

interface Props<T> extends PageProps {
  /** Shown as a funnel button at the far right; the row then shows even with a single page. */
  filter?: FilteredItems<T>
}

export function Pager<T>({ page, pageCount, onChange, filter }: Props<T>) {
  if (!filter) return <PageButtons page={page} pageCount={pageCount} onChange={onChange} />

  return (
    <div className="pager-row">
      <PageButtons page={page} pageCount={pageCount} onChange={onChange} />
      <FilterMenu filter={filter} />
    </div>
  )
}

function PageButtons({ page, pageCount, onChange }: PageProps) {
  if (pageCount <= 1) return null
  const links = pageNumbers(page, pageCount)
  const emptySlots = Array.from({ length: PAGE_LINK_SLOTS - links.length }, (_, index) => index)

  return (
    <nav className="pager" aria-label="Pages">
      <button type="button" disabled={page === 1} onClick={() => onChange(1)}>
        « First
      </button>
      <button type="button" disabled={page === 1} onClick={() => onChange(page - 1)}>
        ‹ Prev
      </button>
      {links.map((link, index) =>
        link === '…' ? (
          <span key={`gap-${index}`} className="pager-slot pager-gap">
            …
          </span>
        ) : (
          <button
            key={link}
            type="button"
            className={link === page ? 'pager-slot current' : 'pager-slot'}
            aria-current={link === page ? 'page' : undefined}
            onClick={() => onChange(link)}
          >
            {link}
          </button>
        ),
      )}
      {emptySlots.map((slot) => (
        <span key={`empty-${slot}`} className="pager-slot" aria-hidden="true" />
      ))}
      <button type="button" disabled={page === pageCount} onClick={() => onChange(page + 1)}>
        Next ›
      </button>
      <button type="button" disabled={page === pageCount} onClick={() => onChange(pageCount)}>
        Last »
      </button>
    </nav>
  )
}
