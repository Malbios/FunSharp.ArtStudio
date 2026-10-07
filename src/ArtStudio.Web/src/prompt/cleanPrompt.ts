const REMOVED_WORDS = ['adult']

const notWordCharacter = '(?<![\\p{L}\\p{N}_-])'
const notWordCharacterAhead = '(?![\\p{L}\\p{N}_-])'

const removedWordPatterns = REMOVED_WORDS.map(
  (word) => new RegExp(`${notWordCharacter}${word}${notWordCharacterAhead}`, 'giu'),
)

export function cleanPrompt(text: string): string {
  let cleaned = text.replace(/’/g, "'")
  for (const pattern of removedWordPatterns) {
    cleaned = cleaned.replace(pattern, '')
  }
  return cleaned
    .replace(/[ \t]{2,}/g, ' ')
    .replace(/ +([,.;:!?])/g, '$1')
    .replace(/,(?:[ \t]*,)+/g, ',')
    .replace(/^ +| +$/gm, '')
}
