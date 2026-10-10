import { useEffect, useRef, useState, type MouseEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import type { BuildingBlock } from '../api'
import { describePreset } from '../prompt/presets'

interface Props {
  presets: BuildingBlock[]
  onChoose: (preset: BuildingBlock) => void
  /** Enables checkboxes to choose several presets at once; they arrive in the order they were checked. */
  onChooseMany?: (presets: BuildingBlock[]) => void
  onClose: () => void
}

/** All presets as a grid of tiles: the example image, or the preset's settings as text when it has none. */
export function PresetPicker({ presets, onChoose, onChooseMany, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [checkedIds, setCheckedIds] = useState<number[]>([])
  const checkedPresets = checkedIds.flatMap((id) => presets.find((preset) => preset.id === id) ?? [])

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  function closeOnBackdrop(event: MouseEvent<HTMLDialogElement>) {
    // Portalled, but React still bubbles the click to the opener; the lightbox would close on it.
    event.stopPropagation()
    if (event.target === dialogRef.current) onClose()
  }

  function toggle(preset: BuildingBlock) {
    setCheckedIds((ids) => (ids.includes(preset.id) ? ids.filter((id) => id !== preset.id) : [...ids, preset.id]))
  }

  function clickTile(preset: BuildingBlock) {
    if (checkedIds.length > 0) toggle(preset)
    else onChoose(preset)
  }

  return createPortal(
    <dialog ref={dialogRef} className="preset-picker" onClose={onClose} onClick={closeOnBackdrop}>
      <div className="preset-picker-body">
        <div className="preset-picker-header">
          <h3>Presets</h3>
          <Link to="/settings#presets" className="hint">
            Manage presets
          </Link>
          {onChooseMany && checkedPresets.length > 0 && (
            <button type="button" className="primary preset-picker-queue" onClick={() => onChooseMany(checkedPresets)}>
              {checkedPresets.length === 1 ? 'Queue 1 preset' : `Queue ${checkedPresets.length} presets`}
            </button>
          )}
          <button type="button" className="link-button" aria-label="Close" onClick={onClose}>
            ✕
          </button>
        </div>
        {presets.length === 0 && <p className="hint">No presets yet. Add some under Manage presets.</p>}
        {onChooseMany && presets.length > 1 && (
          <p className="hint">Click a preset to queue it, or check several to queue them all at once.</p>
        )}
        <div className="preset-grid">
          {presets.map((preset) => {
            const checked = checkedIds.includes(preset.id)
            return (
              <div key={preset.id} className={checked ? 'preset-tile-wrap checked' : 'preset-tile-wrap'}>
                <button
                  type="button"
                  className="preset-tile"
                  title={describePreset(preset)}
                  onClick={() => clickTile(preset)}
                >
                  {preset.imageUrl ? (
                    <img src={preset.imageUrl} alt="" loading="lazy" />
                  ) : (
                    <span className="preset-tile-text">{describePreset(preset)}</span>
                  )}
                  <span className="preset-tile-label">{preset.label}</span>
                </button>
                {onChooseMany && (
                  <input
                    type="checkbox"
                    className="preset-tile-check"
                    aria-label={`Select ${preset.label}`}
                    checked={checked}
                    onChange={() => toggle(preset)}
                  />
                )}
              </div>
            )
          })}
        </div>
      </div>
    </dialog>,
    document.body,
  )
}
