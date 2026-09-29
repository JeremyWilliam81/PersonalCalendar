import type { EventSummary } from '../api/types'
import { describeEvent } from '../lib/describe'
import { formatTimeShort } from '../lib/format'

interface EventButtonProps {
  event: EventSummary
  timeZone: string
  tabbable: boolean
  onOpen: (id: string) => void
}

export function EventButton({ event, timeZone, tabbable, onOpen }: EventButtonProps) {
  return (
    <button
      type="button"
      className={event.isAllDay ? 'event-button all-day' : 'event-button'}
      tabIndex={tabbable ? 0 : -1}
      aria-label={describeEvent(event, timeZone)}
      onClick={() => onOpen(event.id)}
    >
      {!event.isAllDay && event.start && (
        <>
          <span className="event-time">{formatTimeShort(event.start, timeZone)}</span>{' '}
        </>
      )}
      <span className="event-title">{event.title}</span>
    </button>
  )
}
