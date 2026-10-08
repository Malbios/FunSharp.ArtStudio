import { describe, expect, it } from 'vitest'
import {
  addCharacter,
  hasSeveralParagraphs,
  joinedIndex,
  joinParagraphs,
  paragraphLabels,
  pasteIntoParagraphs,
  removeParagraph,
  splitParagraphs,
} from './paragraphs'

describe('splitParagraphs', () => {
  it('splits on blank lines, also ones with spaces or several in a row', () => {
    expect(splitParagraphs('a\n\nb\n  \nc\n\n\n\nd')).toEqual(['a', 'b', 'c', 'd'])
  })

  it('keeps single line breaks inside a paragraph', () => {
    expect(splitParagraphs('a\nstill a\n\nb\n\nc\n\nd')[0]).toBe('a\nstill a')
  })

  it('fills short prompts from the start and pads to four boxes', () => {
    expect(splitParagraphs('one paragraph')).toEqual(['one paragraph', '', '', ''])
    expect(splitParagraphs('')).toEqual(['', '', '', ''])
  })

  it('treats exactly three paragraphs as a prompt without characters', () => {
    const paragraphs = splitParagraphs('setting\n\ncomposition\n\nstyle')
    expect(paragraphs).toEqual(['setting', 'composition', 'style'])
    expect(paragraphLabels(paragraphs.length)).toEqual(['Setting', 'Composition', 'Art style'])
  })

  it('keeps every paragraph of long prompts', () => {
    expect(splitParagraphs('a\n\nb\n\nc\n\nd\n\ne\n\nf')).toHaveLength(6)
  })
})

describe('joinParagraphs', () => {
  it('joins non-empty boxes with one blank line', () => {
    expect(joinParagraphs(['a ', '', ' b', 'c\nc'])).toBe('a\n\nb\n\nc\nc')
    expect(joinParagraphs(['', '', '', ''])).toBe('')
  })
})

describe('paragraphLabels', () => {
  it('counts from the end', () => {
    expect(paragraphLabels(4)).toEqual(['Character', 'Setting', 'Composition', 'Art style'])
    expect(paragraphLabels(6)).toEqual(['Character 1', 'Character 2', 'Character 3', 'Setting', 'Composition', 'Art style'])
  })
})

describe('hasSeveralParagraphs', () => {
  it('ignores blank lines at the edges', () => {
    expect(hasSeveralParagraphs('\n\nonly one\n\n')).toBe(false)
    expect(hasSeveralParagraphs('one\n\ntwo')).toBe(true)
  })
})

describe('joinedIndex', () => {
  it('skips empty boxes before the box', () => {
    expect(joinedIndex(['', 'c2', 's', 'co', 'a'], 4)).toBe(3)
    expect(joinedIndex(['c', 's', 'co', 'a'], 0)).toBe(0)
  })
})

describe('pasteIntoParagraphs', () => {
  it('fills the following empty boxes', () => {
    expect(pasteIntoParagraphs(['', '', '', ''], 0, '', ['a', 'b', 'c', 'd'], '')).toEqual(['a', 'b', 'c', 'd'])
  })

  it('inserts new boxes before filled ones', () => {
    expect(pasteIntoParagraphs(['x', 'set', 'comp', 'art'], 0, '', ['a', 'b'], '')).toEqual(['a', 'b', 'set', 'comp', 'art'])
  })

  it('keeps the text around the cursor', () => {
    expect(pasteIntoParagraphs(['start end', '', 'c', 'd'], 0, 'start ', ['a', 'b'], ' end')).toEqual([
      'start a',
      'b end',
      'c',
      'd',
    ])
  })
})

describe('character boxes', () => {
  it('adds a character before Setting', () => {
    expect(addCharacter(['c', 's', 'co', 'a'])).toEqual(['c', '', 's', 'co', 'a'])
    expect(addCharacter(['s', 'co', 'a'])).toEqual(['', 's', 'co', 'a'])
  })

  it('removes a character but never goes below four boxes', () => {
    expect(removeParagraph(['c1', 'c2', 's', 'co', 'a'], 0)).toEqual(['c2', 's', 'co', 'a'])
    expect(removeParagraph(['c', 's', 'co', 'a'], 0)).toEqual(['c', 's', 'co', 'a'])
  })
})
