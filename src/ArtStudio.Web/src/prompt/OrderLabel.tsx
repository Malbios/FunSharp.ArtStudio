import { orderLabel } from './paragraphOrder'

/** Names a run's paragraph order; the normal order shows nothing. */
export function OrderLabel({ order }: { order: string | null }) {
  const label = orderLabel(order)
  if (!label) return null
  return (
    <span className="hint" title="The order the prompt's paragraphs are sent to ComfyUI in">
      Order: {label}
    </span>
  )
}
