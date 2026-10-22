const compactNumber = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 })

export const formatCompact = (value: number) => compactNumber.format(value)

export const formatCount = (value: number | null) => (value === null ? '–' : value.toLocaleString())

export const formatDecimal = (value: number | null, digits = 1) =>
  value === null ? '–' : value.toLocaleString(undefined, { maximumFractionDigits: digits })

export const formatPercent = (fraction: number | null) =>
  fraction === null ? '–' : fraction.toLocaleString(undefined, { style: 'percent', maximumFractionDigits: 1 })

export const formatSigned = (value: number | null) => (value === null ? '–' : `${value > 0 ? '+' : ''}${value.toLocaleString()}`)

/** Hours while under a day, whole days after that. */
export const formatAge = (days: number) => (days < 1 ? `${Math.floor(days * 24)} h` : `${Math.floor(days)} d`)

/** Days come as yyyy-mm-dd in the studio's own time zone. */
export const formatDay = (day: string, withWeekday = false) =>
  new Date(`${day}T00:00`).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    ...(withWeekday ? { weekday: 'short' } : {}),
  })
