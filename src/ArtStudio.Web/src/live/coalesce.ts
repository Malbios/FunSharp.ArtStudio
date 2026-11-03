/**
 * Runs `run` one at a time: calls made while it runs collapse into a single run after it, so a burst of live events
 * costs two loads instead of one per event, and the last run still sees every change.
 */
export function coalesce(run: () => Promise<void>): () => void {
  let running = false
  let runAgain = false

  async function start() {
    running = true
    do {
      runAgain = false
      await run().catch(() => undefined)
    } while (runAgain)
    running = false
  }

  return () => {
    if (running) runAgain = true
    else void start()
  }
}
