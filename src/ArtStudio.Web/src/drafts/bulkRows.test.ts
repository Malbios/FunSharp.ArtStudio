import { describe, expect, it } from 'vitest'
import { emptyRow, spreadPastedLinks, waitingUrls, withTrailingEmptyRow, type BulkRow } from './bulkRows'

const first = 'https://www.deviantart.com/a/art/One-1'
const second = 'https://www.deviantart.com/b/art/Two-2'

const row = (url: string, visionContext = ''): BulkRow => ({ ...emptyRow(), url, visionContext })
const contents = (rows: BulkRow[]) => rows.map(({ url, visionContext }) => ({ url, visionContext }))

describe('withTrailingEmptyRow', () => {
  it('adds an empty row once the last one has a link or context', () => {
    expect(contents(withTrailingEmptyRow([row(first)]))).toEqual([
      { url: first, visionContext: '' },
      { url: '', visionContext: '' },
    ])
    expect(withTrailingEmptyRow([row('', 'a heron')])).toHaveLength(2)
  })

  it('keeps a single trailing empty row', () => {
    const rows = [row(first), row('')]
    expect(withTrailingEmptyRow(rows)).toBe(rows)
    expect(withTrailingEmptyRow([])).toHaveLength(1)
  })
})

describe('spreadPastedLinks', () => {
  it('gives each pasted link its own row, with the context staying on the first', () => {
    const rows = [row('', 'a heron'), row('later')]
    expect(contents(spreadPastedLinks(rows, 0, [first, second]))).toEqual([
      { url: first, visionContext: 'a heron' },
      { url: second, visionContext: '' },
      { url: 'later', visionContext: '' },
    ])
  })
})

describe('waitingUrls', () => {
  it('takes the rows with a link, trimmed, and drops blank context', () => {
    expect(waitingUrls([row(` ${first} `, '  a heron '), row('', 'orphan context'), row(second, '  ')])).toEqual([
      { url: first, visionContext: 'a heron' },
      { url: second, visionContext: undefined },
    ])
  })
})
