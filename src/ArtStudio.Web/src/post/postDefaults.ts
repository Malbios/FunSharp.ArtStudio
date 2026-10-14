import type { SetDetail } from '../api'

export const DEFAULT_TAGS = 'digital_art made_with_ai'
export const MAX_TITLE_LENGTH = 50
const INSPIRED_BY = 'Inspired by'

export const stashEditorUrl = (stashItemId: string) =>
  `https://www.deviantart.com/_deviation_submit/?deviationid=${stashItemId}`

/**
 * Ends with "Inspired by" on its own line, for the inspiration's link to be pasted after it in DeviantArt's
 * editor: pasted there, the link becomes an embedded thumbnail, while through the API it stays a plain link.
 */
export function stashDescription(description: string, deviantArtUrl: string | null) {
  const typed = description.trim()
  if (!deviantArtUrl) return typed
  if (typed.endsWith(INSPIRED_BY)) return `${typed}\n`
  return `${typed ? `${typed}\n\n` : ''}${INSPIRED_BY}\n`
}

export function hasOnlyDefaultTags(tags: string) {
  const defaults = DEFAULT_TAGS.split(' ')
  return tags
    .split(/\s+/)
    .filter(Boolean)
    .every((tag) => defaults.includes(tag.replace(/^#/, '').toLowerCase()))
}

/** The tags already in the field, then the suggestions it doesn't have yet. */
export function withSuggestedTags(tags: string, suggested: string[]) {
  const current = tags.split(/\s+/).filter(Boolean)
  const known = new Set(current.map((tag) => tag.replace(/^#/, '').toLowerCase()))
  return [...current, ...suggested.filter((tag) => !known.has(tag.toLowerCase()))].join(' ')
}

export function initialPostFields(set: Pick<SetDetail, 'post'>) {
  return {
    title: set.post.title ?? '',
    description: set.post.description ?? '',
    tags: set.post.tags ?? DEFAULT_TAGS,
  }
}
