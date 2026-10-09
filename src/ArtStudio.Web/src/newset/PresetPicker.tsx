import { useEffect, useRef, type MouseEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import type { BuildingBlock } from '../api'
import { describePreset } from '../prompt/presets'

interface Props {
  presets: BuildingBlock[]
  onChoose: (preset: BuildingBlock) => void
  onClose: () => void
}

/** All presets as a grid of tiles: the example image, or the preset's settings as text when it has none. */
export function PresetPicker({ presets, onChoose, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  function closeOnBackdrop(event: MouseEvent<HTMLDialogElement>) {
    // Portalled, but React still bubbles the click to the opener; the lightbox would close on it.
    event.stopPropagation()
    if (event.target === dialogRef.current) onClose()
  }

  return createPortal(
    <dialog ref={dialogRef} className="preset-picker" onClose={onClose} onClick={closeOnBackdrop}>
      <div className="preset-picker-body">
        <div className="preset-picker-header">
          <h3>Presets</h3>
          <Link to="/settings#presets" className="hint">
            Manage presets
          </Link>
          <button type="button" className="link-button" aria-label="Close" onClick={onClose}>
            ✕
          </button>
        </div>
        {presets.length === 0 && <p className="hint">No presets yet. Add some under Manage presets.</p>}
        <div className="preset-grid">
          {presets.map((preset) => (
            <button
              key={preset.id}
              type="button"
              className="preset-tile"
              title={describePreset(preset)}
              onClick={() => onChoose(preset)}
            >
              {preset.imageUrl ? (
                <img src={preset.imageUrl} alt="" loading="lazy" />
              ) : (
                <span className="preset-tile-text">{describePreset(preset)}</span>
              )}
              <span className="preset-tile-label">{preset.label}</span>
            </button>
          ))}
        </div>
      </div>
    </dialog>,
    document.body,
  )
}
