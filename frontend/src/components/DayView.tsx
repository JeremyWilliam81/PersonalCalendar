import type { KeyboardEvent } from 'react'
import type { DaysView } from '../api/types'
import { describeEvent } from '../lib/describe'
import { TimeGrid } from './TimeGrid'

interface DayViewProps {
  day: DaysView
  /** Id of the period heading in the shared header. */
  titleId: string
  onOpenEvent: (id: string, date: string) => void
  /** ← and → move to the previous or next day (FR-024). */
  onStep: (delta: 1 | -1) => void
  onCreateAt?: (localStart: string) => void
  now?: Date
}

/** One day: all-day events stacked on top, then the time grid (contracts/ui-interaction "Day view"). */
export function DayView({ day, titleId, onOpenEvent, onStep, onCreateAt, now }: DayViewProps) {
  const timeline = day.days[0]

  const handleKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return
    if ((event.target as HTMLElement).closest('input, textarea, select, dialog')) return
    event.preventDefault()
    onStep(event.key === 'ArrowLeft' ? -1 : 1)
  }

  return (
    <section className="day-view" aria-labelledby={titleId} onKeyDown={handleKeyDown}>
      {timeline.allDay.length > 0 && (
        <ul className="day-all-day">
          {timeline.allDay.map((event) => (
            <li key={event.id}>
              <button
                type="button"
                className="list-event all-day"
                aria-label={describeEvent(event, day.timeZone)}
                onClick={() => onOpenEvent(event.id, timeline.date)}
              >
                <span className="list-event-time">All day</span>
                <span className="list-event-title">{event.title}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <TimeGrid days={day.days} timeZone={day.timeZone} onOpenEvent={onOpenEvent} onCreateAt={onCreateAt} now={now} />
    </section>
  )
}
