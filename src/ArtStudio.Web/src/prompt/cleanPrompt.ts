const REMOVED_WORDS = ['adult']

const notWordCharacter = '(?<![\\p{L}\\p{N}_-])'
const notWordCharacterAhead = '(?![\\p{L}\\p{N}_-])'

const removedWordPatterns = REMOVED_WORDS.map(
  (word) => new RegExp(`[ \\t]*${notWordCharacter}${word}${notWordCharacterAhead}[ \\t]*`, 'giu'),
)

const startsWithPunctuation = /^[,.;:!?]/

/** Replaces a removed word and its surrounding spaces, keeping one space only between two remaining words. */
function replaceRemovedWord(match: string, offset: number, text: string): string {
  const before = text.slice(0, offset)
  const after = text.slice(offset + match.length)
  const atLineStart = before === '' || before.endsWith('\n')
  const atLineEnd = after === '' || after.startsWith('\n')
  return atLineStart || atLineEnd || startsWithPunctuation.test(after) ? '' : ' '
}

export function cleanPrompt(text: string): string {
  let cleaned = text.replace(/’/g, "'")
  for (const pattern of removedWordPatterns) {
    cleaned = cleaned.replace(pattern, replaceRemovedWord)
  }
  return cleaned.replace(/,(?:[ \t]*,)+/g, ',')
}
