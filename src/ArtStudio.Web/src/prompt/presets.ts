import type { BuildingBlock } from '../api'

/** Replaces the last box (Art style) with the preset's art style; without one the boxes stay as they are. */
export function withArtStyle(paragraphs: string[], artStyle: string | null): string[] {
  if (!artStyle?.trim()) return paragraphs
  return [...paragraphs.slice(0, -1), artStyle.trim()]
}

export function describePreset(preset: Pick<BuildingBlock, 'artStyle' | 'resolution' | 'imageCount'>): string {
  return [
    preset.artStyle && `Art style: ${preset.artStyle}`,
    preset.resolution && `Resolution: ${preset.resolution}`,
    preset.imageCount && `Images: ${preset.imageCount}`,
  ]
    .filter(Boolean)
    .join('\n')
}
