import type { QueueMove } from '../api'

export const MOVES: { move: QueueMove; label: string; title: string; earlier: boolean }[] = [
  { move: 'Top', label: '⤒ Top', title: 'Run next among the waiting ones of its kind', earlier: true },
  { move: 'Up', label: '↑ Up', title: 'Move one place earlier among the waiting ones of its kind', earlier: true },
  { move: 'Down', label: '↓ Down', title: 'Move one place later among the waiting ones of its kind', earlier: false },
  { move: 'Bottom', label: '⤓ Bottom', title: 'Move after all waiting ones of its kind', earlier: false },
]
