import { pageNumbers } from './pagination'

interface Props {
  page: number
  pageCount: number
  onChange: (page: number) => void
}

const MIN_PAGES_FOR_FIRST_AND_LAST = 5

export function Pager({ page, pageCount, onChange }: Props) {
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
