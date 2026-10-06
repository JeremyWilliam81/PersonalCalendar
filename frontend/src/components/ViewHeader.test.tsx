import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import type { ViewState } from '../lib/viewState'
import { ViewHeader } from './ViewHeader'

function renderHeader(state: ViewState, overrides: Partial<Parameters<typeof ViewHeader>[0]> = {}) {
  const props = {
    state,
    titleId: 'title',
    canGoPrevious: true,
    canGoNext: true,
    onPrevious: vi.fn(),
    onNext: vi.fn(),
    onToday: vi.fn(),
    onNewEvent: vi.fn(),
    onChangeView: vi.fn(),
    ...overrides,
  }
  return { props, ...render(<ViewHeader {...props} />) }
}

describe('ViewHeader', () => {
  it('shows the period title as a heading and names the navigation for the view', () => {
    renderHeader({ view: 'week', date: '2026-10-14' })

    expect(screen.getByRole('heading', { level: 2, name: 'October 11 – 17, 2026' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous week' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next week' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Today' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New event' })).toBeInTheDocument()
  })

  it('calls the navigation handlers and disables unusable controls (FR-012)', async () => {
    const user = userEvent.setup()
    const { props } = renderHeader({ view: 'day', date: '1900-01-01' }, { canGoPrevious: false })

    expect(screen.getByRole('button', { name: 'Previous day' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Next day' }))
    await user.click(screen.getByRole('button', { name: 'Today' }))
    await user.click(screen.getByRole('button', { name: 'New event' }))
    expect(props.onNext).toHaveBeenCalledOnce()
    expect(props.onToday).toHaveBeenCalledOnce()
    expect(props.onNewEvent).toHaveBeenCalledOnce()
  })

  it('has no axe violations', async () => {
    const { container } = renderHeader({ view: 'month', date: '2026-10-14' })
    expect((await axe(container)).violations).toEqual([])
  })
})

describe('ViewHeader view switcher', () => {
  it('shows Day, Week and Month toggle buttons with the active one pressed', async () => {
    const user = userEvent.setup()
    const { props } = renderHeader({ view: 'week', date: '2026-10-14' })

    const group = screen.getByRole('group', { name: 'View' })
    expect(group).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Week' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Day' })).toHaveAttribute('aria-pressed', 'false')

    await user.click(screen.getByRole('button', { name: 'Day' }))
    expect(props.onChangeView).toHaveBeenCalledWith('day')
  })
})
