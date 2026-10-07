/** Splits pasted text into distinct entries separated by spaces or line breaks. */
export function parseDeviantArtUrls(text: string): string[] {
  const entries = text.split(/\s+/).filter((entry) => entry.length > 0)
  return [...new Set(entries)]
}
