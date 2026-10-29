export interface BulkRow {
  id: number
  url: string
  askForContext: boolean
}

export interface WaitingUrl {
  url: string
  visionContext?: string
}

export interface RowsToAdd {
  now: WaitingUrl[]
  askFirst: BulkRow[]
}

let lastRowId = 0

export const emptyRow = (): BulkRow => ({ id: ++lastRowId, url: '', askForContext: false })

const hasUrl = (row: BulkRow) => row.url.trim() !== ''

export function withTrailingEmptyRow(rows: BulkRow[]): BulkRow[] {
  return rows.length > 0 && !hasUrl(rows[rows.length - 1]) ? rows : [...rows, emptyRow()]
}

export function rowsToAdd(rows: BulkRow[]): RowsToAdd {
  const filled = rows.filter(hasUrl).map((row) => ({ ...row, url: row.url.trim() }))
  return {
    now: filled.filter((row) => !row.askForContext).map((row) => ({ url: row.url })),
    askFirst: filled.filter((row) => row.askForContext),
  }
}
