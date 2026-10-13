import { describe, expect, it } from 'vitest'
import { pageNumbers } from './pagination'

describe('pageNumbers', () => {
  it('lists every page when there are only a few', () => {
    expect(pageNumbers(1, 2)).toEqual([1, 2])
    expect(pageNumbers(3, 5)).toEqual([1, 2, 3, 4, 5])
    expect(pageNumbers(7, 7)).toEqual([1, 2, 3, 4, 5, 6, 7])
  })

  it('elides only the far side when the current page is near an edge', () => {
    expect(pageNumbers(1, 10)).toEqual([1, 2, 3, 4, 5, '…', 10])
    expect(pageNumbers(4, 10)).toEqual([1, 2, 3, 4, 5, '…', 10])
    expect(pageNumbers(7, 10)).toEqual([1, '…', 6, 7, 8, 9, 10])
    expect(pageNumbers(10, 10)).toEqual([1, '…', 6, 7, 8, 9, 10])
  })

  it('elides both sides when the current page is in the middle', () => {
    expect(pageNumbers(5, 10)).toEqual([1, '…', 4, 5, 6, '…', 10])
    expect(pageNumbers(6, 10)).toEqual([1, '…', 5, 6, 7, '…', 10])
  })

  it('always offers the same number of links beyond a handful of pages', () => {
    for (let current = 1; current <= 12; current++) expect(pageNumbers(current, 12)).toHaveLength(7)
  })
})
