import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PAGE_PARAM } from './usePagedItems'

const FILTER_PARAM = 'filter'
const KEY_SEPARATOR = ','

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
  totalCount: number
  options: FilterOption[]
  /** The keys of the filters that are on; none means everything is shown. */
  active: string[]
  toggle: (key: string) => void
  clear: () => void
}

const storageKey = (list: string) => `artstudio.filter.${list}`

function rememberedFilter(list: string): string | null {
  try {
    return localStorage.getItem(storageKey(list))
  } catch {
    return null
  }
}

function rememberFilter(list: string, keys: string[]) {
  try {
    localStorage.setItem(storageKey(list), keys.join(KEY_SEPARATOR))
  } catch {
    // Without storage the filter is simply not remembered.
  }
}

/**
 * Narrows items to those matching every filter named in the URL (?filter=a,b), or else the ones last chosen for this
 * list in this browser. No filter shows everything; changing the filters goes back to page 1.
 */
export function useListFilter<T>(list: string, items: T[] | undefined, filters: ListFilter<T>[]): FilteredItems<T> {
  const [searchParams, setSearchParams] = useSearchParams()
  const requested = (searchParams.get(FILTER_PARAM) ?? rememberedFilter(list) ?? '').split(KEY_SEPARATOR)
  const activeFilters = filters.filter((filter) => requested.includes(filter.key))
  const active = activeFilters.map((filter) => filter.key)

  const setActive = useCallback(
    (keys: string[]) => {
      rememberFilter(list, keys)
      setSearchParams((params) => {
        if (keys.length === 0) params.delete(FILTER_PARAM)
        else params.set(FILTER_PARAM, keys.join(KEY_SEPARATOR))
        params.delete(PAGE_PARAM)
        return params
      })
    },
    [list, setSearchParams],
  )

  const toggle = (key: string) =>
    setActive(filters.map((filter) => filter.key).filter((other) => (other === key) !== active.includes(other)))

  return {
    items: items?.filter((item) => activeFilters.every((filter) => filter.matches(item))),
    totalCount: items?.length ?? 0,
    options: filters.map(({ key, label, matches }) => ({ key, label, count: items?.filter(matches).length ?? 0 })),
    active,
    toggle,
    clear: () => setActive([]),
  }
}
