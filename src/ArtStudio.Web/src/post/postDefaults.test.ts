import { describe, expect, it } from 'vitest'
import type { PostDetails } from '../api'
import { DEFAULT_TAGS, initialPostFields, stashDescription, stashEditorUrl } from './postDefaults'

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

describe('stash description', () => {
  const url = 'https://www.deviantart.com/someone/art/Fox-123'

  it('ends with an Inspired by line for the link to be pasted after', () => {
    expect(stashDescription('', url)).toBe('Inspired by\n')
    expect(stashDescription(' A fox at dawn. ', url)).toBe('A fox at dawn.\n\nInspired by\n')
  })

  it('adds no second Inspired by line to a description uploaded before', () => {
    expect(stashDescription('A fox at dawn.\n\nInspired by\n', url)).toBe('A fox at dawn.\n\nInspired by\n')
  })

  it('is just the typed description without a DeviantArt inspiration', () => {
    expect(stashDescription(' A fox at dawn. ', null)).toBe('A fox at dawn.')
  })
})

it('links a Stash item to its DeviantArt editor', () => {
  expect(stashEditorUrl('9123456789012345678')).toBe(
    'https://www.deviantart.com/_deviation_submit/?deviationid=9123456789012345678',
  )
})
