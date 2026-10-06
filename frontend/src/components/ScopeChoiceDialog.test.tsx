import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import type { EditScope } from '../api/types'
import { scopeChoices } from '../lib/scope'
import { ScopeChoiceDialog } from './ScopeChoiceDialog'

// contracts/ui-interaction.md "Scope choice": the Situation → Choices shown table.
describe('scopeChoices', () => {
  it.each<[string, Parameters<typeof scopeChoices>[0], EditScope[] | null]>([
    ['delete', { mode: 'delete', dateChanged: false, repeatChanged: false }, ['this', 'following', 'all']],
    ['edit: text or times', { mode: 'edit', dateChanged: false, repeatChanged: false }, ['this', 'following', 'all']],
    ['edit: repeat changed', { mode: 'edit', dateChanged: false, repeatChanged: true }, ['following', 'all']],
    ['edit: date changed', { mode: 'edit', dateChanged: true, repeatChanged: false }, ['this']],
    ['edit: date and repeat changed', { mode: 'edit', dateChanged: true, repeatChanged: true }, null],
  ])('%s', (_name, situation, expected) => {
    expect(scopeChoices(situation)).toEqual(expected)
  })
})

function Opener({
  choices = ['this', 'following', 'all'] as EditScope[],
  mode = 'edit' as const,
  warning,
  onChoose = vi.fn(),
  onCancel = vi.fn(),
}: {
  choices?: EditScope[]
  mode?: 'edit' | 'delete'
  warning?: (scope: EditScope) => string | null
  onChoose?: (scope: EditScope) => void
  onCancel?: () => void
}) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        Save
      </button>
      {open && (
        <ScopeChoiceDialog
          mode={mode}
          choices={choices}
          warning={warning}
          onChoose={(scope) => {
            setOpen(false)
            onChoose(scope)
          }}
          onCancel={() => {
            setOpen(false)
            onCancel()
          }}
        />
      )}
    </>
  )
}

describe('ScopeChoiceDialog', () => {
  it('offers the given choices as full-width buttons and focuses the first', async () => {
    const user = userEvent.setup()
    render(<Opener choices={['following', 'all']} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('dialog', { name: 'Change recurring event' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'This event' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'This and following events' })).toHaveFocus()
    for (const name of ['This and following events', 'All events']) {
      expect(screen.getByRole('button', { name })).toHaveClass('scope-choice')
    }
  })

  it('reports the choice and returns focus to the opener', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<Opener onChoose={onChoose} />)
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await user.click(screen.getByRole('button', { name: 'This event' }))

    expect(onChoose).toHaveBeenCalledWith('this')
    expect(screen.getByRole('button', { name: 'Save' })).toHaveFocus()
  })

  it('cancels with the Cancel button and with Escape', async () => {
    const user = userEvent.setup()
    const onCancel = vi.fn()
    render(<Opener onCancel={onCancel} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.keyboard('{Escape}')

    expect(onCancel).toHaveBeenCalledTimes(2)
  })

  it('shows a warning step before a choice that loses changes, with Back', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(
      <Opener
        onChoose={onChoose}
        warning={(scope) => (scope === 'all' ? 'Changes you made to single events in this series will be lost.' : null)}
      />,
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await user.click(screen.getByRole('button', { name: 'All events' }))
    expect(screen.getByText('Changes you made to single events in this series will be lost.')).toBeInTheDocument()
    expect(onChoose).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByRole('button', { name: 'All events' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'All events' }))
    await user.click(screen.getByRole('button', { name: 'Save anyway' }))
    expect(onChoose).toHaveBeenCalledWith('all')
  })

  it('is titled for deleting, with "Delete anyway" on its warning step', async () => {
    const user = userEvent.setup()
    render(<Opener mode="delete" warning={() => 'This can’t be undone.'} />)
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('dialog', { name: 'Delete recurring event' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'This event' }))
    expect(screen.getByRole('button', { name: 'Delete anyway' })).toBeInTheDocument()
  })

  it('has no axe violations in either step', async () => {
    const user = userEvent.setup()
    const { container } = render(<Opener warning={() => 'Changes will be lost.'} />)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect((await axe(container)).violations).toEqual([])

    await user.click(screen.getByRole('button', { name: 'All events' }))
    expect((await axe(container)).violations).toEqual([])
  })

  it('has no axe violations in delete mode', async () => {
    const user = userEvent.setup()
    const { container } = render(<Opener mode="delete" />)
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect((await axe(container)).violations).toEqual([])
  })
})
