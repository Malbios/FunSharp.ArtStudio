import { describe, expect, it } from 'vitest'
import { parseDeviantArtUrls, takeCompleteEntries } from './parseUrls'

const first = 'https://www.deviantart.com/a/art/One-1'
const second = 'https://www.deviantart.com/b/art/Two-2'

describe('parseDeviantArtUrls', () => {
  it('splits on line breaks, spaces or both', () => {
    expect(parseDeviantArtUrls(`${first}\n${second}`)).toEqual([first, second])
    expect(parseDeviantArtUrls(`${first} ${second}`)).toEqual([first, second])
    expect(parseDeviantArtUrls(`${first} \n ${second}`)).toEqual([first, second])
  })

  it('keeps commas inside a URL', () => {
    const withComma = 'https://www.deviantart.com/a/art/Morning,-oil-painting-3'
    expect(parseDeviantArtUrls(`${withComma}\n${second}`)).toEqual([withComma, second])
  })

  it('ignores blank entries and surrounding whitespace', () => {
    expect(parseDeviantArtUrls(`\n  ${first}\r\n\r\n\t${second}  \n`)).toEqual([first, second])
    expect(parseDeviantArtUrls('  \n ')).toEqual([])
  })

  it('keeps only the first of repeated entries', () => {
    expect(parseDeviantArtUrls(`${first}\n${second}\n${first}`)).toEqual([first, second])
  })
})

describe('takeCompleteEntries', () => {
  it('takes an entry once whitespace follows it', () => {
    expect(takeCompleteEntries(`${first} `)).toEqual({ complete: [first], rest: '' })
    expect(takeCompleteEntries(`${first}\n`)).toEqual({ complete: [first], rest: '' })
  })

  it('leaves an entry that is still being typed', () => {
    expect(takeCompleteEntries('https://www.devi')).toEqual({ complete: [], rest: 'https://www.devi' })
  })

  it('takes every finished entry and keeps the unfinished last one', () => {
    expect(takeCompleteEntries(`${first}\n${second} https://www`)).toEqual({ complete: [first, second], rest: 'https://www' })
  })

  it('takes nothing from whitespace alone', () => {
    expect(takeCompleteEntries('')).toEqual({ complete: [], rest: '' })
    expect(takeCompleteEntries(' \n ')).toEqual({ complete: [], rest: '' })
  })
})
