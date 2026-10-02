import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useSwipe } from './useSwipe'

interface Gesture {
  dx: number
  dy?: number
  ms?: number
  pointerType?: string
  target?: string
}

function Harness({ onPrevious, onNext, now }: { onPrevious: () => void; onNext: () => void; now: () => number }) {
  const swipe = useSwipe(onPrevious, onNext, now)
  return (
    <div data-testid="body" {...swipe.handlers} style={swipe.style}>
      <input aria-label="Field" />
      <button type="button" onClick={onClickSpy}>
        Day
      </button>
    </div>
  )
}

const onClickSpy = vi.fn()

function setup() {
  let time = 0
  const onPrevious = vi.fn()
  const onNext = vi.fn()
  render(<Harness onPrevious={onPrevious} onNext={onNext} now={() => time} />)

  const swipe = ({ dx, dy = 0, ms = 300, pointerType = 'touch', target = 'body' }: Gesture) => {
    const element = target === 'field' ? screen.getByLabelText('Field') : target === 'button' ? screen.getByRole('button') : screen.getByTestId('body')
    time = 0
    fireEvent.pointerDown(element, { pointerId: 1, pointerType, clientX: 200, clientY: 200 })
    fireEvent.pointerMove(element, { pointerId: 1, pointerType, clientX: 200 + dx / 2, clientY: 200 + dy / 2 })
    time = ms
    fireEvent.pointerUp(element, { pointerId: 1, pointerType, clientX: 200 + dx, clientY: 200 + dy })
    return element
  }

  return { onPrevious, onNext, swipe }
}

// Horizontal touch swipes change the period (FR-008a, research V7).
describe('useSwipe', () => {
  it('goes to the next period on a left swipe and the previous one on a right swipe', () => {
    const { onPrevious, onNext, swipe } = setup()

    swipe({ dx: -60, dy: 10 })
    expect(onNext).toHaveBeenCalledOnce()

    swipe({ dx: 60 })
    expect(onPrevious).toHaveBeenCalledOnce()
  })

  it('ignores short, steep, slow and non-touch gestures', () => {
    const { onPrevious, onNext, swipe } = setup()

    swipe({ dx: -40 })
    swipe({ dx: -60, dy: 40 })
    swipe({ dx: -60, ms: 800 })
    swipe({ dx: -60, pointerType: 'mouse' })

    expect(onNext).not.toHaveBeenCalled()
    expect(onPrevious).not.toHaveBeenCalled()
  })

  it('ignores gestures that start in a text field', () => {
    const { onNext, swipe } = setup()

    swipe({ dx: -60, target: 'field' })

    expect(onNext).not.toHaveBeenCalled()
  })

  it('stops the click that follows a swipe, so ending on a day does not open it', () => {
    const { onNext, swipe } = setup()
    onClickSpy.mockClear()

    const button = swipe({ dx: -60, target: 'button' })
    fireEvent.click(button)

    expect(onNext).toHaveBeenCalledOnce()
    expect(onClickSpy).not.toHaveBeenCalled()

    fireEvent.click(button)
    expect(onClickSpy).toHaveBeenCalledOnce()
  })

  it('lets the browser handle vertical scrolling', () => {
    setup()

    expect(screen.getByTestId('body').style.touchAction).toBe('pan-y')
  })
})
