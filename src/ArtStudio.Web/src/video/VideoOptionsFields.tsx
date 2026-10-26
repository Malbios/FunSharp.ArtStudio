import type { VideoOptions } from '../api'
import { chosenSeconds, CUSTOM, renderedSeconds, type VideoForm } from './videoForm'

interface Props {
  form: VideoForm
  options: VideoOptions
  disabled?: boolean
  onChange: (form: VideoForm) => void
}

/** Duration, size, turbo, count and the editable prompt of a video job; shared by the set page and the New page. */
export function VideoOptionsFields({ form, options, disabled, onChange }: Props) {
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
        <label className="field">
          <span>Size</span>
          <select value={form.resolution} disabled={disabled} onChange={(e) => update({ resolution: e.target.value })}>
            {options.resolutionPresets.map((preset) => (
              <option key={preset.name} value={preset.name}>
                {preset.name} ({preset.megapixels} MP)
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
        Sizes are named for a 16:9 start image; other shapes keep the same number of pixels.
      </p>
      <label className="inline-check" title="The Lightning LoRA: 8 steps instead of 20">
        <input type="checkbox" checked={form.turbo} disabled={disabled} onChange={(e) => update({ turbo: e.target.checked })} />
        Turbo (8 steps instead of 20)
      </label>
      <label className="field">
        <span className="prompt-label-row">
          Prompt
          {form.prompt !== options.promptTemplate && (
            <button
              type="button"
              className="link-button"
              disabled={disabled}
              onClick={() => update({ prompt: options.promptTemplate })}
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
          onChange={(e) => update({ prompt: e.target.value })}
        />
        <span className="hint">Replace ??? with what happens. The sound and music lines describe the audio track.</span>
      </label>
    </>
  )
}
