import { describe, expect, it } from 'vitest'
import { parseDeviantArtUrls } from './parseUrls'

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
