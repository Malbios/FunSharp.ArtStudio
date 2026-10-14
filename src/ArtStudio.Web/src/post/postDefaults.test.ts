import { describe, expect, it } from 'vitest'
import type { PostDetails } from '../api'
import { DEFAULT_TAGS, initialPostFields, inspirationCredit, stashEditorUrl } from './postDefaults'

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
  it('starts with an empty title and description and the default tags', () => {
    expect(initialPostFields({ post: noPost })).toEqual({ title: '', description: '', tags: DEFAULT_TAGS })
  })

  it('keeps what was saved before', () => {
    const post = { ...noPost, title: 'Ember Fox', description: 'A fox.', tags: 'fox' }
    expect(initialPostFields({ post })).toEqual({ title: 'Ember Fox', description: 'A fox.', tags: 'fox' })
  })
})

it('credits the inspiration with its link', () => {
  expect(inspirationCredit('https://www.deviantart.com/someone/art/Fox-123')).toBe(
    'Inspired by https://www.deviantart.com/someone/art/Fox-123',
  )
})

it('links a Stash item to its DeviantArt editor', () => {
  expect(stashEditorUrl('9123456789012345678')).toBe(
    'https://www.deviantart.com/_deviation_submit/?deviationid=9123456789012345678',
  )
})
