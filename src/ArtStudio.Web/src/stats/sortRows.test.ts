import { describe, expect, it } from 'vitest'
import { nextSortOrder, sortRows } from './sortRows'

interface Row {
  name: string
  views: number | null
}

const rows: Row[] = [
  { name: 'owl', views: 30 },
  { name: 'cat', views: null },
  { name: 'fox', views: 220 },
]

describe('sortRows', () => {
  it('sorts numbers both ways and keeps rows without a value last', () => {
    expect(sortRows(rows, { key: 'views', direction: 'desc' }).map((row) => row.name)).toEqual(['fox', 'owl', 'cat'])
    expect(sortRows(rows, { key: 'views', direction: 'asc' }).map((row) => row.name)).toEqual(['owl', 'fox', 'cat'])
  })

  it('sorts text alphabetically', () => {
    expect(sortRows(rows, { key: 'name', direction: 'asc' }).map((row) => row.name)).toEqual(['cat', 'fox', 'owl'])
  })

  it('flips the sorted column and starts another column with its largest values', () => {
    expect(nextSortOrder<Row>({ key: 'views', direction: 'desc' }, 'views')).toEqual({ key: 'views', direction: 'asc' })
    expect(nextSortOrder<Row>({ key: 'views', direction: 'asc' }, 'name')).toEqual({ key: 'name', direction: 'desc' })
  })
})
