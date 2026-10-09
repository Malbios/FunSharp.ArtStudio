import { useState, type ClipboardEvent, type FormEvent } from 'react'
import { api, type BuildingBlock, type PresetFields, type ResolutionPreset } from '../api'
import { useLoad } from '../live/useLoad'
import { imageFileFrom } from '../newset/imageSource'
import { cleanPrompt } from '../prompt/cleanPrompt'
import { insertAtSelection } from '../prompt/insertAtSelection'
import { PresetImage, readClipboardImage } from './PresetImage'

const EMPTY_PRESET: PresetFields = { label: '', artStyle: null, resolution: null, imageCount: null }

export function PresetsEditor() {
  const blocks = useLoad(api.buildingBlocks)
  const resolutions = useLoad(api.resolutions)
  const [addingAt, setAddingAt] = useState<number>()
  const [newPreset, setNewPreset] = useState(EMPTY_PRESET)
  const [error, setError] = useState<string>()
  const list = blocks.data?.filter((block) => block.kind === 'Preset') ?? []

  async function run(action: () => Promise<unknown>) {
    setError(undefined)
    try {
      await action()
      blocks.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  function startAdding(position: number) {
    setNewPreset(EMPTY_PRESET)
    setAddingAt(position)
  }

  async function add(event: FormEvent, position: number) {
    event.preventDefault()
    await run(async () => {
      const created = await api.addPreset(newPreset)
      const ids = list.map((block) => block.id)
      ids.splice(position, 0, created.id)
      await api.reorderBuildingBlocks(ids)
      setAddingAt(undefined)
    })
  }

  function move(index: number, offset: number) {
    const ids = list.map((block) => block.id)
    const [moved] = ids.splice(index, 1)
    ids.splice(index + offset, 0, moved)
    void run(() => api.reorderBuildingBlocks(ids))
  }

  const insertPoint = (position: number) =>
    addingAt === position ? (
      <form
        key={`new-${position}`}
        className="editable-row preset-row new-preset"
        onSubmit={(e) => void add(e, position)}
      >
        <PresetInputs fields={newPreset} resolutions={resolutions.data ?? []} onChange={setNewPreset} />
        <div className="row-actions">
          <button type="button" onClick={() => setAddingAt(undefined)}>
            Cancel
          </button>
          <button type="submit" className="primary" disabled={!newPreset.label.trim()}>
            Add
          </button>
        </div>
      </form>
    ) : (
      <button
        key={`insert-${position}`}
        type="button"
        className="link-button add-preset-here"
        onClick={() => startAdding(position)}
      >
        {list.length === 0 ? '+ Add preset' : '+ Add preset here'}
      </button>
    )

  return (
    <section className="panel" id="presets">
      <h3>Presets</h3>
      <p className="hint">
        Shown above the building blocks. Clicking one applies it: it replaces the Art style box and sets Resolution and
        Images. Empty fields leave that part as it is. The example image shows when hovering the preset.
      </p>
      <div className="editable-list">
        {insertPoint(0)}
        {list.map((preset, index) => [
          <PresetRow
            key={`${preset.id}-${preset.label}-${preset.artStyle}-${preset.resolution}-${preset.imageCount}`}
            preset={preset}
            resolutions={resolutions.data ?? []}
            canMoveUp={index > 0}
            canMoveDown={index < list.length - 1}
            onSave={(fields) => void run(() => api.updatePreset(preset.id, fields))}
            onDelete={() => void run(() => api.deleteBuildingBlock(preset.id))}
            onImage={(image) => void run(() => api.setPresetImage(preset.id, image))}
            onPasteFromClipboard={() => void run(async () => api.setPresetImage(preset.id, await readClipboardImage()))}
            onRemoveImage={() => void run(() => api.removePresetImage(preset.id))}
            onMove={(offset) => move(index, offset)}
          />,
          insertPoint(index + 1),
        ])}
      </div>
      {(error ?? blocks.error) && <p className="error">{error ?? blocks.error}</p>}
    </section>
  )
}

interface RowProps {
  preset: BuildingBlock
  resolutions: ResolutionPreset[]
  canMoveUp: boolean
  canMoveDown: boolean
  onSave: (fields: PresetFields) => void
  onDelete: () => void
  onMove: (offset: number) => void
  onImage: (image: Blob) => void
  onPasteFromClipboard: () => void
  onRemoveImage: () => void
}

function PresetRow(props: RowProps) {
  const { preset, resolutions, canMoveUp, canMoveDown, onSave, onDelete, onMove } = props
  const { onImage, onPasteFromClipboard, onRemoveImage } = props
  const saved: PresetFields = {
    label: preset.label,
    artStyle: preset.artStyle,
    resolution: preset.resolution,
    imageCount: preset.imageCount,
  }
  const [fields, setFields] = useState(saved)
  const changed = JSON.stringify(fields) !== JSON.stringify(saved)

  function pasteImage(event: ClipboardEvent) {
    const image = imageFileFrom(event.clipboardData)
    if (!image) return
    event.preventDefault()
    onImage(image)
  }

  return (
    <div className="editable-row preset-row" onPaste={pasteImage}>
      <PresetInputs fields={fields} resolutions={resolutions} onChange={setFields} />
      <div className="row-actions">
        <button type="button" disabled={!canMoveUp} onClick={() => onMove(-1)} title="Move up">
          ↑
        </button>
        <button type="button" disabled={!canMoveDown} onClick={() => onMove(1)} title="Move down">
          ↓
        </button>
        <button type="button" className="primary" disabled={!changed} onClick={() => onSave(fields)}>
          Save
        </button>
        <button type="button" className="danger" onClick={onDelete}>
          Delete
        </button>
      </div>
      <PresetImage
        preset={preset}
        onImage={onImage}
        onPasteFromClipboard={onPasteFromClipboard}
        onRemove={onRemoveImage}
      />
    </div>
  )
}

interface InputsProps {
  fields: PresetFields
  resolutions: ResolutionPreset[]
  onChange: (fields: PresetFields) => void
}

function PresetInputs({ fields, resolutions, onChange }: InputsProps) {
  const change = (patch: Partial<PresetFields>) => onChange({ ...fields, ...patch })

  function pasteTrimmed(event: ClipboardEvent<HTMLTextAreaElement>) {
    const text = event.clipboardData.getData('text')
    if (!text) return
    event.preventDefault()
    insertAtSelection(event.currentTarget, cleanPrompt(text).trim())
  }

  return (
    <>
      <input
        className="preset-label"
        placeholder="Label"
        value={fields.label}
        onChange={(e) => change({ label: e.target.value })}
      />
      <textarea
        className="preset-style"
        rows={5}
        placeholder="Art style (empty keeps the Art style box as it is)"
        value={fields.artStyle ?? ''}
        onChange={(e) => change({ artStyle: e.target.value || null })}
        onPaste={pasteTrimmed}
      />
      <select
        className="preset-resolution"
        aria-label="Resolution"
        value={fields.resolution ?? ''}
        onChange={(e) => change({ resolution: e.target.value || null })}
      >
        <option value="">Keep resolution</option>
        {resolutions.map((preset) => (
          <option key={preset.name} value={preset.name}>
            {preset.name} ({preset.width}×{preset.height})
          </option>
        ))}
      </select>
      <input
        className="preset-count"
        type="number"
        min={1}
        max={100}
        aria-label="Images"
        placeholder="Keep images"
        value={fields.imageCount ?? ''}
        onChange={(e) => change({ imageCount: e.target.value ? Number(e.target.value) : null })}
      />
    </>
  )
}
