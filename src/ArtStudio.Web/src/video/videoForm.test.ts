import { describe, expect, it } from 'vitest'
import type { VideoOptions } from '../api'
import { CUSTOM, initialVideoForm, renderedSeconds, resolveVideoForm, videoFrameCount } from './videoForm'

const options: VideoOptions = {
  promptTemplate: 'Start.\n\n???\n\nEnd.',
  durationPresets: [3, 5, 6, 8, 10, 15, 20, 30],
  resolutionPresets: [
    { name: '896 x 512', megapixels: 0.46, note: null },
    { name: '1344 x 768', megapixels: 0.98, note: 'official 768p, the largest' },
  ],
  defaultSeconds: 5,
  defaultResolution: '896 x 512',
  maxSeconds: 30,
  maxMegapixels: 0.98,
  maxCount: 20,
}

describe('videoFrameCount', () => {
  it('snaps up to the 17k+5 frame grid at 24 fps', () => {
    expect([3, 4, 5, 6, 8, 10, 15, 20, 30].map(videoFrameCount)).toEqual([73, 107, 124, 158, 192, 243, 362, 481, 736])
  })

  it('never goes below five frames', () => {
    expect(videoFrameCount(0.01)).toBe(5)
  })

  it('gives the length that is actually rendered', () => {
    expect(renderedSeconds(5)).toBeCloseTo(5.1667, 3)
    expect(renderedSeconds(8)).toBe(8)
  })
})

describe('resolveVideoForm', () => {
  it('starts from the template and the defaults', () => {
    expect(resolveVideoForm(initialVideoForm(options), options)).toEqual({
      settings: { prompt: options.promptTemplate, seconds: 5, megapixels: 0.46, turbo: true, count: 1, queueFirst: false },
    })
  })

  it('takes typed custom values, with a comma as decimal separator too', () => {
    const form = { ...initialVideoForm(options), duration: CUSTOM, customSeconds: '7.5', resolution: CUSTOM, customMegapixels: '0,55' }

    expect(resolveVideoForm(form, options)).toMatchObject({ settings: { seconds: 7.5, megapixels: 0.55 } })
  })

  it('rejects empty or out-of-range values', () => {
    const base = initialVideoForm(options)

    expect(resolveVideoForm({ ...base, prompt: '  ' }, options).error).toMatch(/prompt/)
    expect(resolveVideoForm({ ...base, duration: CUSTOM, customSeconds: '' }, options).error).toMatch(/length/)
    expect(resolveVideoForm({ ...base, duration: CUSTOM, customSeconds: '31' }, options).error).toMatch(/length/)
    expect(resolveVideoForm({ ...base, resolution: CUSTOM, customMegapixels: '1.2' }, options).error).toMatch(/size/)
    expect(resolveVideoForm({ ...base, count: 21 }, options).error).toMatch(/number of videos/)
  })
})
