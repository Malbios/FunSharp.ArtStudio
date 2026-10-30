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
 * Splits items into pages of pageSize; the page number is kept in the URL so Back returns to it. Lists sharing a page
 * each need their own parameter name.
 */
export function usePagedItems<T>(items: T[] | undefined, pageParam = PAGE_PARAM, pageSize = PAGE_SIZE): PagedItems<T> {
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedPage = Number(searchParams.get(pageParam)) || 1
  const pageCount = Math.max(1, Math.ceil((items?.length ?? 0) / pageSize))
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
    pageItems: items?.slice((page - 1) * pageSize, page * pageSize) ?? [],
    page,
    pageCount,
    setPage,
  }
}
