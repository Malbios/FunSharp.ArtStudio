const FIXED_SECTIONS = ['Setting', 'Composition', 'Art style']
const MIN_PARAGRAPHS = FIXED_SECTIONS.length + 1
const PARAGRAPH_BREAK = /\n\s*\n/

export function hasSeveralParagraphs(text: string): boolean {
  return PARAGRAPH_BREAK.test(text.trim())
}

export function toParagraphs(text: string): string[] {
  return text
    .split(PARAGRAPH_BREAK)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph.length > 0)
}

/**
 * Splits a prompt into paragraph boxes. Exactly three paragraphs are a prompt without characters; other short
 * prompts fill from the start and are padded to the minimum number of boxes.
 */
export function splitParagraphs(text: string): string[] {
  const paragraphs = toParagraphs(text)
  if (paragraphs.length === FIXED_SECTIONS.length) return paragraphs
  while (paragraphs.length < MIN_PARAGRAPHS) paragraphs.push('')
  return paragraphs
}

export function joinParagraphs(paragraphs: string[]): string {
  return paragraphs
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph.length > 0)
    .join('\n\n')
}

/** Labels count from the end: Art style, Composition, Setting, and every paragraph before them is a character. */
export function paragraphLabels(count: number): string[] {
  const characterCount = count - FIXED_SECTIONS.length
  const characters = Array.from({ length: characterCount }, (_, index) =>
    characterCount > 1 ? `Character ${index + 1}` : 'Character',
  )
  return [...characters, ...FIXED_SECTIONS]
}

export function isCharacter(index: number, count: number): boolean {
  return index < count - FIXED_SECTIONS.length
}

/**
 * Pastes several paragraphs into box `index` at the cursor: the first joins the text before the cursor, the last
 * keeps the text after it, and the ones in between fill the following empty boxes or are inserted as new boxes.
 */
export function pasteIntoParagraphs(
  paragraphs: string[],
  index: number,
  before: string,
  pasted: string[],
  after: string,
): string[] {
  const result = [...paragraphs]
  const parts = pasted.map((part, position) => {
    const withBefore = position === 0 ? before + part : part
    return position === pasted.length - 1 ? withBefore + after : withBefore
  })

  result[index] = parts[0]
  let target = index + 1
  for (const part of parts.slice(1)) {
    if (target < result.length && result[target].trim() === '') result[target] = part
    else result.splice(target, 0, part)
    target++
  }
  return result
}

export function addCharacter(paragraphs: string[]): string[] {
  const result = [...paragraphs]
  result.splice(paragraphs.length - FIXED_SECTIONS.length, 0, '')
  return result
}

export function removeParagraph(paragraphs: string[], index: number): string[] {
  if (paragraphs.length <= MIN_PARAGRAPHS) return paragraphs
  return paragraphs.filter((_, position) => position !== index)
}
