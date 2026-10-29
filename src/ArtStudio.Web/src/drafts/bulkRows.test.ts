import { describe, expect, it } from 'vitest'
import { emptyRow, rowsToAdd, withTrailingEmptyRow, type BulkRow } from './bulkRows'

const first = 'https://www.deviantart.com/a/art/One-1'
const second = 'https://www.deviantart.com/b/art/Two-2'

const row = (url: string, askForContext = false): BulkRow => ({ ...emptyRow(), url, askForContext })

describe('withTrailingEmptyRow', () => {
  it('adds an empty row once the last one has a link', () => {
    const rows = withTrailingEmptyRow([row(first)])
    expect(rows.map((r) => r.url)).toEqual([first, ''])
  })

  it('keeps a single trailing row without a link, even when it is checked', () => {
    const rows = [row(first), row('', true)]
    expect(withTrailingEmptyRow(rows)).toBe(rows)
    expect(withTrailingEmptyRow([])).toHaveLength(1)
  })
})

describe('rowsToAdd', () => {
  it('adds unchecked links now and asks for context on checked ones first', () => {
    const checked = row(` ${second} `, true)
    const { now, askFirst } = rowsToAdd([row(` ${first} `), checked, row('', true)])

    expect(now).toEqual([{ url: first }])
    expect(askFirst).toEqual([{ ...checked, url: second }])
  })
})
