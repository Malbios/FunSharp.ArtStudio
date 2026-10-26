import type { VideoJobSettings, VideoOptions } from '../api'

export const CUSTOM = 'custom'
const FRAMES_PER_SECOND = 24
const FRAME_BLOCK = 17
const FRAME_OFFSET = 5

/** What the video options form holds; presets are chosen by value, CUSTOM switches to the typed number. */
export interface VideoForm {
  prompt: string
  duration: string
  customSeconds: string
  resolution: string
  customMegapixels: string
  turbo: boolean
  count: number
  queueFirst: boolean
}

export function initialVideoForm(options: VideoOptions): VideoForm {
  return {
    prompt: options.promptTemplate,
    duration: String(options.defaultSeconds),
    customSeconds: '',
    resolution: options.defaultResolution,
    customMegapixels: '',
    turbo: true,
    count: 1,
    queueFirst: false,
  }
}

/** The workflow's own snapping: frames at 24 fps, rounded up to the model's 17k+5 frame grid. */
export function videoFrameCount(seconds: number): number {
  const frames = Math.max(FRAME_OFFSET, Math.round(seconds * FRAMES_PER_SECOND))
  const remainder = (((FRAME_OFFSET - (frames % FRAME_BLOCK)) % FRAME_BLOCK) + FRAME_BLOCK) % FRAME_BLOCK
  return frames + remainder
}

export function renderedSeconds(seconds: number): number {
  return videoFrameCount(seconds) / FRAMES_PER_SECOND
}

function parseNumber(text: string): number {
  return text.trim() === '' ? Number.NaN : Number(text.replace(',', '.'))
}

export function chosenSeconds(form: VideoForm): number {
  return form.duration === CUSTOM ? parseNumber(form.customSeconds) : Number(form.duration)
}

export function chosenMegapixels(form: VideoForm, options: VideoOptions): number {
  if (form.resolution === CUSTOM) return parseNumber(form.customMegapixels)
  return options.resolutionPresets.find((preset) => preset.name === form.resolution)?.megapixels ?? Number.NaN
}

export type ResolvedVideoForm = { settings: VideoJobSettings; error?: undefined } | { error: string }

export function resolveVideoForm(form: VideoForm, options: VideoOptions): ResolvedVideoForm {
  const seconds = chosenSeconds(form)
  const megapixels = chosenMegapixels(form, options)
  if (!form.prompt.trim()) return { error: 'Enter a prompt.' }
  if (!(seconds > 0 && seconds <= options.maxSeconds))
    return { error: `The length must be more than 0 and at most ${options.maxSeconds} seconds.` }
  if (!(megapixels > 0 && megapixels <= options.maxMegapixels))
    return { error: `The size must be more than 0 and at most ${options.maxMegapixels} megapixels.` }
  if (!(Number.isInteger(form.count) && form.count >= 1 && form.count <= options.maxCount))
    return { error: `The number of videos must be between 1 and ${options.maxCount}.` }
  return {
    settings: { prompt: form.prompt, seconds, megapixels, turbo: form.turbo, count: form.count, queueFirst: form.queueFirst },
  }
}
