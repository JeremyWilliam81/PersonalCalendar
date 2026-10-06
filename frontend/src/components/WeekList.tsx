import { useEffect, useRef, type KeyboardEvent } from 'react'
import type { DaysView, EventSummary } from '../api/types'
import { addDays } from '../lib/dates'
import { describeDay, describeEvent, eventKey } from '../lib/describe'
import { formatDayHeading, formatFullDate, formatTimeShort } from '../lib/format'
import { RepeatIcon } from './RepeatIcon'

interface WeekListProps {
  week: DaysView
  selectedDate: string
  /** `leavesPeriod` is true when the date is outside this week, so the neighboring week must load. */
  onMoveDate: (date: string, leavesPeriod: boolean) => void
  onOpenDay: (date: string) => void
  onOpenEvent: (event: EventSummary, date: string) => void
}

function EventRow({ event, timeZone, onOpen }: { event: EventSummary; timeZone: string; onOpen: () => void }) {
  return (
    <button type="button" className={event.isAllDay ? 'list-event all-day' : 'list-event'} aria-label={describeEvent(event, timeZone)} onClick={onOpen}>
      <span className="list-event-time">
        {event.isAllDay || !event.start || !event.end
          ? 'All day'
          : `${formatTimeShort(event.start, timeZone)} – ${formatTimeShort(event.end, timeZone)}`}
      </span>
      <span className="list-event-title">
        {event.title}
        {event.isRecurring && <RepeatIcon />}
      </span>
    </button>
  )
}

/** The week on phone-sized screens: seven stacked day sections (FR-004a). */
export function WeekList({ week, selectedDate, onMoveDate, onOpenDay, onOpenEvent }: WeekListProps) {
  const listRef = useRef<HTMLDivElement>(null)
  const moveFocus = useRef(false)
  const dates = week.days.map((d) => d.date)
  const firstDate = dates[0]

  useEffect(() => {
    const target = week.days.find((d) => d.isToday)?.date ?? selectedDate
    listRef.current?.querySelector(`[data-date="${target}"]`)?.scrollIntoView?.({ block: 'start' })
    // Only when a new week is shown.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firstDate])

  useEffect(() => {
    if (!moveFocus.current) return
    const target = listRef.current?.querySelector<HTMLElement>(`button[data-date="${selectedDate}"]`)
    if (target) {
      target.focus()
      moveFocus.current = false
    }
  }, [selectedDate, week])

  const handleHeadingKeyDown = (event: KeyboardEvent<HTMLButtonElement>, date: string) => {
    const delta = event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1 : event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1 : 0
    if (delta === 0) return
    event.preventDefault()
    const target = addDays(date, delta)
    const leavesPeriod = !dates.includes(target)
    if (leavesPeriod) moveFocus.current = true
    else listRef.current?.querySelector<HTMLElement>(`button[data-date="${target}"]`)?.focus()
    onMoveDate(target, leavesPeriod)
  }

  return (
    <div className="week-list" ref={listRef}>
      {week.days.map((day) => {
        const events = [...day.allDay, ...day.timed.map((s) => s.event)]
        return (
          <section key={day.date} aria-label={formatFullDate(day.date)} className="week-list-day">
            <h3>
              <button
                type="button"
                className="week-list-heading"
                data-date={day.date}
                aria-current={day.isToday ? 'date' : undefined}
                aria-label={describeDay(day.date, { isToday: day.isToday })}
                onClick={() => onOpenDay(day.date)}
                onKeyDown={(event) => handleHeadingKeyDown(event, day.date)}
              >
                <span className="day-number">{Number(day.date.slice(8, 10))}</span>
                <span>{formatDayHeading(day.date)}</span>
              </button>
            </h3>
            {events.length === 0 ? (
              <p className="no-events">No events</p>
            ) : (
              <ul className="week-list-events">
                {events.map((event) => (
                  <li key={eventKey(event)}>
                    <EventRow event={event} timeZone={week.timeZone} onOpen={() => onOpenEvent(event, day.date)} />
                  </li>
                ))}
              </ul>
            )}
          </section>
        )
      })}
    </div>
  )
}
