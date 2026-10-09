import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PAGE_PARAM } from './usePagedItems'

const FILTER_PARAM = 'filter'

export interface ListFilter<T> {
  key: string
  label: string
  matches: (item: T) => boolean
}

export interface FilterOption {
  key: string
  label: string
  count: number
}

export interface FilteredItems<T> {
  items: T[] | undefined
  options: FilterOption[]
  active: string
  setActive: (key: string) => void
}

/**
 * Narrows items by the filter named in the URL (?filter=). The first filter is the default and has no parameter;
 * changing the filter goes back to page 1.
 */
export function useListFilter<T>(items: T[] | undefined, filters: ListFilter<T>[]): FilteredItems<T> {
  const [searchParams, setSearchParams] = useSearchParams()
  const requested = searchParams.get(FILTER_PARAM)
  const filter = filters.find((candidate) => candidate.key === requested) ?? filters[0]

  const setActive = useCallback(
    (key: string) =>
      setSearchParams((params) => {
        if (key === filters[0].key) params.delete(FILTER_PARAM)
        else params.set(FILTER_PARAM, key)
        params.delete(PAGE_PARAM)
        return params
      }),
    [filters, setSearchParams],
  )

  return {
    items: items?.filter(filter.matches),
    options: filters.map(({ key, label, matches }) => ({ key, label, count: items?.filter(matches).length ?? 0 })),
    active: filter.key,
    setActive,
  }
}
