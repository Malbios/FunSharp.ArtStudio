import { describe, expect, it } from 'vitest'
import type { BuildingBlock } from '../api'
import { describePreset, presetRequeue, resolveBundles, withArtStyle } from './presets'

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

describe('presetRequeue', () => {
  it('replaces the art style and uses the preset resolution and count', () => {
    expect(
      presetRequeue({ artStyle: 'Oil.', resolution: 'Wide', imageCount: 4 }, 'A fox.\n\nA forest.\n\nEye level.\n\nWatercolour.', 'Native', 2),
    ).toEqual({ prompt: 'A fox.\n\nA forest.\n\nEye level.\n\nOil.', resolution: 'Wide', count: 4 })
  })

  it('keeps what the preset does not set', () => {
    expect(presetRequeue({ artStyle: null, resolution: null, imageCount: null }, 'A fox.\n\nWatercolour.', 'Native', 3)).toEqual({
      prompt: 'A fox.\n\nWatercolour.',
      resolution: 'Native',
      count: 3,
    })
  })
})

describe('resolveBundles', () => {
  const block = (id: number, kind: BuildingBlock['kind'], presetIds: number[] = []) =>
    ({ id, kind, label: `#${id}`, presetIds }) as BuildingBlock

  it('lists the presets of each bundle in bundle order, skipping missing ones', () => {
    const resolved = resolveBundles([block(1, 'Preset'), block(2, 'Preset'), block(3, 'Text'), block(9, 'Bundle', [2, 7, 1, 3])])
    expect(resolved.map((r) => [r.bundle.id, r.presets.map((p) => p.id)])).toEqual([[9, [2, 1]]])
  })

  it('drops bundles without any remaining preset', () => {
    expect(resolveBundles([block(9, 'Bundle', [7])])).toEqual([])
  })
})
