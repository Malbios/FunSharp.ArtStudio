import { useCallback, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PAGE_SIZE } from './pagination'

const PAGE_PARAM = 'page'

export interface PagedItems<T> {
  pageItems: T[]
  page: number
  pageCount: number
  setPage: (page: number) => void
}

/** Splits items into pages of PAGE_SIZE; the page number is kept in the URL so Back returns to it. */
export function usePagedItems<T>(items: T[] | undefined): PagedItems<T> {
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedPage = Number(searchParams.get(PAGE_PARAM)) || 1
  const pageCount = Math.max(1, Math.ceil((items?.length ?? 0) / PAGE_SIZE))
  const page = items ? Math.min(Math.max(1, requestedPage), pageCount) : requestedPage

  const setPage = useCallback(
    (next: number, replace = false) =>
      setSearchParams(
        (params) => {
          if (next === 1) params.delete(PAGE_PARAM)
          else params.set(PAGE_PARAM, String(next))
          return params
        },
        { replace },
      ),
    [setSearchParams],
  )

  useEffect(() => {
    if (items && page !== requestedPage) setPage(page, true)
  }, [items, page, requestedPage, setPage])

  return {
    pageItems: items?.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE) ?? [],
    page,
    pageCount,
    setPage,
  }
}
