import { describe, expect, it } from 'vitest'
import { niceTicks } from './chartScale'

describe('niceTicks', () => {
  it('covers the values with round steps from zero', () => {
    expect(niceTicks(0, 130)).toEqual([0, 50, 100, 150])
    expect(niceTicks(3, 9)).toEqual([0, 5, 10])
  })

  it('reaches below zero for falling values', () => {
    expect(niceTicks(-2, 7)).toEqual([-5, 0, 5, 10])
  })

  it('still draws a scale when every value is zero', () => {
    expect(niceTicks(0, 0)).toEqual([0, 1])
  })
})
