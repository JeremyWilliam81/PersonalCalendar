import type { EventSummary } from '../api/types'
import { continuesFromPreviousDay, describeEventOnDay } from '../lib/describe'
import { formatLocalTime, formatTimeShort } from '../lib/format'
import { RepeatIcon } from './RepeatIcon'

interface EventButtonProps {
  event: EventSummary
  timeZone: string
  /** The day the button is shown on; a timed event that began earlier shows from 12:00 AM (003 FR-032). */
  date: string
  tabbable: boolean
  onOpen: (event: EventSummary) => void
}

export function EventButton({ event, timeZone, date, tabbable, onOpen }: EventButtonProps) {
  const continuesBefore = continuesFromPreviousDay(event, date, timeZone)
  return (
    <button
      type="button"
      className={event.isAllDay ? 'event-button all-day' : 'event-button'}
      tabIndex={tabbable ? 0 : -1}
      aria-label={describeEventOnDay(event, timeZone, continuesBefore)}
      onClick={() => onOpen(event)}
    >
      {!event.isAllDay && event.start && (
        <>
          <span className="event-time">
            {continuesBefore ? formatLocalTime(`${date}T00:00`) : formatTimeShort(event.start, timeZone)}
          </span>{' '}
        </>
      )}
      <span className="event-title">{event.title}</span>
      {event.isRecurring && <RepeatIcon />}
    </button>
  )
}
