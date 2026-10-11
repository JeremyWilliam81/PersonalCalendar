import type { AllDayBar, EventSummary } from '../api/types'
import { describeEvent, eventKey } from '../lib/describe'
import { RepeatIcon } from './RepeatIcon'

interface AllDayBarsProps {
  bars: AllDayBar[]
  timeZone: string
  /** The first day of the range; a bar opens with the date it starts on in this range. */
  dates: string[]
  onOpenEvent: (event: EventSummary, date: string) => void
}

function describeBar(bar: AllDayBar, timeZone: string): string {
  const parts = [describeEvent(bar.event, timeZone)]
  if (bar.continuesBefore) parts.push('continues from the previous week')
  if (bar.continuesAfter) parts.push('continues into the next week')
  return parts.join(', ')
}

/** All-day events as one continuous bar each across the wide week view (FR-005, research V5). */
export function AllDayBars({ bars, timeZone, dates, onOpenEvent }: AllDayBarsProps) {
  if (bars.length === 0) return null
  return (
    <div className="all-day-bars" style={{ gridTemplateColumns: `repeat(${dates.length}, minmax(0, 1fr))` }}>
      {bars.map((bar) => (
        <button
          key={eventKey(bar.event)}
          type="button"
          className="all-day-bar"
          aria-label={describeBar(bar, timeZone)}
          data-continues-before={bar.continuesBefore || undefined}
          data-continues-after={bar.continuesAfter || undefined}
          style={{ gridColumn: `${bar.startIndex + 1} / span ${bar.span}`, gridRow: bar.lane + 1 }}
          onClick={() => onOpenEvent(bar.event, dates[bar.startIndex])}
        >
          {bar.event.title}
          {bar.event.isRecurring && <RepeatIcon />}
          {bar.continuesBefore && <span className="visually-hidden">, continues from the previous week</span>}
          {bar.continuesAfter && <span className="visually-hidden">, continues into the next week</span>}
        </button>
      ))}
    </div>
  )
}
