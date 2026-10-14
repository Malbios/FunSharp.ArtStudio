import { describe, expect, it } from 'vitest'
import type { PromptGenerationState, SetSummary } from '../api'
import { DRAFT_FILTERS, POST_FILTERS, SET_FILTERS } from './listFilters'

const summary = (overrides: Partial<SetSummary> & { state?: PromptGenerationState }): SetSummary =>
  ({
    prompt: '',
    hasActiveJob: false,
    pickedCount: 0,
    ...overrides,
    promptGeneration: { state: overrides.state ?? 'None', kind: 'Generate', error: null, truncated: false, text: null },
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

  it('shows only sets with picked images when with picks', () => {
    expect(matches(SET_FILTERS, 'picked', summary({ pickedCount: 1 }))).toBe(true)
    expect(matches(SET_FILTERS, 'picked', summary({ pickedCount: 0 }))).toBe(false)
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

describe('draft filters', () => {
  it('shows drafts with a prompt and nothing pending as ready', () => {
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'Done' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'None' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: '  ', state: 'Done' }))).toBe(false)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'Running' }))).toBe(false)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'Queued' }))).toBe(false)
  })
})
