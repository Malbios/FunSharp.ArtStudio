export const PAGE_SIZE = 12

export type PageLink = number | '…'

/** Room for the first and last page, the current page with one neighbour on each side, and a gap on each side. */
export const PAGE_LINK_SLOTS = 7
const EDGE_RUN = PAGE_LINK_SLOTS - 2

const range = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, index) => from + index)

/**
 * Page numbers to offer: always the first and last page and the pages around the current one, with gaps elided.
 * Beyond a handful of pages the list always has PAGE_LINK_SLOTS entries, so the links keep their places while paging.
 */
export function pageNumbers(current: number, pageCount: number): PageLink[] {
  if (pageCount <= PAGE_LINK_SLOTS) return range(1, pageCount)
  if (current < EDGE_RUN) return [...range(1, EDGE_RUN), '…', pageCount]
  if (current > pageCount - EDGE_RUN + 1) return [1, '…', ...range(pageCount - EDGE_RUN + 1, pageCount)]
  return [1, '…', current - 1, current, current + 1, '…', pageCount]
}
