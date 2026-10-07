export const PAGE_SIZE = 12

export type PageLink = number | '…'

const NEIGHBOURS = 1

/** Page numbers to offer: always the first and last page and the pages around the current one, with gaps elided. */
export function pageNumbers(current: number, pageCount: number): PageLink[] {
  const shown = new Set<number>([1, pageCount])
  for (let page = current - NEIGHBOURS; page <= current + NEIGHBOURS; page++) {
    if (page >= 1 && page <= pageCount) shown.add(page)
  }

  const links: PageLink[] = []
  let previous = 0
  for (const page of [...shown].sort((a, b) => a - b)) {
    if (page - previous === 2) links.push(previous + 1)
    else if (page - previous > 2) links.push('…')
    links.push(page)
    previous = page
  }
  return links
}
