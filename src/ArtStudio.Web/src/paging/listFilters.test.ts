import { describe, expect, it } from 'vitest'
import type { PromptGenerationKind, PromptGenerationState, SetSummary } from '../api'
import {
  ARCHIVED_FILTERS,
  DRAFT_FILTERS,
  POST_FILTERS,
  PUBLISHED_FILTERS,
  SET_FILTERS,
  STASHED_FILTERS,
} from './listFilters'

const summary = (
  overrides: Partial<SetSummary> & { state?: PromptGenerationState; kind?: PromptGenerationKind },
): SetSummary =>
  ({
    prompt: '',
    hasActiveJob: false,
    pickedCount: 0,
    ...overrides,
    promptGeneration: {
      state: overrides.state ?? 'None',
      kind: overrides.kind ?? 'Generate',
      error: null,
      truncated: false,
      text: null,
    },
  }) as SetSummary

const matches = (filters: typeof SET_FILTERS, key: string, item: SetSummary) =>
  filters.find((filter) => filter.key === key)!.matches(item)

describe('set filters', () => {
  it('shows only sets with nothing queued or rendering when not generating', () => {
    expect(matches(SET_FILTERS, 'idle', summary({ hasActiveJob: false }))).toBe(true)
    expect(matches(SET_FILTERS, 'idle', summary({ hasActiveJob: true }))).toBe(false)
  })

  it('shows only sets without picked images when without picks', () => {
    expect(matches(SET_FILTERS, 'unpicked', summary({ pickedCount: 0 }))).toBe(true)
    expect(matches(SET_FILTERS, 'unpicked', summary({ pickedCount: 2 }))).toBe(false)
  })

  it('shows only sets with exactly one picked image when with one pick', () => {
    expect(matches(SET_FILTERS, 'one-pick', summary({ pickedCount: 1 }))).toBe(true)
    expect(matches(SET_FILTERS, 'one-pick', summary({ pickedCount: 0 }))).toBe(false)
    expect(matches(SET_FILTERS, 'one-pick', summary({ pickedCount: 2 }))).toBe(false)
  })

  it('shows only sets with two or more picked images when with more than one pick', () => {
    expect(matches(SET_FILTERS, 'several-picks', summary({ pickedCount: 2 }))).toBe(true)
    expect(matches(SET_FILTERS, 'several-picks', summary({ pickedCount: 1 }))).toBe(false)
    expect(matches(SET_FILTERS, 'several-picks', summary({ pickedCount: 0 }))).toBe(false)
  })

  it('splits sets by whether their prompt is waiting for or being modified', () => {
    for (const state of ['Queued', 'Running'] as const) {
      expect(matches(SET_FILTERS, 'modifying', summary({ kind: 'Modify', state }))).toBe(true)
      expect(matches(SET_FILTERS, 'unmodifying', summary({ kind: 'Modify', state }))).toBe(false)
    }
    const notModifying = [
      { kind: 'Modify', state: 'Done' },
      { kind: 'Modify', state: 'Failed' },
      { kind: 'Generate', state: 'Running' },
    ] as const
    for (const generation of notModifying) {
      expect(matches(SET_FILTERS, 'modifying', summary(generation))).toBe(false)
      expect(matches(SET_FILTERS, 'unmodifying', summary(generation))).toBe(true)
    }
  })
})

describe('post filters', () => {
  it('splits ready sets by whether they have a title and tags', () => {
    expect(matches(POST_FILTERS, 'described', summary({ hasTitleAndTags: true }))).toBe(true)
    expect(matches(POST_FILTERS, 'described', summary({ hasTitleAndTags: false }))).toBe(false)
    expect(matches(POST_FILTERS, 'undescribed', summary({ hasTitleAndTags: false }))).toBe(true)
    expect(matches(POST_FILTERS, 'undescribed', summary({ hasTitleAndTags: true }))).toBe(false)
  })
})

describe('has videos filter', () => {
  const listsWithVideos = [SET_FILTERS, POST_FILTERS, STASHED_FILTERS, PUBLISHED_FILTERS, ARCHIVED_FILTERS]

  it('is offered on every list of sets except drafts, which cannot have videos', () => {
    for (const filters of listsWithVideos) expect(filters.map((filter) => filter.key)).toContain('videos')
    expect(DRAFT_FILTERS.map((filter) => filter.key)).not.toContain('videos')
  })

  it('shows only sets with at least one video and combines with the other filters', () => {
    for (const filters of listsWithVideos) {
      expect(matches(filters, 'videos', summary({ videoCount: 2 }))).toBe(true)
      expect(matches(filters, 'videos', summary({ videoCount: 0 }))).toBe(false)
      expect(filters.find((filter) => filter.key === 'videos')!.excludes).toBeUndefined()
    }
  })

  it('comes with an unviewed videos filter that shows only sets with videos not played yet', () => {
    for (const filters of listsWithVideos) {
      expect(matches(filters, 'new-videos', summary({ videoCount: 2, unviewedVideoCount: 1 }))).toBe(true)
      expect(matches(filters, 'new-videos', summary({ videoCount: 2, unviewedVideoCount: 0 }))).toBe(false)
    }
    expect(DRAFT_FILTERS.map((filter) => filter.key)).not.toContain('new-videos')
  })
})

describe('draft filters', () => {
  it('shows drafts whose prompt is being generated or waits for it as generating or queued', () => {
    expect(matches(DRAFT_FILTERS, 'pending', summary({ state: 'Running' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'pending', summary({ prompt: 'A fox.', state: 'Queued' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'pending', summary({ prompt: 'A fox.', state: 'Done' }))).toBe(false)
    expect(matches(DRAFT_FILTERS, 'pending', summary({ state: 'Failed' }))).toBe(false)
  })

  it('shows drafts with a finished prompt, or a failed one, as ready or failed', () => {
    expect(matches(DRAFT_FILTERS, 'settled', summary({ prompt: 'A fox.', state: 'Done' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'settled', summary({ prompt: 'A fox.', state: 'None' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'settled', summary({ prompt: '', state: 'Failed' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'settled', summary({ prompt: 'A fox.', state: 'Running' }))).toBe(false)
    expect(matches(DRAFT_FILTERS, 'settled', summary({ prompt: '  ', state: 'None' }))).toBe(false)
  })

  it('never combines the two', () => {
    expect(DRAFT_FILTERS.find((filter) => filter.key === 'pending')!.excludes).toEqual(['settled'])
    expect(DRAFT_FILTERS.find((filter) => filter.key === 'settled')!.excludes).toEqual(['pending'])
  })
})
