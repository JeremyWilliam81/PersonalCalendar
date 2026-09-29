import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import App from './App'
import { ApiContext } from './api/ApiContext'
import { allDaySummary, octoberMonth, stubApi, timedSummary } from './test/fixtures'

const month = octoberMonth({
  '2026-10-14': [
    allDaySummary('v1', 'Vacation', '2026-10-12', '2026-10-16'),
    timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00'),
  ],
})

function renderApp() {
  const api = stubApi({ getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: month })) })
  return render(
    <ApiContext.Provider value={api}>
      <App />
    </ApiContext.Provider>,
  )
}

describe('App (whole page)', () => {
  it('has no axe violations with the month view, the details dialog and the form dialog open in turn', async () => {
    const user = userEvent.setup()
    const { container } = renderApp()
    await screen.findByRole('grid', { name: 'October 2026' })
    expect((await axe(container)).violations).toEqual([])

    await user.click(screen.getByRole('button', { name: /^Dentist,/ }))
    await screen.findByRole('heading', { name: 'Dentist' })
    expect((await axe(container)).violations).toEqual([])

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    await screen.findByRole('dialog', { name: 'Edit event' })
    expect((await axe(container)).violations).toEqual([])
  })

  it('tabs from the header controls into the grid and then out of it', async () => {
    const user = userEvent.setup()
    renderApp()
    await screen.findByRole('grid', { name: 'October 2026' })

    await user.tab()
    expect(screen.getByRole('button', { name: 'Previous month' })).toHaveFocus()
    await user.tab()
    await user.tab()
    await user.tab()
    expect(screen.getByRole('button', { name: 'New event' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('gridcell', { name: 'Thursday, October 1, 2026' })).toHaveFocus()
    await user.tab()
    expect(document.body).toHaveFocus()
  })
})
