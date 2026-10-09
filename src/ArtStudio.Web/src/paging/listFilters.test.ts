import { describe, expect, it } from 'vitest'
import type { PromptGenerationState, SetSummary } from '../api'
import { DRAFT_FILTERS, SET_FILTERS } from './listFilters'

const summary = (overrides: Partial<SetSummary> & { state?: PromptGenerationState }): SetSummary =>
  ({
    prompt: '',
    hasActiveJob: false,
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
})

describe('draft filters', () => {
  it('shows drafts with a prompt and nothing pending as ready', () => {
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'Done' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'None' }))).toBe(true)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: '  ', state: 'Done' }))).toBe(false)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'Running' }))).toBe(false)
    expect(matches(DRAFT_FILTERS, 'ready', summary({ prompt: 'A fox.', state: 'Queued' }))).toBe(false)
  })

  it('shows everything under All', () => {
    expect(matches(DRAFT_FILTERS, 'all', summary({ state: 'Running' }))).toBe(true)
  })
})
