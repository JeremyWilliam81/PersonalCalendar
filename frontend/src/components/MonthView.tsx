import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import type { MonthView as MonthViewData } from '../api/types'
import { useNarrowScreen } from '../hooks/useNarrowScreen'
import { addDays, addMonthsClamped, endOfWeek, isWithin, startOfWeek } from '../lib/dates'
import { formatWeekdayName } from '../lib/format'
import { DayCell } from './DayCell'
import { DayOverflowDialog } from './DayOverflowDialog'

interface MonthViewProps {
  month: MonthViewData
  focusedDate: string
  /** Id of the period heading in the shared header, which names the grid. */
  titleId: string
  /** `changeMonth` is true when the new date needs a different month loaded (Page Up/Down or leaving the grid). */
  onMoveFocus: (date: string, changeMonth: boolean) => void
  /** Tapping a day, or Enter/Space on the focused day, opens it in the day view (FR-013). */
  onOpenDay: (date: string) => void
  /** The day the event was opened from becomes the focused day. */
  onOpenEvent: (id: string, date: string) => void
  maxVisible?: number
  /** Incrementing it moves keyboard focus to the focused day (e.g. after the opened event was deleted). */
  focusRequest?: number
}

const WEEKDAYS = [0, 1, 2, 3, 4, 5, 6]

/** Accessible month grid following the WAI-ARIA APG grid pattern (contracts/ui-interaction.md). */
export function MonthView({
  month,
  focusedDate,
  titleId,
  onMoveFocus,
  onOpenDay,
  onOpenEvent,
  maxVisible = 3,
  focusRequest = 0,
}: MonthViewProps) {
  const gridRef = useRef<HTMLDivElement>(null)
  const narrow = useNarrowScreen()
  const moveFocusIntoGrid = useRef(false)
  const [overflowDate, setOverflowDate] = useState<string | null>(null)

  const days = month.weeks.flatMap((week) => week.days)
  const gridFirst = days[0].date
  const gridLast = days[days.length - 1].date

  useEffect(() => {
    if (!moveFocusIntoGrid.current) return
    const cell = gridRef.current?.querySelector<HTMLElement>(`[role="gridcell"][data-date="${focusedDate}"]`)
    if (cell) {
      cell.focus()
      moveFocusIntoGrid.current = false
    }
  }, [focusedDate, month])

  useEffect(() => {
    if (focusRequest === 0) return
    gridRef.current?.querySelector<HTMLElement>('[role="gridcell"][tabindex="0"]')?.focus()
  }, [focusRequest])

  const move = (target: string, forceMonthChange = false) => {
    moveFocusIntoGrid.current = true
    onMoveFocus(target, forceMonthChange || !isWithin(target, gridFirst, gridLast))
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const date = (event.target as HTMLElement).getAttribute('data-date')
    if (!date || (event.target as HTMLElement).getAttribute('role') !== 'gridcell') return

    const actions: Record<string, () => void> = {
      ArrowLeft: () => move(addDays(date, -1)),
      ArrowRight: () => move(addDays(date, 1)),
      ArrowUp: () => move(addDays(date, -7)),
      ArrowDown: () => move(addDays(date, 7)),
      Home: () => move(startOfWeek(date)),
      End: () => move(endOfWeek(date)),
      PageUp: () => move(addMonthsClamped(date, -1), true),
      PageDown: () => move(addMonthsClamped(date, 1), true),
      Enter: () => onOpenDay(date),
      ' ': () => onOpenDay(date),
    }
    const action = actions[event.key]
    if (!action) return
    event.preventDefault()
    action()
  }

  const overflowDay = overflowDate ? days.find((d) => d.date === overflowDate) : undefined

  return (
    <section className="month" aria-labelledby={titleId}>
      <div role="grid" aria-labelledby={titleId} className="grid" ref={gridRef} onKeyDown={handleKeyDown}>
        <div role="row" className="grid-row weekdays">
          {WEEKDAYS.map((index) => (
            <div role="columnheader" key={index} aria-label={formatWeekdayName(index, 'long')}>
              <span>{formatWeekdayName(index, 'short')}</span>
            </div>
          ))}
        </div>
        {month.weeks.map((week) => (
          <div role="row" className="grid-row" key={week.days[0].date}>
            {week.days.map((day) => (
              <DayCell
                key={day.date}
                day={day}
                timeZone={month.timeZone}
                focused={day.date === focusedDate}
                maxVisible={maxVisible}
                narrow={narrow}
                onOpenDay={onOpenDay}
                onOpenEvent={(id) => onOpenEvent(id, day.date)}
                onShowAll={setOverflowDate}
              />
            ))}
          </div>
        ))}
      </div>

      {overflowDay && (
        <DayOverflowDialog
          date={overflowDay.date}
          events={overflowDay.events}
          timeZone={month.timeZone}
          onOpenEvent={(id) => onOpenEvent(id, overflowDay.date)}
          onClose={() => setOverflowDate(null)}
        />
      )}
    </section>
  )
}
