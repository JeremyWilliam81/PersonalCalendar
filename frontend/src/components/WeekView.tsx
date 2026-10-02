import { useEffect, useRef, type KeyboardEvent } from 'react'
import type { DaysView } from '../api/types'
import { addDays } from '../lib/dates'
import { describeDay } from '../lib/describe'
import { formatWeekdayName } from '../lib/format'
import { AllDayBars } from './AllDayBars'
import { TimeGrid } from './TimeGrid'

interface WeekViewProps {
  week: DaysView
  /** Id of the period heading in the shared header, which names the day headings. */
  titleId: string
  selectedDate: string
  /** `leavesPeriod` is true when the date is outside this week, so the neighboring week must load. */
  onMoveDate: (date: string, leavesPeriod: boolean) => void
  onOpenDay: (date: string) => void
  onOpenEvent: (id: string, date: string) => void
  onCreateAt?: (localStart: string) => void
  now?: Date
}

/** The wide week view: day headings, all-day bars, and the time grid (contracts/ui-interaction "Week view: wide"). */
export function WeekView({
  week,
  titleId,
  selectedDate,
  onMoveDate,
  onOpenDay,
  onOpenEvent,
  onCreateAt,
  now,
}: WeekViewProps) {
  const headingsRef = useRef<HTMLDivElement>(null)
  const moveFocus = useRef(false)
  const dates = week.days.map((d) => d.date)

  useEffect(() => {
    if (!moveFocus.current) return
    const target = headingsRef.current?.querySelector<HTMLElement>(`[data-date="${selectedDate}"]`)
    if (target) {
      target.focus()
      moveFocus.current = false
    }
  }, [selectedDate, week])

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const date = (event.target as HTMLElement).getAttribute('data-date')
    if (!date) return
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      onOpenDay(date)
      return
    }
    const delta = event.key === 'ArrowLeft' ? -1 : event.key === 'ArrowRight' ? 1 : 0
    if (delta === 0) return
    event.preventDefault()
    const target = addDays(date, delta)
    moveFocus.current = true
    onMoveDate(target, !dates.includes(target))
  }

  return (
    <section className="week" aria-labelledby={titleId}>
      <div role="grid" aria-labelledby={titleId} className="week-headings" ref={headingsRef} onKeyDown={handleKeyDown}>
        <div role="row" className="week-headings-row">
          {week.days.map((day, index) => (
            <div
              key={day.date}
              role="gridcell"
              className="week-heading"
              data-date={day.date}
              aria-current={day.isToday ? 'date' : undefined}
              aria-label={describeDay(day.date, { isToday: day.isToday })}
              tabIndex={day.date === selectedDate || (!dates.includes(selectedDate) && index === 0) ? 0 : -1}
              onClick={() => onOpenDay(day.date)}
            >
              <span className="week-heading-weekday">{formatWeekdayName(index, 'short')}</span>
              <span className="day-number">{Number(day.date.slice(8, 10))}</span>
            </div>
          ))}
        </div>
      </div>
      <AllDayBars bars={week.allDayBars} timeZone={week.timeZone} dates={dates} onOpenEvent={onOpenEvent} />
      <TimeGrid days={week.days} timeZone={week.timeZone} onOpenEvent={onOpenEvent} onCreateAt={onCreateAt} now={now} />
    </section>
  )
}
