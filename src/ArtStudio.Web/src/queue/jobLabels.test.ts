import { describe, expect, it } from 'vitest'
import type { Job } from '../api'
import { jobTimeLabel } from './jobLabels'

const NOW = Date.parse('2026-10-26T12:00:00Z')

function videoJob(changes: Partial<Job>): Job {
  return {
    id: 1,
    setId: 1,
    kind: 'Video',
    prompt: '',
    resolution: '896 x 512',
    sourceImageUrl: null,
    requestedCount: 1,
    completedCount: 0,
    status: 'Queued',
    error: null,
    createdAt: '',
    startedAt: null,
    finishedAt: null,
    videoSeconds: 5,
    videoTurbo: true,
    hasEndFrame: false,
    estimatedSecondsPerVideo: 17 * 60,
    currentRunStartedAt: null,
    ...changes,
  }
}

describe('jobTimeLabel', () => {
  it('gives the whole time for a waiting job', () => {
    expect(jobTimeLabel(videoJob({}), NOW)).toBe('takes about 17 min')
    expect(jobTimeLabel(videoJob({ requestedCount: 3 }), NOW)).toBe('takes about 51 min for 3 videos')
    expect(jobTimeLabel(videoJob({ requestedCount: 8 }), NOW)).toBe('takes about 2 h 16 min for 8 videos')
  })

  it('gives the time left for a running job', () => {
    const startedFiveMinutesAgo = new Date(NOW - 5 * 60 * 1000).toISOString()

    expect(jobTimeLabel(videoJob({ status: 'Running', currentRunStartedAt: startedFiveMinutesAgo }), NOW)).toBe('about 12 min left')
    expect(
      jobTimeLabel(videoJob({ status: 'Running', requestedCount: 3, completedCount: 1, currentRunStartedAt: startedFiveMinutesAgo }), NOW),
    ).toBe('about 29 min left')
  })

  it('says so when a video takes longer than estimated', () => {
    const startedAnHourAgo = new Date(NOW - 60 * 60 * 1000).toISOString()

    expect(jobTimeLabel(videoJob({ status: 'Running', currentRunStartedAt: startedAnHourAgo }), NOW)).toBe('taking longer than estimated')
  })

  it('has nothing to say for image jobs or finished and failed ones', () => {
    expect(jobTimeLabel(videoJob({ kind: 'Image', estimatedSecondsPerVideo: null }), NOW)).toBeNull()
    expect(jobTimeLabel(videoJob({ status: 'Failed' }), NOW)).toBeNull()
    expect(jobTimeLabel(videoJob({ completedCount: 1 }), NOW)).toBeNull()
  })
})
