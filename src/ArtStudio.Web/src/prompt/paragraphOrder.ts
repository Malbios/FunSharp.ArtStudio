import { movedItem } from '../sets/useDragReorder'
import { toParagraphs } from './paragraphs'

export type Section = 'Characters' | 'Setting' | 'Composition' | 'Style'

export const NORMAL_ORDER: Section[] = ['Characters', 'Setting', 'Composition', 'Style']

export const SECTION_LABELS: Record<Section, string> = {
  Characters: 'Characters',
  Setting: 'Setting',
  Composition: 'Composition',
  Style: 'Art style',
}

/** The order as the server stores it; null (or anything unexpected) is the normal order. */
export function parseOrder(order: string | null | undefined): Section[] {
  const sections = order?.split(',') ?? []
  const isFullOrder =
    sections.length === NORMAL_ORDER.length && NORMAL_ORDER.every((section) => sections.includes(section))
  return isFullOrder ? (sections as Section[]) : NORMAL_ORDER
}

export const serializeOrder = (order: Section[]): string => order.join(',')

export const isNormalOrder = (order: Section[]): boolean =>
  order.every((section, index) => section === NORMAL_ORDER[index])

export function moveSection(order: Section[], index: number, offset: number): Section[] {
  const target = index + offset
  return target < 0 || target >= order.length ? order : movedItem(order, index, target)
}

/** "Art style → Characters → Setting → Composition", or null for the normal order, which needs no label. */
export function orderLabel(order: string | null | undefined): string | null {
  const sections = parseOrder(order)
  return isNormalOrder(sections) ? null : sections.map((section) => SECTION_LABELS[section]).join(' → ')
}

/** Only prompts with Setting, Composition and Art style paragraphs can be reordered; others are sent as written. */
export const canReorder = (prompt: string): boolean => toParagraphs(prompt).length >= 3
