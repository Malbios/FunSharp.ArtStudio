import { useEffect, useRef, useState, type MouseEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import type { BuildingBlock } from '../api'
import { describePreset, type ResolvedBundle } from '../prompt/presets'

const BUNDLE_MOSAIC_SIZE = 4

interface Props {
  presets: BuildingBlock[]
  onChoose: (preset: BuildingBlock) => void
  /** Enables bundles and choosing several presets; the presets arrive in bundle order or the order they were checked. */
  onChooseMany?: (presets: BuildingBlock[]) => void
  bundles?: ResolvedBundle[]
  onClose: () => void
}

/** All presets as a grid of tiles: the example image, or the preset's settings as text when it has none. */
export function PresetPicker({ presets, onChoose, onChooseMany, bundles = [], onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [selectingSeveral, setSelectingSeveral] = useState(false)
  const [checkedIds, setCheckedIds] = useState<number[]>([])
  const checkedPresets = checkedIds.flatMap((id) => presets.find((preset) => preset.id === id) ?? [])
  const shownBundles = onChooseMany && !selectingSeveral ? bundles : []

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  function closeOnBackdrop(event: MouseEvent<HTMLDialogElement>) {
    // Portalled, but React still bubbles the click to the opener; the lightbox would close on it.
    event.stopPropagation()
    if (event.target === dialogRef.current) onClose()
  }

  function toggleSelectingSeveral(on: boolean) {
    setSelectingSeveral(on)
    setCheckedIds([])
  }

  function toggle(preset: BuildingBlock) {
    setCheckedIds((ids) => (ids.includes(preset.id) ? ids.filter((id) => id !== preset.id) : [...ids, preset.id]))
  }

  function clickTile(preset: BuildingBlock) {
    if (selectingSeveral) toggle(preset)
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
          {onChooseMany && presets.length > 1 && (
            <label className="preset-picker-several">
              <input
                type="checkbox"
                checked={selectingSeveral}
                onChange={(e) => toggleSelectingSeveral(e.target.checked)}
              />
              Select several
            </label>
          )}
          {onChooseMany && checkedPresets.length > 0 && (
            <button type="button" className="primary preset-picker-queue" onClick={() => onChooseMany(checkedPresets)}>
              {checkedPresets.length === 1 ? 'Queue 1 preset' : `Queue ${checkedPresets.length} presets`}
            </button>
          )}
          <button type="button" className="link-button preset-picker-close" aria-label="Close" onClick={onClose}>
            ✕
          </button>
        </div>
        {presets.length === 0 && <p className="hint">No presets yet. Add some under Manage presets.</p>}
        {shownBundles.length > 0 && (
          <>
            <div className="preset-grid">
              {shownBundles.map(({ bundle, presets: bundled }) => (
                <BundleTile key={bundle.id} bundle={bundle} presets={bundled} onClick={() => onChooseMany?.(bundled)} />
              ))}
            </div>
            <hr className="preset-picker-divider" />
          </>
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
                {selectingSeveral && (
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

function BundleTile({ bundle, presets, onClick }: { bundle: BuildingBlock; presets: BuildingBlock[]; onClick: () => void }) {
  const images = presets.flatMap((preset) => preset.imageUrl ?? []).slice(0, BUNDLE_MOSAIC_SIZE)
  const labels = presets.map((preset) => preset.label)
  return (
    <button type="button" className="preset-tile bundle-tile" title={labels.join('\n')} onClick={onClick}>
      {images.length > 0 ? (
        <span className={`bundle-mosaic count-${images.length}`}>
          {images.map((url) => (
            <img key={url} src={url} alt="" loading="lazy" />
          ))}
        </span>
      ) : (
        <span className="preset-tile-text">{labels.join('\n')}</span>
      )}
      <span className="preset-tile-label">
        {bundle.label} · {presets.length === 1 ? '1 preset' : `${presets.length} presets`}
      </span>
    </button>
  )
}
