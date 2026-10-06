import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RepeatIcon } from './RepeatIcon'

describe('RepeatIcon', () => {
  it('is a decorative shape hidden from screen readers (the name says "repeats" instead)', () => {
    const { container } = render(<RepeatIcon />)

    const svg = container.querySelector('svg')!
    expect(svg).toHaveAttribute('aria-hidden', 'true')
    expect(svg).toHaveClass('repeat-icon')
    expect(svg.getAttribute('focusable')).toBe('false')
  })
})
