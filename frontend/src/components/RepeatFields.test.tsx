import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import type { FieldErrors, Recurrence } from '../api/types'
import { setNarrowViewport } from '../test/viewport'
import { RepeatFields } from './RepeatFields'

function Harness({
  startDate = '2026-10-06',
  initial = null,
  errors = {},
  onValue,
}: {
  startDate?: string
  initial?: Recurrence | null
  errors?: FieldErrors
  onValue?: (value: Recurrence | null) => void
}) {
  const [value, setValue] = useState<Recurrence | null>(initial)
  const [start, setStart] = useState(startDate)
  return (
    <>
      <label>
        Start date
        <input type="date" value={start} onChange={(e) => setStart(e.target.value)} />
      </label>
      <RepeatFields
        value={value}
        startDate={start}
        errors={errors}
        onChange={(next) => {
          setValue(next)
          onValue?.(next)
        }}
      />
      <output data-testid="value">{JSON.stringify(value)}</output>
    </>
  )
}

function currentValue(): Recurrence | null {
  return JSON.parse(screen.getByTestId('value').textContent ?? 'null') as Recurrence | null
}

describe('RepeatFields', () => {
  it('starts as "Does not repeat"', () => {
    render(<Harness />)

    expect(screen.getByRole('radio', { name: 'Does not repeat' })).toBeChecked()
    expect(screen.queryByRole('group', { name: 'Repeat on' })).not.toBeInTheDocument()
    expect(currentValue()).toBeNull()
  })

  it('weekly: preselects the start weekday and toggles days by tapping', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('radio', { name: 'Weekly' }))

    const days = within(screen.getByRole('group', { name: 'Repeat on' })).getAllByRole('button')
    expect(days.map((d) => d.getAttribute('aria-label'))).toEqual([
      'Sunday',
      'Monday',
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday',
    ])
    expect(screen.getByRole('button', { name: 'Tuesday' })).toHaveAttribute('aria-pressed', 'true')

    await user.click(screen.getByRole('button', { name: 'Friday' }))

    expect(screen.getByRole('button', { name: 'Friday' })).toHaveAttribute('aria-pressed', 'true')
    expect(currentValue()?.weekdays).toEqual(['tuesday', 'friday'])
  })

  it('weekly: clearing every weekday shows an error next to the days', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))

    await user.click(screen.getByRole('button', { name: 'Tuesday' }))

    expect(screen.getByText('Choose at least one weekday.')).toBeInTheDocument()
    expect(currentValue()?.weekdays).toEqual([])
  })

  it('interval stepper stays within 1 to 99 and labels the unit', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))

    await user.click(screen.getByRole('button', { name: 'Fewer' }))
    expect(screen.getByRole('spinbutton', { name: 'Repeat every' })).toHaveValue(1)

    await user.click(screen.getByRole('button', { name: 'More' }))
    expect(screen.getByRole('spinbutton', { name: 'Repeat every' })).toHaveValue(2)
    expect(screen.getByText('weeks')).toBeInTheDocument()
  })

  it('interval stepper stops at 99', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ frequency: 'daily', interval: 99, weekdays: [], monthly: null, end: { type: 'never' } }} />)

    await user.click(screen.getByRole('button', { name: 'More' }))

    expect(screen.getByRole('spinbutton', { name: 'Repeat every' })).toHaveValue(99)
  })

  it('monthly: shows the options for the start date with the day number selected', async () => {
    const user = userEvent.setup()
    render(<Harness startDate="2026-10-14" />)

    await user.click(screen.getByRole('radio', { name: 'Monthly' }))

    const group = screen.getByRole('radiogroup', { name: 'Monthly on' })
    expect(within(group).getByRole('radio', { name: 'On day 14' })).toBeChecked()
    await user.click(within(group).getByRole('radio', { name: 'On the second Wednesday' }))
    expect(currentValue()?.monthly).toEqual({ type: 'weekdayPosition', ordinal: 2 })
  })

  it('monthly: refills the options when the start date changes, keeping the kind of choice', async () => {
    const user = userEvent.setup()
    render(
      <Harness
        startDate="2026-10-14"
        initial={{ frequency: 'monthly', interval: 1, weekdays: [], monthly: { type: 'weekdayPosition', ordinal: 2 }, end: { type: 'never' } }}
      />,
    )

    const start = screen.getByLabelText('Start date')
    await user.clear(start)
    await user.type(start, '2026-10-30')

    expect(screen.getByRole('radio', { name: 'On the last Friday' })).toBeChecked()
    expect(currentValue()?.monthly).toEqual({ type: 'weekdayPosition', ordinal: -1 })
  })

  it('weekly: keeps an explicit weekday choice when the start date changes', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))
    await user.click(screen.getByRole('button', { name: 'Friday' }))

    const start = screen.getByLabelText('Start date')
    await user.clear(start)
    await user.type(start, '2026-10-07')

    expect(currentValue()?.weekdays).toEqual(['tuesday', 'friday'])
  })

  it('ends: after a number of times, or on a date', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Daily' }))
    const ends = screen.getByRole('radiogroup', { name: 'Ends' })

    await user.click(within(ends).getByRole('radio', { name: 'After' }))
    expect(screen.getByRole('spinbutton', { name: 'Number of times' })).toHaveValue(10)
    expect(currentValue()?.end).toEqual({ type: 'count', count: 10 })

    await user.click(within(ends).getByRole('radio', { name: 'On' }))
    expect(screen.getByLabelText('Ends on date')).toBeInTheDocument()
    expect(currentValue()?.end.type).toBe('until')
  })

  it('shows a summary that includes an adjusted first date', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))
    await user.click(screen.getByRole('button', { name: 'Thursday' }))
    await user.click(screen.getByRole('button', { name: 'Tuesday' }))

    expect(screen.getByTestId('repeat-summary')).toHaveTextContent(
      'Weekly on Thursday. Starts Thursday, October 8, 2026.',
    )
  })

  it('shows server errors next to the right control', async () => {
    render(
      <Harness
        initial={{ frequency: 'daily', interval: 1, weekdays: [], monthly: null, end: { type: 'count', count: 1000 } }}
        errors={{ 'recurrence.count': ['recurrence.count.outOfRange'] }}
      />,
    )

    expect(screen.getByRole('spinbutton', { name: 'Number of times' })).toHaveAccessibleDescription('Enter a number from 1 to 999.')
  })

  it('uses arrow keys inside the radio groups and Space on a weekday', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    screen.getByRole('radio', { name: 'Does not repeat' }).focus()
    await user.keyboard('{ArrowRight}')
    expect(screen.getByRole('radio', { name: 'Daily' })).toBeChecked()
    await user.keyboard('{ArrowRight}')
    expect(screen.getByRole('radio', { name: 'Weekly' })).toBeChecked()

    screen.getByRole('button', { name: 'Monday' }).focus()
    await user.keyboard(' ')
    expect(screen.getByRole('button', { name: 'Monday' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('uses touch-sized controls on a narrow screen', async () => {
    setNarrowViewport(true)
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))

    for (const day of within(screen.getByRole('group', { name: 'Repeat on' })).getAllByRole('button')) {
      expect(day).toHaveClass('weekday-toggle')
    }
    expect(screen.getByRole('button', { name: 'More' })).toHaveClass('stepper-button')
    expect(screen.getByRole('radio', { name: 'Weekly' }).closest('label')).toHaveClass('segmented-option')
  })

  it('has no axe violations with every section open', async () => {
    const user = userEvent.setup()
    const { container } = render(<Harness startDate="2026-10-28" />)
    await user.click(screen.getByRole('radio', { name: 'Monthly' }))
    await user.click(within(screen.getByRole('radiogroup', { name: 'Ends' })).getByRole('radio', { name: 'After' }))

    expect((await axe(container)).violations).toEqual([])
  })

  it('has no axe violations in the weekly section with an end date', async () => {
    const user = userEvent.setup()
    const { container } = render(<Harness />)
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))
    await user.click(screen.getByRole('radio', { name: 'On' }))

    expect((await axe(container)).violations).toEqual([])
  })
})
