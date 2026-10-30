import { describe, expect, it } from 'vitest'
import type { Gallery, PostDetails } from '../api'
import {
  awaitsInspirationLink,
  DEFAULT_TAGS,
  hasOnlyDefaultTags,
  initialPostFields,
  initialPublishChoice,
  stashDescription,
  stashEditorUrl,
  withSuggestedTags,
} from './postDefaults'

const noPost: PostDetails = {
  title: null,
  description: null,
  tags: null,
  stashItemId: null,
  stashedAt: null,
  publishedUrl: null,
  publishedAt: null,
  suggesting: false,
  publishGalleryId: null,
  publishMatureLevel: null,
}

describe('initial publish choice', () => {
  const galleries: Gallery[] = [
    { folderId: 'scenery', name: 'Scenery', mature: false, lastUsed: true },
    { folderId: 'spicy', name: 'Spicy', mature: true, lastUsed: false },
  ]

  it('starts at the gallery published to last, with its mature setting', () => {
    expect(initialPublishChoice(galleries, noPost)).toEqual({ galleryId: 'scenery', matureLevel: 'none' })
  })

  it('keeps what was chosen for the set', () => {
    expect(initialPublishChoice(galleries, { publishGalleryId: 'spicy', publishMatureLevel: 'strict' })).toEqual({
      galleryId: 'spicy',
      matureLevel: 'strict',
    })
  })

  it('falls back to the gallery published to last when the chosen one is gone, keeping the mature choice', () => {
    expect(initialPublishChoice(galleries, { publishGalleryId: 'deleted', publishMatureLevel: 'moderate' })).toEqual({
      galleryId: 'scenery',
      matureLevel: 'moderate',
    })
  })

  it('has no gallery when none was chosen or published to', () => {
    expect(initialPublishChoice([{ ...galleries[1], lastUsed: false }], noPost)).toEqual({ galleryId: '', matureLevel: 'none' })
  })
})

it('tells default-only tags from real ones', () => {
  expect(hasOnlyDefaultTags('')).toBe(true)
  expect(hasOnlyDefaultTags(' made_with_ai  #Digital_Art ')).toBe(true)
  expect(hasOnlyDefaultTags('digital_art fox')).toBe(false)
})

describe('initial post fields', () => {
  it('starts with an empty title and description and the default tags', () => {
    expect(initialPostFields({ post: noPost })).toEqual({ title: '', description: '', tags: DEFAULT_TAGS })
  })

  it('keeps what was saved before', () => {
    const post = { ...noPost, title: 'Ember Fox', description: 'A fox.', tags: 'fox' }
    expect(initialPostFields({ post })).toEqual({ title: 'Ember Fox', description: 'A fox.', tags: 'fox' })
  })
})

it('adds suggested tags after the current ones, skipping those already there', () => {
  expect(withSuggestedTags(' digital_art  #Fox made_with_ai ', ['fox', 'lantern', 'digital_art', 'snow'])).toBe(
    'digital_art #Fox made_with_ai lantern snow',
  )
  expect(withSuggestedTags('', ['fox'])).toBe('fox')
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

  it('asks for the link only while the uploaded description still ends with Inspired by', () => {
    expect(awaitsInspirationLink('A fox at dawn.\n\nInspired by\n', url)).toBe(true)
    expect(awaitsInspirationLink('A fox at dawn.', url)).toBe(false)
    expect(awaitsInspirationLink(null, url)).toBe(false)
    expect(awaitsInspirationLink('Inspired by\n', null)).toBe(false)
  })
})

it('links a Stash item to its DeviantArt editor', () => {
  expect(stashEditorUrl('9123456789012345678')).toBe(
    'https://www.deviantart.com/_deviation_submit/?deviationid=9123456789012345678',
  )
})
