import type { SetSummary } from '../api'
import { isPromptPending } from '../prompt/generateAction'
import type { ListFilter } from './useListFilter'

export const SET_FILTERS: ListFilter<SetSummary>[] = [
  { key: 'idle', label: 'Not generating', matches: (set) => !set.hasActiveJob },
  { key: 'unpicked', label: 'No picks', matches: (set) => set.pickedCount === 0, excludes: ['picked'] },
  { key: 'picked', label: 'Has picks', matches: (set) => set.pickedCount > 0, excludes: ['unpicked'] },
]

export const POST_FILTERS: ListFilter<SetSummary>[] = [
  { key: 'described', label: 'Title and tags', matches: (set) => set.hasTitleAndTags, excludes: ['undescribed'] },
  { key: 'undescribed', label: 'Missing title or tags', matches: (set) => !set.hasTitleAndTags, excludes: ['described'] },
]

export const isDraftReady = (draft: SetSummary) => draft.prompt.trim() !== '' && !isPromptPending(draft.promptGeneration)

export const DRAFT_FILTERS: ListFilter<SetSummary>[] = [{ key: 'ready', label: 'Prompt ready', matches: isDraftReady }]
