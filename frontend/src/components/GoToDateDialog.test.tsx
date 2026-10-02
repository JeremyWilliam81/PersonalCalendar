import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { GoToDateDialog } from './GoToDateDialog'

function renderDialog() {
  const props = { initialDate: '2026-10-14', onGo: vi.fn(), onClose: vi.fn() }
  return { props, ...render(<GoToDateDialog {...props} />) }
}

function field() {
  return screen.getByLabelText('Date') as HTMLInputElement
}

// contracts/ui-interaction "Go to date dialog" (FR-011, research V8).
describe('GoToDateDialog', () => {
  it('offers a native date field limited to the supported range, starting at the selected date', () => {
    renderDialog()

    expect(screen.getByRole('dialog', { name: 'Go to date' })).toBeInTheDocument()
    expect(field()).toHaveAttribute('type', 'date')
    expect(field()).toHaveAttribute('min', '1900-01-01')
    expect(field()).toHaveAttribute('max', '2199-12-31')
    expect(field()).toHaveValue('2026-10-14')
  })

  it('goes to a valid date, including Feb 29', async () => {
    const user = userEvent.setup()
    const { props } = renderDialog()

    fireEvent.change(field(), { target: { value: '2028-02-29' } })
    await user.click(screen.getByRole('button', { name: 'Go' }))

    expect(props.onGo).toHaveBeenCalledWith('2028-02-29')
  })

  it('rejects an empty or unfinished date without going anywhere', async () => {
    const user = userEvent.setup()
    const { props } = renderDialog()

    fireEvent.change(field(), { target: { value: '' } })
    await user.click(screen.getByRole('button', { name: 'Go' }))

    expect(props.onGo).not.toHaveBeenCalled()
    expect(field()).toHaveAttribute('aria-invalid', 'true')
    expect(field()).toHaveAccessibleDescription('Enter a valid date.')
  })

  it('rejects a date outside 1900–2199', async () => {
    const user = userEvent.setup()
    const { props } = renderDialog()

    fireEvent.change(field(), { target: { value: '2200-01-01' } })
    await user.click(screen.getByRole('button', { name: 'Go' }))

    expect(props.onGo).not.toHaveBeenCalled()
    expect(field()).toHaveAccessibleDescription('Choose a date between 1900 and 2199.')
  })

  it('closes with Cancel or Escape', async () => {
    const user = userEvent.setup()
    const { props } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    await user.keyboard('{Escape}')

    expect(props.onClose).toHaveBeenCalledTimes(2)
  })

  it('has no axe violations', async () => {
    const { container } = renderDialog()

    expect((await axe(container)).violations).toEqual([])
  })
})
