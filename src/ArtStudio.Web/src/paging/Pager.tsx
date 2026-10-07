import { pageNumbers } from './pagination'

interface Props {
  page: number
  pageCount: number
  onChange: (page: number) => void
}

export function Pager({ page, pageCount, onChange }: Props) {
  if (pageCount <= 1) return null

  return (
    <nav className="pager" aria-label="Pages">
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
    </nav>
  )
}
