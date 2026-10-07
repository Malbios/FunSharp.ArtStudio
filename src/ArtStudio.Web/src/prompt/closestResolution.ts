import type { ResolutionPreset } from '../api'

export function closestResolution(presets: ResolutionPreset[], width: number, height: number): ResolutionPreset {
  const logAspect = Math.log(width / height)
  const distance = (preset: ResolutionPreset) => Math.abs(logAspect - Math.log(preset.width / preset.height))
  return presets.reduce((best, preset) => (distance(preset) < distance(best) ? preset : best))
}
