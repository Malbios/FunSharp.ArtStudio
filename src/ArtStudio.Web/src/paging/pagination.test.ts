import { describe, expect, it } from 'vitest'
import { pageNumbers } from './pagination'

describe('pageNumbers', () => {
  it('lists every page when there are only a few', () => {
    expect(pageNumbers(1, 2)).toEqual([1, 2])
    expect(pageNumbers(3, 5)).toEqual([1, 2, 3, 4, 5])
  })

  it('elides the far side when the current page is near an edge', () => {
    expect(pageNumbers(1, 10)).toEqual([1, 2, '…', 10])
    expect(pageNumbers(10, 10)).toEqual([1, '…', 9, 10])
  })

  it('elides both sides when the current page is in the middle', () => {
    expect(pageNumbers(5, 10)).toEqual([1, '…', 4, 5, 6, '…', 10])
  })

  it('shows a single hidden page instead of a gap', () => {
    expect(pageNumbers(4, 10)).toEqual([1, 2, 3, 4, 5, '…', 10])
  })
})
