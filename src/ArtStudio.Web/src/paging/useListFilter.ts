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

const storageKey = (list: string) => `artstudio.filter.${list}`

function rememberedFilter(list: string): string | null {
  try {
    return localStorage.getItem(storageKey(list))
  } catch {
    return null
  }
}

function rememberFilter(list: string, key: string) {
  try {
    localStorage.setItem(storageKey(list), key)
  } catch {
    // Without storage the filter is simply not remembered.
  }
}

/**
 * Narrows items by the filter named in the URL (?filter=), or else by the one last chosen for this list in this
 * browser. The first filter is the default and has no parameter; changing the filter goes back to page 1.
 */
export function useListFilter<T>(list: string, items: T[] | undefined, filters: ListFilter<T>[]): FilteredItems<T> {
  const [searchParams, setSearchParams] = useSearchParams()
  const requested = searchParams.get(FILTER_PARAM) ?? rememberedFilter(list)
  const filter = filters.find((candidate) => candidate.key === requested) ?? filters[0]

  const setActive = useCallback(
    (key: string) => {
      rememberFilter(list, key)
      setSearchParams((params) => {
        if (key === filters[0].key) params.delete(FILTER_PARAM)
        else params.set(FILTER_PARAM, key)
        params.delete(PAGE_PARAM)
        return params
      })
    },
    [list, filters, setSearchParams],
  )

  return {
    items: items?.filter(filter.matches),
    options: filters.map(({ key, label, matches }) => ({ key, label, count: items?.filter(matches).length ?? 0 })),
    active: filter.key,
    setActive,
  }
}
