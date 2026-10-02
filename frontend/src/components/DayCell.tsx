import type { DayCell as DayCellData } from '../api/types'
import { describeDay } from '../lib/describe'
import { formatFullDate } from '../lib/format'
import { EventButton } from './EventButton'

interface DayCellProps {
  day: DayCellData
  timeZone: string
  focused: boolean
  maxVisible: number
  /** Phone-sized screens show markers instead of event titles (FR-003a). */
  narrow: boolean
  onOpenDay: (date: string) => void
  onOpenEvent: (id: string) => void
  onShowAll: (date: string) => void
}

const MAX_MARKERS = 3

/** One month-grid day. Tapping anywhere but an event opens the day view (FR-013). */
export function DayCell({ day, timeZone, focused, maxVisible, narrow, onOpenDay, onOpenEvent, onShowAll }: DayCellProps) {
  const fullDate = formatFullDate(day.date)
  const visible = day.events.slice(0, narrow ? MAX_MARKERS : maxVisible)
  const hidden = day.events.length - visible.length
  const dayNumber = Number(day.date.slice(8, 10))

  return (
    <div
      role="gridcell"
      className="day"
      data-date={day.date}
      data-outside-month={day.inMonth ? undefined : 'true'}
      aria-current={day.isToday ? 'date' : undefined}
      aria-label={describeDay(day.date, { isToday: day.isToday, eventCount: narrow ? day.events.length : undefined })}
      tabIndex={focused ? 0 : -1}
      onClick={(event) => {
        if (!(event.target as HTMLElement).closest('button')) onOpenDay(day.date)
      }}
    >
      <span className="day-number" aria-hidden="true">
        {dayNumber}
      </span>
      {narrow ? (
        day.events.length > 0 && (
          <span className="markers" aria-hidden="true">
            {visible.map((event) => (
              <span key={event.id} className="marker" data-kind={event.isAllDay ? 'all-day' : 'timed'} />
            ))}
            {hidden > 0 && <span className="marker-more">+{hidden}</span>}
          </span>
        )
      ) : (
        <>
          {visible.length > 0 && (
            <ul className="day-events">
              {visible.map((event) => (
                <li key={event.id}>
                  <EventButton event={event} timeZone={timeZone} tabbable={focused} onOpen={onOpenEvent} />
                </li>
              ))}
            </ul>
          )}
          {hidden > 0 && (
            <button
              type="button"
              className="more-button"
              tabIndex={focused ? 0 : -1}
              aria-label={`${hidden} more events on ${fullDate}`}
              onClick={() => onShowAll(day.date)}
            >
              +{hidden} more
            </button>
          )}
        </>
      )}
    </div>
  )
}
