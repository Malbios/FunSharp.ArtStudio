import { describe, expect, it } from 'vitest'
import type { Job, JobKind, JobStatus, QueueState } from '../api'
import { pausedLabel, queueSections } from './queueSections'

let nextId = 1
function job(kind: JobKind, status: JobStatus = 'Queued'): Job {
  return {
    id: nextId++,
    setId: 1,
    kind,
    prompt: '',
    resolution: '',
    sourceImageUrl: null,
    requestedCount: 1,
    completedCount: 0,
    status,
    error: null,
    createdAt: '',
    startedAt: null,
    finishedAt: null,
    videoSeconds: null,
    videoTurbo: true,
    hasEndFrame: false,
  }
}

const state = (active: Job[], changes: Partial<QueueState> = {}): QueueState => ({
  imagesPaused: false,
  videosPaused: false,
  loadedKind: null,
  active,
  ...changes,
})

describe('queueSections', () => {
  it('puts the running kind first and explains why the other kind waits', () => {
    const [first, second] = queueSections(state([job('Video', 'Running'), job('Video'), job('Image')]))

    expect([first.kind, second.kind]).toEqual(['Video', 'Image'])
    expect(first.jobs).toHaveLength(2)
    expect(first.waitingNote).toBeNull()
    expect(second.waitingNote).toBe("Waiting until the 2 videos ahead are done, so ComfyUI doesn't switch models.")
  })

  it('stays with the loaded model while it has waiting jobs', () => {
    const sections = queueSections(state([job('Image'), job('Video')], { loadedKind: 'Video' }))

    expect(sections.map((s) => s.kind)).toEqual(['Video', 'Image'])
    expect(sections[1].waitingNote).toBe("Waiting until the 1 video ahead is done, so ComfyUI doesn't switch models.")
  })

  it('does not count paused jobs as ahead', () => {
    const sections = queueSections(state([job('Video'), job('Image')], { loadedKind: 'Video', videosPaused: true }))

    expect(sections.map((s) => s.kind)).toEqual(['Image', 'Video'])
    expect(sections.every((s) => s.waitingNote === null)).toBe(true)
    expect(sections[1].paused).toBe(true)
  })
})

describe('pausedLabel', () => {
  it('names the paused kinds', () => {
    expect(pausedLabel(state([]))).toBeNull()
    expect(pausedLabel(state([], { imagesPaused: true }))).toBe('images paused')
    expect(pausedLabel(state([], { videosPaused: true }))).toBe('videos paused')
    expect(pausedLabel(state([], { imagesPaused: true, videosPaused: true }))).toBe('paused')
  })
})
