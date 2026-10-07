/** Splits pasted text into distinct entries separated by whitespace or commas. */
export function parseDeviantArtUrls(text: string): string[] {
  const entries = text.split(/[\s,]+/).filter((entry) => entry.length > 0)
  return [...new Set(entries)]
}
