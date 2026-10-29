export interface BulkRow {
  id: number
  url: string
  visionContext: string
}

export interface WaitingUrl {
  url: string
  visionContext?: string
}

let lastRowId = 0

export const emptyRow = (): BulkRow => ({ id: ++lastRowId, url: '', visionContext: '' })

const isEmpty = (row: BulkRow) => row.url.trim() === '' && row.visionContext.trim() === ''

export function withTrailingEmptyRow(rows: BulkRow[]): BulkRow[] {
  return rows.length > 0 && isEmpty(rows[rows.length - 1]) ? rows : [...rows, emptyRow()]
}

/** Puts each pasted link on a row of its own; the row pasted into keeps its context for the first link. */
export function spreadPastedLinks(rows: BulkRow[], index: number, urls: string[]): BulkRow[] {
  const [first, ...rest] = urls
  return [
    ...rows.slice(0, index),
    { ...rows[index], url: first },
    ...rest.map((url) => ({ ...emptyRow(), url })),
    ...rows.slice(index + 1),
  ]
}

export function waitingUrls(rows: BulkRow[]): WaitingUrl[] {
  return rows
    .filter((row) => row.url.trim() !== '')
    .map((row) => ({ url: row.url.trim(), visionContext: row.visionContext.trim() || undefined }))
}
