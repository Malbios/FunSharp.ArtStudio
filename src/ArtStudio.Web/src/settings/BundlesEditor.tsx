import { useState, type FormEvent } from 'react'
import { api, type BuildingBlock } from '../api'
import type { Loaded } from '../live/useLoad'
import { movedItem } from '../sets/useDragReorder'

interface BundleFields {
  label: string
  presetIds: number[]
}

const EMPTY_BUNDLE: BundleFields = { label: '', presetIds: [] }

export function BundlesEditor({ blocks }: { blocks: Loaded<BuildingBlock[]> }) {
  const [addingAt, setAddingAt] = useState<number>()
  const [newBundle, setNewBundle] = useState(EMPTY_BUNDLE)
  const [error, setError] = useState<string>()
  const presets = blocks.data?.filter((block) => block.kind === 'Preset') ?? []
  const list = blocks.data?.filter((block) => block.kind === 'Bundle') ?? []

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
    setNewBundle(EMPTY_BUNDLE)
    setAddingAt(position)
  }

  async function add(event: FormEvent, position: number) {
    event.preventDefault()
    await run(async () => {
      const created = await api.addBundle(newBundle.label, newBundle.presetIds)
      const ids = list.map((block) => block.id)
      ids.splice(position, 0, created.id)
      await api.reorderBuildingBlocks(ids)
      setAddingAt(undefined)
    })
  }

  function move(index: number, offset: number) {
    const ids = list.map((block) => block.id)
    void run(() => api.reorderBuildingBlocks(movedItem(ids, index, index + offset)))
  }

  const insertPoint = (position: number) =>
    addingAt === position ? (
      <form key={`new-${position}`} className="editable-row bundle-row new-entry" onSubmit={(e) => void add(e, position)}>
        <BundleInputs fields={newBundle} presets={presets} onChange={setNewBundle} />
        <div className="row-actions">
          <button type="button" onClick={() => setAddingAt(undefined)}>
            Cancel
          </button>
          <button
            type="submit"
            className="primary"
            disabled={!newBundle.label.trim() || newBundle.presetIds.length === 0}
          >
            Add
          </button>
        </div>
      </form>
    ) : (
      <button key={`insert-${position}`} type="button" className="link-button add-here" onClick={() => startAdding(position)}>
        {list.length === 0 ? '+ Add bundle' : '+ Add bundle here'}
      </button>
    )

  return (
    <section className="panel" id="bundles">
      <h3>Preset bundles</h3>
      <p className="hint">
        Shown first in Requeue with preset(s). Clicking a bundle queues one job per preset in it, in this order.
      </p>
      {presets.length === 0 ? (
        <p className="hint">Add some presets first.</p>
      ) : (
        <div className="editable-list">
          {insertPoint(0)}
          {list.map((bundle, index) => [
            <BundleRow
              key={`${bundle.id}-${bundle.label}-${bundle.presetIds.join(',')}`}
              bundle={bundle}
              presets={presets}
              canMoveUp={index > 0}
              canMoveDown={index < list.length - 1}
              onSave={(fields) => void run(() => api.updateBundle(bundle.id, fields.label, fields.presetIds))}
              onDelete={() => void run(() => api.deleteBuildingBlock(bundle.id))}
              onMove={(offset) => move(index, offset)}
            />,
            insertPoint(index + 1),
          ])}
        </div>
      )}
      {(error ?? blocks.error) && <p className="error">{error ?? blocks.error}</p>}
    </section>
  )
}

interface RowProps {
  bundle: BuildingBlock
  presets: BuildingBlock[]
  canMoveUp: boolean
  canMoveDown: boolean
  onSave: (fields: BundleFields) => void
  onDelete: () => void
  onMove: (offset: number) => void
}

function BundleRow({ bundle, presets, canMoveUp, canMoveDown, onSave, onDelete, onMove }: RowProps) {
  const saved: BundleFields = { label: bundle.label, presetIds: bundle.presetIds }
  const [fields, setFields] = useState(saved)
  const changed = JSON.stringify(fields) !== JSON.stringify(saved)

  return (
    <div className="editable-row bundle-row">
      <BundleInputs fields={fields} presets={presets} onChange={setFields} />
      <div className="row-actions">
        <button type="button" disabled={!canMoveUp} onClick={() => onMove(-1)} title="Move up">
          ↑
        </button>
        <button type="button" disabled={!canMoveDown} onClick={() => onMove(1)} title="Move down">
          ↓
        </button>
        <button
          type="button"
          className="primary"
          disabled={!changed || fields.presetIds.length === 0}
          onClick={() => onSave(fields)}
        >
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
  fields: BundleFields
  presets: BuildingBlock[]
  onChange: (fields: BundleFields) => void
}

function BundleInputs({ fields, presets, onChange }: InputsProps) {
  const presetsById = new Map(presets.map((preset) => [preset.id, preset]))
  const included = fields.presetIds.filter((id) => presetsById.has(id))
  const available = presets.filter((preset) => !included.includes(preset.id))
  const setIds = (presetIds: number[]) => onChange({ ...fields, presetIds })

  return (
    <>
      <input placeholder="Label" value={fields.label} onChange={(e) => onChange({ ...fields, label: e.target.value })} />
      <div className="bundle-presets">
        {included.map((id, index) => (
          <span key={id} className="bundle-preset">
            <button
              type="button"
              className="link-button"
              disabled={index === 0}
              title="Move earlier"
              onClick={() => setIds(movedItem(included, index, index - 1))}
            >
              ←
            </button>
            {presetsById.get(id)!.label}
            <button
              type="button"
              className="link-button"
              disabled={index === included.length - 1}
              title="Move later"
              onClick={() => setIds(movedItem(included, index, index + 1))}
            >
              →
            </button>
            <button
              type="button"
              className="link-button"
              title="Remove from bundle"
              onClick={() => setIds(included.filter((other) => other !== id))}
            >
              ×
            </button>
          </span>
        ))}
        {available.length > 0 && (
          <select
            aria-label="Add preset"
            value=""
            onChange={(e) => e.target.value && setIds([...included, Number(e.target.value)])}
          >
            <option value="">Add preset…</option>
            {available.map((preset) => (
              <option key={preset.id} value={preset.id}>
                {preset.label}
              </option>
            ))}
          </select>
        )}
      </div>
    </>
  )
}
