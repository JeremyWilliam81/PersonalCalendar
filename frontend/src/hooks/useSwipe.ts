import { useRef, type CSSProperties, type MouseEvent, type PointerEvent } from 'react'

// Touch swipes change the period (FR-008a, research V7). Buttons stay the single-pointer alternative.
const MIN_DISTANCE_PX = 48
const MAX_DURATION_MS = 700
const IGNORED_TARGETS = 'input, textarea, select, dialog'

interface Start {
  x: number
  y: number
  at: number
}

/** `now` is injectable so tests control the gesture's duration. */
export function useSwipe(onPrevious: () => void, onNext: () => void, now: () => number = () => performance.now()) {
  const start = useRef<Start | null>(null)
  const swallowClick = useRef(false)

  const onPointerDown = (event: PointerEvent<HTMLElement>) => {
    const ignored = event.pointerType !== 'touch' || (event.target as HTMLElement).closest(IGNORED_TARGETS)
    start.current = ignored ? null : { x: event.clientX, y: event.clientY, at: now() }
  }

  const onPointerUp = (event: PointerEvent<HTMLElement>) => {
    const from = start.current
    start.current = null
    if (!from || event.pointerType !== 'touch') return
    const dx = event.clientX - from.x
    const dy = event.clientY - from.y
    if (Math.abs(dx) < MIN_DISTANCE_PX || Math.abs(dx) < 2 * Math.abs(dy) || now() - from.at > MAX_DURATION_MS) return
    // A swipe that ends on a day or an empty time slot must not also tap it.
    swallowClick.current = true
    if (dx < 0) onNext()
    else onPrevious()
  }

  const onClickCapture = (event: MouseEvent<HTMLElement>) => {
    if (!swallowClick.current) return
    swallowClick.current = false
    event.stopPropagation()
    event.preventDefault()
  }

  const onPointerCancel = () => {
    start.current = null
  }

  const style: CSSProperties = { touchAction: 'pan-y' }
  return { handlers: { onPointerDown, onPointerUp, onPointerCancel, onClickCapture }, style }
}
