import { describe, expect, it } from 'vitest'
import { generateActionLabel } from './generateAction'

const generate = (state: 'None' | 'Queued' | 'Running' | 'Done' | 'Failed') => ({ state, kind: 'Generate' as const })

describe('generateActionLabel', () => {
  it('offers to generate a first prompt or a new one', () => {
    expect(generateActionLabel(generate('None'), false)).toBe('Generate prompt')
    expect(generateActionLabel(generate('None'), true)).toBe('Generate new prompt')
    expect(generateActionLabel(generate('Done'), true)).toBe('Generate new prompt')
  })

  it('offers a retry after a failed generation', () => {
    expect(generateActionLabel(generate('Failed'), false)).toBe('Retry generating')
  })

  it('offers a fresh generation after a failed modification', () => {
    expect(generateActionLabel({ state: 'Failed', kind: 'Modify' }, true)).toBe('Generate new prompt')
  })

  it('offers nothing while waiting or generating', () => {
    expect(generateActionLabel(generate('Queued'), false)).toBeUndefined()
    expect(generateActionLabel({ state: 'Running', kind: 'Modify' }, true)).toBeUndefined()
  })
})
