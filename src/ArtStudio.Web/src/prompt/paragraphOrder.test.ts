import { describe, expect, it } from 'vitest'
import { canReorder, isNormalOrder, moveSection, NORMAL_ORDER, orderLabel, parseOrder, serializeOrder } from './paragraphOrder'

describe('paragraph order', () => {
  it('reads the stored order and falls back to the normal order', () => {
    expect(parseOrder('Style,Characters,Setting,Composition')).toEqual(['Style', 'Characters', 'Setting', 'Composition'])
    expect(parseOrder(null)).toEqual(NORMAL_ORDER)
    expect(parseOrder('Style,Setting')).toEqual(NORMAL_ORDER)
    expect(serializeOrder(NORMAL_ORDER)).toBe('Characters,Setting,Composition,Style')
  })

  it('moves a section by one step and stays within the row', () => {
    expect(moveSection(NORMAL_ORDER, 3, -1)).toEqual(['Characters', 'Setting', 'Style', 'Composition'])
    expect(moveSection(NORMAL_ORDER, 0, -1)).toEqual(NORMAL_ORDER)
    expect(moveSection(NORMAL_ORDER, 3, 1)).toEqual(NORMAL_ORDER)
    expect(isNormalOrder(moveSection(NORMAL_ORDER, 0, 1))).toBe(false)
  })

  it('labels only orders that differ from the normal one', () => {
    expect(orderLabel(null)).toBeNull()
    expect(orderLabel('Characters,Setting,Composition,Style')).toBeNull()
    expect(orderLabel('Style,Characters,Setting,Composition')).toBe('Art style → Characters → Setting → Composition')
  })

  it('reorders only prompts with at least the three fixed sections', () => {
    expect(canReorder('A forest.\n\nEye level.\n\nWatercolour.')).toBe(true)
    expect(canReorder('A fox in a forest, watercolour.')).toBe(false)
    expect(canReorder('A fox.\n\nWatercolour.')).toBe(false)
  })
})
