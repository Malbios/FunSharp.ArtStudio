import { describe, expect, it } from 'vitest'
import { generateActionLabel } from './generateAction'

describe('generateActionLabel', () => {
  it('offers to generate a first prompt or a new one', () => {
    expect(generateActionLabel('None', false)).toBe('Generate prompt')
    expect(generateActionLabel('None', true)).toBe('Generate new prompt')
    expect(generateActionLabel('Done', true)).toBe('Generate new prompt')
  })

  it('offers a retry after a failure', () => {
    expect(generateActionLabel('Failed', false)).toBe('Retry generating')
  })

  it('offers nothing while waiting or generating', () => {
    expect(generateActionLabel('Queued', false)).toBeUndefined()
    expect(generateActionLabel('Running', true)).toBeUndefined()
  })
})
