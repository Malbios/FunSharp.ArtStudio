import { describe, expect, it } from 'vitest'
import { closestResolution } from './closestResolution'

const presets = [
  { name: 'Native', width: 1024, height: 1024, comfyLabel: '' },
  { name: 'Wide', width: 1344, height: 768, comfyLabel: '' },
  { name: 'Tall', width: 768, height: 1344, comfyLabel: '' },
  { name: 'Ultrawide', width: 1280, height: 768, comfyLabel: '' },
]

describe('closestResolution', () => {
  it('picks the preset with the nearest aspect ratio', () => {
    expect(closestResolution(presets, 1920, 1080).name).toBe('Wide')
    expect(closestResolution(presets, 1080, 1920).name).toBe('Tall')
    expect(closestResolution(presets, 900, 1000).name).toBe('Native')
    expect(closestResolution(presets, 1600, 960).name).toBe('Ultrawide')
  })
})
