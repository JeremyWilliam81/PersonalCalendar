import { useEffect, useRef, type KeyboardEvent } from 'react'
import type { DaysView, EventSummary, TimedSegment, TimelineDay } from '../api/types'
import { addDays } from '../lib/dates'
import { describeDay, describeEventOnDay, eventKey } from '../lib/describe'
import { formatDayHeading, formatFullDate, formatTimeShort } from '../lib/format'
import { instantAfter } from '../lib/timeline'
import { RepeatIcon } from './RepeatIcon'

interface WeekListProps {
  week: DaysView
  selectedDate: string
  /** `leavesPeriod` is true when the date is outside this week, so the neighboring week must load. */
  onMoveDate: (date: string, leavesPeriod: boolean) => void
  onOpenDay: (date: string) => void
  onOpenEvent: (event: EventSummary, date: string) => void
}

interface EventRowProps {
  event: EventSummary
  timeZone: string
  day: TimelineDay
  /** The event's part on this day; absent for all-day events. */
  segment?: TimedSegment
  onOpen: () => void
}

/** The event's own range, or its part from 12:00 AM on a day after the one it starts on (003 FR-032). */
function timeText({ event, timeZone, day, segment }: EventRowProps): string {
  if (event.isAllDay || !event.start || !event.end) return 'All day'
  if (!segment?.continuesBefore) return `${formatTimeShort(event.start, timeZone)} – ${formatTimeShort(event.end, timeZone)}`
  const from = instantAfter(day.dayStart, segment.offsetMinutes)
  const to = instantAfter(day.dayStart, segment.offsetMinutes + segment.durationMinutes)
  return `${formatTimeShort(from, timeZone)} – ${formatTimeShort(to, timeZone)}`
}

function EventRow(props: EventRowProps) {
  const { event, timeZone, segment, onOpen } = props
  return (
    <button
      type="button"
      className={event.isAllDay ? 'list-event all-day' : 'list-event'}
      aria-label={describeEventOnDay(event, timeZone, segment?.continuesBefore ?? false)}
      onClick={onOpen}
    >
      <span className="list-event-time">{timeText(props)}</span>
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
        const rows: { event: EventSummary; segment?: TimedSegment }[] = [
          ...day.allDay.map((event) => ({ event })),
          ...day.timed.map((segment) => ({ event: segment.event, segment })),
        ]
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
            {rows.length === 0 ? (
              <p className="no-events">No events</p>
            ) : (
              <ul className="week-list-events">
                {rows.map(({ event, segment }) => (
                  <li key={eventKey(event)}>
                    <EventRow
                      event={event}
                      timeZone={week.timeZone}
                      day={day}
                      segment={segment}
                      onOpen={() => onOpenEvent(event, day.date)}
                    />
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
