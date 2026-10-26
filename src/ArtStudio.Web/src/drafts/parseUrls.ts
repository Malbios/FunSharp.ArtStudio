/** Splits pasted text into distinct entries separated by spaces or line breaks. */
export function parseDeviantArtUrls(text: string): string[] {
  const entries = text.split(/\s+/).filter((entry) => entry.length > 0)
  return [...new Set(entries)]
}

export interface CompleteEntries {
  complete: string[]
  rest: string
}

/** Takes the entries that whitespace already follows; the last entry may still be being typed, so it stays as rest. */
export function takeCompleteEntries(text: string): CompleteEntries {
  const lastWhitespace = text.search(/\s\S*$/)
  if (lastWhitespace < 0) return { complete: [], rest: text }
  return { complete: parseDeviantArtUrls(text.slice(0, lastWhitespace)), rest: text.slice(lastWhitespace + 1) }
}
