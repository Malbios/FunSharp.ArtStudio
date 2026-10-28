import { isNormalOrder, moveSection, NORMAL_ORDER, SECTION_LABELS, type Section } from '../prompt/paragraphOrder'
import { movedItem, useDragReorder } from '../sets/useDragReorder'

interface Props {
  order: Section[]
  onChange: (order: Section[]) => void
  /** Why the order can't apply to the prompt as it is, if it can't. */
  disabledReason?: string
}

/** The order a run sends the prompt's sections in: drag a chip, or move it with its arrows. */
export function ParagraphOrderChips({ order, onChange, disabledReason }: Props) {
  const disabled = disabledReason !== undefined
  const drag = useDragReorder((from, to) => onChange(movedItem(order, from, to)))

  return (
    <div className={disabled ? 'order-chips disabled' : 'order-chips'} title={disabledReason}>
      {order.map((section, index) => (
        <span
          key={section}
          className={['order-chip', disabled ? '' : drag.itemClass(index)].filter(Boolean).join(' ')}
          {...(disabled ? {} : drag.itemProps(index))}
        >
          <button
            type="button"
            className="link-button"
            disabled={disabled || index === 0}
            aria-label={`Move ${SECTION_LABELS[section]} earlier`}
            onClick={() => onChange(moveSection(order, index, -1))}
          >
            ‹
          </button>
          {index + 1}. {SECTION_LABELS[section]}
          <button
            type="button"
            className="link-button"
            disabled={disabled || index === order.length - 1}
            aria-label={`Move ${SECTION_LABELS[section]} later`}
            onClick={() => onChange(moveSection(order, index, 1))}
          >
            ›
          </button>
        </span>
      ))}
      {!disabled && !isNormalOrder(order) && (
        <button type="button" className="link-button" onClick={() => onChange(NORMAL_ORDER)}>
          ↺ Normal order
        </button>
      )}
    </div>
  )
}
