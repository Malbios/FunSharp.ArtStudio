import { FilterMenu } from './FilterMenu'
import { pageNumbers } from './pagination'
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

const MIN_PAGES_FOR_FIRST_AND_LAST = 5

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
  const showFirstAndLast = pageCount >= MIN_PAGES_FOR_FIRST_AND_LAST

  return (
    <nav className="pager" aria-label="Pages">
      {showFirstAndLast && (
        <button type="button" disabled={page === 1} onClick={() => onChange(1)}>
          « First
        </button>
      )}
      <button type="button" disabled={page === 1} onClick={() => onChange(page - 1)}>
        ‹ Prev
      </button>
      {pageNumbers(page, pageCount).map((link, index) =>
        link === '…' ? (
          <span key={`gap-${index}`} className="pager-gap">
            …
          </span>
        ) : (
          <button
            key={link}
            type="button"
            className={link === page ? 'current' : undefined}
            aria-current={link === page ? 'page' : undefined}
            onClick={() => onChange(link)}
          >
            {link}
          </button>
        ),
      )}
      <button type="button" disabled={page === pageCount} onClick={() => onChange(page + 1)}>
        Next ›
      </button>
      {showFirstAndLast && (
        <button type="button" disabled={page === pageCount} onClick={() => onChange(pageCount)}>
          Last »
        </button>
      )}
    </nav>
  )
}
