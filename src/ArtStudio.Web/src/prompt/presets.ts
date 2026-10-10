import type { BuildingBlock } from '../api'
import { joinParagraphs, splitParagraphs } from './paragraphs'

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

export interface PresetRequeue {
  prompt: string
  resolution: string
  count: number
}

/** What a requeue with this preset sends: its art style in the prompt, its resolution and count where it has them. */
export function presetRequeue(
  preset: Pick<BuildingBlock, 'artStyle' | 'resolution' | 'imageCount'>,
  prompt: string,
  resolution: string,
  fallbackCount: number,
): PresetRequeue {
  return {
    prompt: joinParagraphs(withArtStyle(splitParagraphs(prompt), preset.artStyle)),
    resolution: preset.resolution ?? resolution,
    count: preset.imageCount ?? fallbackCount,
  }
}

export interface ResolvedBundle {
  bundle: BuildingBlock
  presets: BuildingBlock[]
}

/** Each bundle with its presets in order; deleted presets are skipped and bundles left empty are dropped. */
export function resolveBundles(blocks: BuildingBlock[]): ResolvedBundle[] {
  const presetsById = new Map(blocks.filter((block) => block.kind === 'Preset').map((preset) => [preset.id, preset]))
  return blocks
    .filter((block) => block.kind === 'Bundle')
    .map((bundle) => ({ bundle, presets: bundle.presetIds.flatMap((id) => presetsById.get(id) ?? []) }))
    .filter((resolved) => resolved.presets.length > 0)
}
