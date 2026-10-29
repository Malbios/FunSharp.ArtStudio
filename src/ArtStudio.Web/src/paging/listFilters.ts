import type { SetSummary } from '../api'
import { isPromptBeingModified, isPromptPending } from '../prompt/generateAction'
import type { ListFilter } from './useListFilter'

/** Drafts cannot have videos, so every list of sets but Drafts offers this one. */
const HAS_VIDEOS: ListFilter<SetSummary> = { key: 'videos', label: 'Has videos', matches: (set) => set.videoCount > 0 }
const NEW_VIDEOS: ListFilter<SetSummary> = {
  key: 'new-videos',
  label: 'Unviewed videos',
  matches: (set) => set.unviewedVideoCount > 0,
}

export const SET_FILTERS: ListFilter<SetSummary>[] = [
  { key: 'idle', label: 'Not generating', matches: (set) => !set.hasActiveJob },
  { key: 'unpicked', label: 'No picks', matches: (set) => set.pickedCount === 0, excludes: ['picked'] },
  { key: 'picked', label: 'Has picks', matches: (set) => set.pickedCount > 0, excludes: ['unpicked'] },
  {
    key: 'modifying',
    label: 'Prompt being modified',
    matches: (set) => isPromptBeingModified(set.promptGeneration),
    excludes: ['unmodifying'],
  },
  {
    key: 'unmodifying',
    label: 'Prompt not being modified',
    matches: (set) => !isPromptBeingModified(set.promptGeneration),
    excludes: ['modifying'],
  },
  HAS_VIDEOS,
  NEW_VIDEOS,
]

export const POST_FILTERS: ListFilter<SetSummary>[] = [
  { key: 'described', label: 'Title and tags', matches: (set) => set.hasTitleAndTags, excludes: ['undescribed'] },
  { key: 'undescribed', label: 'Missing title or tags', matches: (set) => !set.hasTitleAndTags, excludes: ['described'] },
  HAS_VIDEOS,
  NEW_VIDEOS,
]

export const STASHED_FILTERS: ListFilter<SetSummary>[] = [HAS_VIDEOS, NEW_VIDEOS]

export const PUBLISHED_FILTERS: ListFilter<SetSummary>[] = [HAS_VIDEOS, NEW_VIDEOS]

export const ARCHIVED_FILTERS: ListFilter<SetSummary>[] = [HAS_VIDEOS, NEW_VIDEOS]

export const isDraftReady = (draft: SetSummary) => draft.prompt.trim() !== '' && !isPromptPending(draft.promptGeneration)

export const DRAFT_FILTERS: ListFilter<SetSummary>[] = [
  {
    key: 'pending',
    label: 'Generating or queued',
    matches: (draft) => isPromptPending(draft.promptGeneration),
    excludes: ['settled'],
  },
  {
    key: 'settled',
    label: 'Ready or failed',
    matches: (draft) => isDraftReady(draft) || draft.promptGeneration.state === 'Failed',
    excludes: ['pending'],
  },
]
