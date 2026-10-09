import type { SetSummary } from '../api'
import { isPromptPending } from '../prompt/generateAction'
import type { ListFilter } from './useListFilter'

const ALL: ListFilter<SetSummary> = { key: 'all', label: 'All', matches: () => true }

export const SET_FILTERS: ListFilter<SetSummary>[] = [
  ALL,
  { key: 'idle', label: 'Not generating', matches: (set) => !set.hasActiveJob },
]

export const DRAFT_FILTERS: ListFilter<SetSummary>[] = [
  ALL,
  {
    key: 'ready',
    label: 'Prompt ready',
    matches: (draft) => draft.prompt.trim() !== '' && !isPromptPending(draft.promptGeneration),
  },
]
