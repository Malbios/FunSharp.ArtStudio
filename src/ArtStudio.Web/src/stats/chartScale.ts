const TICK_COUNT = 3
const CORNER_RADIUS = 4

function niceStep(rough: number): number {
  const power = 10 ** Math.floor(Math.log10(rough))
  const fraction = rough / power
  return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * power
}

/** Round ticks covering the values and zero; counts are whole, so steps are at least 1. */
export function niceTicks(min: number, max: number): number[] {
  const low = Math.min(0, min)
  const high = Math.max(0, max)
  const step = Math.max(1, niceStep((high - low || 1) / TICK_COUNT))
  const ticks = []
  for (let tick = Math.floor(low / step) * step; tick <= Math.ceil(high / step) * step; tick += step) ticks.push(tick)
  return ticks.length > 1 ? ticks : [0, step]
}

/** A bar growing from the baseline, rounded only at its data end. */
export function barPath(x: number, width: number, baseline: number, end: number): string {
  const radius = Math.min(CORNER_RADIUS, Math.abs(baseline - end), width / 2)
  const towardEnd = end < baseline ? 1 : -1
  const corner = end + towardEnd * radius
  return [
    `M ${x} ${baseline}`,
    `L ${x} ${corner}`,
    `Q ${x} ${end} ${x + radius} ${end}`,
    `L ${x + width - radius} ${end}`,
    `Q ${x + width} ${end} ${x + width} ${corner}`,
    `L ${x + width} ${baseline}`,
    'Z',
  ].join(' ')
}
