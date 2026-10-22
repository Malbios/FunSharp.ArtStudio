export type SortDirection = 'asc' | 'desc'

export interface SortOrder<T> {
  key: keyof T
  direction: SortDirection
}

/** Sorts by one column; rows without a value stay at the end in either direction. */
export function sortRows<T>(rows: T[], { key, direction }: SortOrder<T>): T[] {
  const sign = direction === 'asc' ? 1 : -1
  return [...rows].sort((a, b) => {
    const left = a[key]
    const right = b[key]
    if (left === null || left === undefined) return right === null || right === undefined ? 0 : 1
    if (right === null || right === undefined) return -1
    if (typeof left === 'string' && typeof right === 'string') return sign * left.localeCompare(right)
    return sign * (left < right ? -1 : left > right ? 1 : 0)
  })
}

/** Clicking the sorted column flips it; another column starts with its largest values. */
export function nextSortOrder<T>(current: SortOrder<T>, key: keyof T): SortOrder<T> {
  if (current.key === key) return { key, direction: current.direction === 'asc' ? 'desc' : 'asc' }
  return { key, direction: 'desc' }
}
