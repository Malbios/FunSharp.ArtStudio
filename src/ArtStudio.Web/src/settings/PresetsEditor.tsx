import { useState, type FormEvent } from 'react'
import { api, type BuildingBlock, type PresetFields, type ResolutionPreset } from '../api'
import { useLoad } from '../live/useLoad'

const EMPTY_PRESET: PresetFields = { label: '', artStyle: null, resolution: null, imageCount: null }

export function PresetsEditor() {
  const blocks = useLoad(api.buildingBlocks)
  const resolutions = useLoad(api.resolutions)
  const [newPreset, setNewPreset] = useState(EMPTY_PRESET)
  const [addVersion, setAddVersion] = useState(0)
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

  async function add(event: FormEvent) {
    event.preventDefault()
    await run(async () => {
      await api.addPreset(newPreset)
      setNewPreset(EMPTY_PRESET)
      setAddVersion((version) => version + 1)
    })
  }

  function move(index: number, offset: number) {
    const ids = list.map((block) => block.id)
    const [moved] = ids.splice(index, 1)
    ids.splice(index + offset, 0, moved)
    void run(() => api.reorderBuildingBlocks(ids))
  }

  return (
    <section className="panel" id="presets">
      <h3>Presets</h3>
      <p className="hint">
        Shown above the building blocks. Clicking one applies it: it replaces the Art style box and sets Resolution and
        Images. Empty fields leave that part as it is.
      </p>
      <div className="editable-list">
        {list.map((preset, index) => (
          <PresetRow
            key={`${preset.id}-${preset.label}-${preset.artStyle}-${preset.resolution}-${preset.imageCount}`}
            preset={preset}
            resolutions={resolutions.data ?? []}
            canMoveUp={index > 0}
            canMoveDown={index < list.length - 1}
            onSave={(fields) => void run(() => api.updatePreset(preset.id, fields))}
            onDelete={() => void run(() => api.deleteBuildingBlock(preset.id))}
            onMove={(offset) => move(index, offset)}
          />
        ))}
      </div>
      <form className="editable-row preset-row" onSubmit={(e) => void add(e)}>
        <PresetInputs key={addVersion} fields={newPreset} resolutions={resolutions.data ?? []} onChange={setNewPreset} />
        <div className="row-actions">
          <button type="submit" className="primary" disabled={!newPreset.label.trim()}>
            Add
          </button>
        </div>
      </form>
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
}

function PresetRow({ preset, resolutions, canMoveUp, canMoveDown, onSave, onDelete, onMove }: RowProps) {
  const saved: PresetFields = {
    label: preset.label,
    artStyle: preset.artStyle,
    resolution: preset.resolution,
    imageCount: preset.imageCount,
  }
  const [fields, setFields] = useState(saved)
  const changed = JSON.stringify(fields) !== JSON.stringify(saved)

  return (
    <div className="editable-row preset-row">
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

  return (
    <>
      <input placeholder="Label" value={fields.label} onChange={(e) => change({ label: e.target.value })} />
      <textarea
        rows={2}
        placeholder="Art style (empty keeps the box as it is)"
        value={fields.artStyle ?? ''}
        onChange={(e) => change({ artStyle: e.target.value || null })}
      />
      <select
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
