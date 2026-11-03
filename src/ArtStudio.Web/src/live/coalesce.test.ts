import { describe, expect, it } from 'vitest'
import { coalesce } from './coalesce'

function controlledRuns() {
  const finishers: (() => void)[] = []
  const run = () => new Promise<void>((resolve) => finishers.push(resolve))
  const finishNext = async () => {
    finishers.shift()?.()
    await new Promise((resolve) => setTimeout(resolve))
  }
  return { run, finishers, finishNext }
}

describe('coalesce', () => {
  it('runs right away when idle', () => {
    const { run, finishers } = controlledRuns()
    coalesce(run)()
    expect(finishers).toHaveLength(1)
  })

  it('collapses calls made while running into one more run', async () => {
    const { run, finishers, finishNext } = controlledRuns()
    const reload = coalesce(run)
    for (let i = 0; i < 300; i++) reload()
    expect(finishers).toHaveLength(1)

    await finishNext()
    expect(finishers).toHaveLength(1)
    await finishNext()
    expect(finishers).toHaveLength(0)
  })

  it('runs again after a failed run', async () => {
    let calls = 0
    const reload = coalesce(async () => {
      calls++
      throw new Error('offline')
    })
    reload()
    await new Promise((resolve) => setTimeout(resolve))
    reload()
    await new Promise((resolve) => setTimeout(resolve))
    expect(calls).toBe(2)
  })
})
