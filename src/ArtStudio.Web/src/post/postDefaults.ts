import type { SetDetail } from '../api'

export const DEFAULT_TAGS = 'digital_art made_with_ai'
export const MAX_TITLE_LENGTH = 50

export const stashEditorUrl = (stashItemId: string) =>
  `https://www.deviantart.com/_deviation_submit/?deviationid=${stashItemId}`

/** Pasted into DeviantArt's editor, the link becomes an embedded thumbnail. Through the API it stays a plain link. */
export const inspirationCredit = (deviantArtUrl: string) => `Inspired by ${deviantArtUrl}`

export function initialPostFields(set: Pick<SetDetail, 'post'>) {
  return {
    title: set.post.title ?? '',
    description: set.post.description ?? '',
    tags: set.post.tags ?? DEFAULT_TAGS,
  }
}
