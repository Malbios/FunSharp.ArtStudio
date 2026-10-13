import { describe, expect, it } from 'vitest'
import type { PostDetails } from '../api'
import { DEFAULT_TAGS, initialPostFields, stashEditorUrl } from './postDefaults'

const noPost: PostDetails = {
  title: null,
  description: null,
  tags: null,
  stashItemId: null,
  stashedAt: null,
  publishedUrl: null,
  publishedAt: null,
}

describe('initial post fields', () => {
  it('credits the DeviantArt inspiration and uses the default tags', () => {
    const url = 'https://www.deviantart.com/someone/art/Fox-123'
    expect(initialPostFields({ post: noPost, deviantArtUrl: url })).toEqual({
      title: '',
      description: `Inspired by ${url}`,
      tags: DEFAULT_TAGS,
    })
  })

  it('leaves the description empty without a DeviantArt inspiration', () => {
    expect(initialPostFields({ post: noPost, deviantArtUrl: null }).description).toBe('')
  })

  it('keeps what was saved before, even an empty description', () => {
    const post = { ...noPost, title: 'Ember Fox', description: '', tags: 'fox' }
    expect(initialPostFields({ post, deviantArtUrl: 'https://www.deviantart.com/x/art/y-1' })).toEqual({
      title: 'Ember Fox',
      description: '',
      tags: 'fox',
    })
  })
})

it('links a Stash item to its DeviantArt editor', () => {
  expect(stashEditorUrl('9123456789012345678')).toBe(
    'https://www.deviantart.com/_deviation_submit/?deviationid=9123456789012345678',
  )
})
