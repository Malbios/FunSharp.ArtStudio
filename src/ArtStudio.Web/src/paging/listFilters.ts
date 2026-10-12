import type { SetSummary } from '../api'
import { isPromptPending } from '../prompt/generateAction'
import type { ListFilter } from './useListFilter'

export const SET_FILTERS: ListFilter<SetSummary>[] = [
  { key: 'idle', label: 'Not generating', matches: (set) => !set.hasActiveJob },
  { key: 'unpicked', label: 'No picks', matches: (set) => set.pickedCount === 0 },
]

export const isDraftReady = (draft: SetSummary) => draft.prompt.trim() !== '' && !isPromptPending(draft.promptGeneration)

export const DRAFT_FILTERS: ListFilter<SetSummary>[] = [{ key: 'ready', label: 'Prompt ready', matches: isDraftReady }]
