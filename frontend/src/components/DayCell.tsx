import type { DayCell as DayCellData } from '../api/types'
import { formatFullDate } from '../lib/format'
import { EventButton } from './EventButton'

interface DayCellProps {
  day: DayCellData
  timeZone: string
  focused: boolean
  maxVisible: number
  onFocusDay: (date: string) => void
  onOpenEvent: (id: string) => void
  onShowAll: (date: string) => void
}

export function DayCell({ day, timeZone, focused, maxVisible, onFocusDay, onOpenEvent, onShowAll }: DayCellProps) {
  const fullDate = formatFullDate(day.date)
  const visible = day.events.slice(0, maxVisible)
  const hidden = day.events.length - visible.length
  const dayNumber = Number(day.date.slice(8, 10))

  return (
    <div
      role="gridcell"
      className="day"
      data-date={day.date}
      data-outside-month={day.inMonth ? undefined : 'true'}
      aria-current={day.isToday ? 'date' : undefined}
      aria-label={fullDate}
      tabIndex={focused ? 0 : -1}
      onClick={(event) => {
        if (event.target === event.currentTarget) onFocusDay(day.date)
      }}
    >
      <span className="day-number" aria-hidden="true">
        {dayNumber}
      </span>
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
    </div>
  )
}
