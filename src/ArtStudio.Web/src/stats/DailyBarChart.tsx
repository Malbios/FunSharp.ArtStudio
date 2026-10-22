import { useRef, useState } from 'react'
import { barPath, niceTicks } from './chartScale'
import { formatCompact, formatDay } from './format'
import { useElementWidth } from './useElementWidth'

export interface DailyValue {
  day: string
  value: number
}

interface DailyBarChartProps {
  title: string
  unit: string
  values: DailyValue[]
}

const INITIAL_WIDTH = 640
const HEIGHT = 180
const PLOT = { top: 10, right: 4, bottom: 22, left: 44 }
const MAX_BAR_WIDTH = 24
const BAR_GAP = 2

export function DailyBarChart({ title, unit, values }: DailyBarChartProps) {
  const [hovered, setHovered] = useState<number>()
  const plotRef = useRef<HTMLDivElement>(null)
  const width = useElementWidth(plotRef, INITIAL_WIDTH)
  const ticks = niceTicks(Math.min(...values.map((v) => v.value)), Math.max(...values.map((v) => v.value)))
  const lowest = ticks[0]
  const highest = ticks[ticks.length - 1]
  const plotHeight = HEIGHT - PLOT.top - PLOT.bottom
  const y = (value: number) => PLOT.top + ((highest - value) / (highest - lowest)) * plotHeight
  const slot = (width - PLOT.left - PLOT.right) / values.length
  const barWidth = Math.min(MAX_BAR_WIDTH, slot - BAR_GAP)
  const barX = (index: number) => PLOT.left + index * slot + (slot - barWidth) / 2
  const labelledDays = [0, Math.floor(values.length / 2), values.length - 1]
  const total = values.reduce((sum, v) => sum + v.value, 0)
  const point = hovered === undefined ? undefined : values[hovered]

  return (
    <figure className="daily-chart">
      <figcaption>
        <h4>{title}</h4>
        <span className="hint">
          {total.toLocaleString()} {unit} in {values.length} days
        </span>
      </figcaption>
      <div className="daily-chart-plot" ref={plotRef}>
        <svg width={width} height={HEIGHT} role="img" aria-label={`${title}: ${total} ${unit} in ${values.length} days`}>
          {ticks.map((tick) => (
            <g key={tick}>
              <line className="chart-grid" x1={PLOT.left} x2={width - PLOT.right} y1={y(tick)} y2={y(tick)} />
              <text className="chart-tick" x={PLOT.left - 6} y={y(tick)} textAnchor="end" dominantBaseline="middle">
                {formatCompact(tick)}
              </text>
            </g>
          ))}
          {values.map((value, index) =>
            value.value === 0 ? null : (
              <path
                key={value.day}
                className={`chart-bar${hovered === index ? ' hovered' : ''}`}
                d={barPath(barX(index), barWidth, y(0), y(value.value))}
              />
            ),
          )}
          {labelledDays.map((index) => (
            <text
              key={index}
              className="chart-tick"
              x={barX(index) + barWidth / 2}
              y={HEIGHT - 6}
              textAnchor={index === 0 ? 'start' : index === values.length - 1 ? 'end' : 'middle'}
            >
              {formatDay(values[index].day)}
            </text>
          ))}
          {values.map((value, index) => (
            <rect
              key={value.day}
              className="chart-hit"
              x={PLOT.left + index * slot}
              y={PLOT.top}
              width={slot}
              height={plotHeight}
              onMouseEnter={() => setHovered(index)}
              onMouseLeave={() => setHovered(undefined)}
            />
          ))}
        </svg>
        {point && hovered !== undefined && (
          <div className="chart-tooltip" style={{ left: barX(hovered) + barWidth / 2 }}>
            <span>{formatDay(point.day, true)}</span>
            <strong>
              {point.value.toLocaleString()} {unit}
            </strong>
          </div>
        )}
      </div>
      <details className="chart-values">
        <summary>Show values</summary>
        <table className="stats-table">
          <tbody>
            {values.map((value) => (
              <tr key={value.day}>
                <td>{formatDay(value.day, true)}</td>
                <td className="number">{value.value.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </figure>
  )
}
