import { describe, expect, it } from 'vitest'
import { describePreset, withArtStyle } from './presets'

describe('withArtStyle', () => {
  it('replaces only the art style box', () => {
    expect(withArtStyle(['c', 's', 'co', 'old style'], ' Oil paint. ')).toEqual(['c', 's', 'co', 'Oil paint.'])
    expect(withArtStyle(['s', 'co', ''], 'Oil paint.')).toEqual(['s', 'co', 'Oil paint.'])
  })

  it('keeps the boxes when the preset has no art style', () => {
    const paragraphs = ['c', 's', 'co', 'a']
    expect(withArtStyle(paragraphs, null)).toBe(paragraphs)
    expect(withArtStyle(paragraphs, '  ')).toBe(paragraphs)
  })
})

describe('describePreset', () => {
  it('lists only what the preset changes', () => {
    expect(describePreset({ artStyle: 'Oil paint.', resolution: null, imageCount: 4 })).toBe(
      'Art style: Oil paint.\nImages: 4',
    )
  })
})
