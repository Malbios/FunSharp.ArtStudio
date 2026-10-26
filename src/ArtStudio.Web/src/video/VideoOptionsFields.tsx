import type { VideoOptions, VideoResolutionPreset } from '../api'
import { chosenSeconds, CUSTOM, renderedSeconds, type VideoForm } from './videoForm'

interface FieldsProps {
  form: VideoForm
  options: VideoOptions
  disabled?: boolean
  onChange: (form: VideoForm) => void
}

interface OptionsProps extends FieldsProps {
  /** The start image's shape, e.g. "896×1152 (7:9) - Portrait", when known. */
  startShape?: string
}

function presetLabel(preset: VideoResolutionPreset): string {
  return `${preset.name} (16:9 widescreen) · ${preset.megapixels} MP${preset.note ? ` - ${preset.note}` : ''}`
}

/** Duration, size, turbo and count of a video job; shared by the set's Make video page and the New page. */
export function VideoOptionsFields({ form, options, disabled, startShape, onChange }: OptionsProps) {
  const update = (changes: Partial<VideoForm>) => onChange({ ...form, ...changes })
  const seconds = chosenSeconds(form)
  const lengthHint = seconds > 0 ? `renders ${renderedSeconds(seconds).toFixed(2)} s` : undefined

  return (
    <>
      <div className="field-row video-option-row">
        <label className="field">
          <span>Duration {lengthHint && <em className="hint">({lengthHint})</em>}</span>
          <select value={form.duration} disabled={disabled} onChange={(e) => update({ duration: e.target.value })}>
            {options.durationPresets.map((preset) => (
              <option key={preset} value={String(preset)}>
                {preset} s
              </option>
            ))}
            <option value={CUSTOM}>Custom…</option>
          </select>
        </label>
        {form.duration === CUSTOM && (
          <label className="field narrow">
            <span>Seconds</span>
            <input
              type="number"
              min={0.5}
              max={options.maxSeconds}
              step={0.5}
              value={form.customSeconds}
              disabled={disabled}
              onChange={(e) => update({ customSeconds: e.target.value })}
            />
          </label>
        )}
        <label className="field video-size-field">
          <span>Size</span>
          <select value={form.resolution} disabled={disabled} onChange={(e) => update({ resolution: e.target.value })}>
            {options.resolutionPresets.map((preset) => (
              <option key={preset.name} value={preset.name}>
                {presetLabel(preset)}
              </option>
            ))}
            <option value={CUSTOM}>Custom megapixels…</option>
          </select>
        </label>
        {form.resolution === CUSTOM && (
          <label className="field narrow">
            <span>Megapixels</span>
            <input
              type="number"
              min={0.05}
              max={options.maxMegapixels}
              step={0.01}
              value={form.customMegapixels}
              disabled={disabled}
              onChange={(e) => update({ customMegapixels: e.target.value })}
            />
          </label>
        )}
        <label className="field narrow">
          <span>Videos</span>
          <input
            type="number"
            min={1}
            max={options.maxCount}
            value={form.count}
            disabled={disabled}
            onChange={(e) => update({ count: Math.max(1, Number(e.target.value) || 1) })}
          />
        </label>
      </div>
      <p className="hint">
        The sizes are what a 16:9 widescreen start image gets. The video always takes the start image's shape, with about
        the same number of pixels.
        {startShape && <> This start image is {startShape}.</>}
      </p>
      <label className="inline-check" title="The Lightning LoRA: 8 steps instead of 20">
        <input type="checkbox" checked={form.turbo} disabled={disabled} onChange={(e) => update({ turbo: e.target.checked })} />
        Turbo (8 steps instead of 20)
      </label>
    </>
  )
}

/** The video's whole prompt, pre-filled with the workflow's template. */
export function VideoPromptField({ form, options, disabled, onChange }: FieldsProps) {
  return (
    <label className="field">
      <span className="prompt-label-row">
        Prompt
        {form.prompt !== options.promptTemplate && (
          <button
            type="button"
            className="link-button"
            disabled={disabled}
            onClick={() => onChange({ ...form, prompt: options.promptTemplate })}
          >
            Reset to template
          </button>
        )}
      </span>
      <textarea
        className="video-prompt"
        rows={10}
        value={form.prompt}
        disabled={disabled}
        onChange={(e) => onChange({ ...form, prompt: e.target.value })}
      />
      <span className="hint">Replace ??? with what happens. The sound and music lines describe the audio track.</span>
    </label>
  )
}
