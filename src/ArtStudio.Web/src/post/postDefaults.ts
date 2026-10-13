import type { SetDetail } from '../api'

export const DEFAULT_TAGS = 'digital_art made_with_ai'
export const MAX_TITLE_LENGTH = 50

export const stashEditorUrl = (stashItemId: string) =>
  `https://www.deviantart.com/_deviation_submit/?deviationid=${stashItemId}`

/** What the post form starts with: what was saved before, else a link to the DeviantArt inspiration. */
export function initialPostFields(set: Pick<SetDetail, 'post' | 'deviantArtUrl'>) {
  return {
    title: set.post.title ?? '',
    description: set.post.description ?? (set.deviantArtUrl ? `Inspired by ${set.deviantArtUrl}` : ''),
    tags: set.post.tags ?? DEFAULT_TAGS,
  }
}
