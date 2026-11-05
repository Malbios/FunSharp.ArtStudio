import { describe, expect, it } from 'vitest'
import { promptTimeLabel } from './promptTimeLabel'

const NOW = Date.parse('2026-11-05T12:00:00Z')
const minutesBefore = (minutes: number) => new Date(NOW - minutes * 60_000).toISOString()

describe('promptTimeLabel', () => {
  it('counts up while running', () => {
    expect(promptTimeLabel({ state: 'Running', startedAt: minutesBefore(12), finishedAt: null }, NOW)).toBe('running for 12 min')
  })

  it('tells how long a finished or failed run took', () => {
    const run = { startedAt: minutesBefore(30), finishedAt: minutesBefore(12) }
    expect(promptTimeLabel({ state: 'Done', ...run }, NOW)).toBe('took 18 min')
    expect(promptTimeLabel({ state: 'Failed', ...run }, NOW)).toBe('took 18 min')
  })

  it('is nothing without a run, or while queued again', () => {
    expect(promptTimeLabel({ state: 'None', startedAt: null, finishedAt: null }, NOW)).toBeNull()
    expect(promptTimeLabel({ state: 'Done', startedAt: null, finishedAt: null }, NOW)).toBeNull()
    expect(promptTimeLabel({ state: 'Queued', startedAt: minutesBefore(30), finishedAt: minutesBefore(12) }, NOW)).toBeNull()
  })
})
