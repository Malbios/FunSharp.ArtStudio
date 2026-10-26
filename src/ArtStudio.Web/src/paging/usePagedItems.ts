import { useCallback, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PAGE_SIZE } from './pagination'

export const PAGE_PARAM = 'page'

export interface PagedItems<T> {
  pageItems: T[]
  page: number
  pageCount: number
  setPage: (page: number) => void
}

/**
 * Splits items into pages of PAGE_SIZE; the page number is kept in the URL so Back returns to it. Lists sharing a page
 * each need their own parameter name.
 */
export function usePagedItems<T>(items: T[] | undefined, pageParam = PAGE_PARAM): PagedItems<T> {
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedPage = Number(searchParams.get(pageParam)) || 1
  const pageCount = Math.max(1, Math.ceil((items?.length ?? 0) / PAGE_SIZE))
  const page = items ? Math.min(Math.max(1, requestedPage), pageCount) : requestedPage

  const setPage = useCallback(
    (next: number, replace = false) =>
      setSearchParams(
        (params) => {
          if (next === 1) params.delete(pageParam)
          else params.set(pageParam, String(next))
          return params
        },
        { replace },
      ),
    [setSearchParams, pageParam],
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
