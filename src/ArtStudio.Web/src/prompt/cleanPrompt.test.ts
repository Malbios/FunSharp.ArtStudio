import { describe, expect, it } from 'vitest'
import { cleanPrompt } from './cleanPrompt'

describe('cleanPrompt', () => {
  it('replaces typographic apostrophes', () => {
    expect(cleanPrompt('the fox’s den, it’s late')).toBe("the fox's den, it's late")
  })

  it('removes the whole word adult in any casing', () => {
    expect(cleanPrompt('an adult fox')).toBe('an fox')
    expect(cleanPrompt('Adult fox')).toBe('fox')
    expect(cleanPrompt('a fox, ADULT')).toBe('a fox,')
  })

  it('keeps words that only contain adult', () => {
    expect(cleanPrompt('adulthood, adults, nonadult, adult-like')).toBe('adulthood, adults, nonadult, adult-like')
  })

  it('tidies spacing left behind without touching line breaks', () => {
    expect(cleanPrompt('a fox, adult , in snow\nsecond line')).toBe('a fox, in snow\nsecond line')
    expect(cleanPrompt('first line\nadult fox\nlast adult')).toBe('first line\nfox\nlast')
  })

  it('leaves surrounding whitespace of pasted text alone', () => {
    expect(cleanPrompt(' She has a hyper-sized bust.')).toBe(' She has a hyper-sized bust.')
    expect(cleanPrompt('  two leading spaces, trailing ')).toBe('  two leading spaces, trailing ')
    expect(cleanPrompt('line one\n\nline   two')).toBe('line one\n\nline   two')
  })

  it('keeps a leading space while removing a word elsewhere', () => {
    expect(cleanPrompt(' She is an adult woman.')).toBe(' She is an woman.')
  })

  it('leaves clean text unchanged', () => {
    expect(cleanPrompt('a red fox in the snow')).toBe('a red fox in the snow')
  })
})
