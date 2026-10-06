import { useLayoutEffect, useRef, useState, type MouseEvent, type PointerEvent } from 'react'
import type { EventSummary, TimedSegment, TimelineDay } from '../api/types'
import { describeEvent, eventKey } from '../lib/describe'
import { formatFullDate, formatHourLabel, formatTimeShort, toLocalInputValue } from '../lib/format'
import { clustersOf, instantAfter, MIN_BLOCK_PX, minutesToPx } from '../lib/timeline'
import { DayOverflowDialog } from './DayOverflowDialog'
import { NowLine } from './NowLine'
import { RepeatIcon } from './RepeatIcon'

interface TimeGridProps {
  days: TimelineDay[]
  timeZone: string
  onOpenEvent: (event: EventSummary, date: string) => void
  /** Tapping empty time starts an event at that half-hour, given as a local yyyy-MM-ddTHH:mm (FR-013b). */
  onCreateAt?: (localStart: string) => void
  /** The current time, for the line on today's column (FR-016). */
  now?: Date
}

/** Clusters this wide get a full-size list, since their blocks are too narrow to tap reliably (research V10). */
const CROWDED_COLUMNS = 3
const DEFAULT_SCROLL_HOUR = '08:00'
const SCROLL_MARGIN_PX = 16
const SLOT_MINUTES = 30

/** The half-hour slot under the pointer, counted from the day's real start (research V9). */
function slotAt(event: MouseEvent<HTMLElement> | PointerEvent<HTMLElement>, day: TimelineDay): number | null {
  const offsetPx = event.clientY - event.currentTarget.getBoundingClientRect().top
  const slot = Math.floor(offsetPx / minutesToPx(SLOT_MINUTES))
  return slot >= 0 && slot * SLOT_MINUTES < day.lengthMinutes ? slot : null
}

/** Where the view opens: now if today is shown, else the earliest timed event, else 8:00 AM (spec Edge Cases). */
function initialScrollMinutes(days: TimelineDay[], now?: Date): number {
  const today = days.find((d) => d.isToday)
  if (today && now) return (now.getTime() - Date.parse(today.dayStart)) / 60_000
  const offsets = days.flatMap((d) => d.timed.map((s) => s.offsetMinutes))
  if (offsets.length > 0) return Math.min(...offsets)
  return days[0]?.hourMarks.find((m) => m.label === DEFAULT_SCROLL_HOUR)?.offsetMinutes ?? 8 * 60
}

/**
 * The time-of-day scale for the day view and the wide week view (FR-004). One column per day, as tall as the
 * day really is, so DST days are 23 or 25 hours long.
 */
export function TimeGrid({ days, timeZone, onOpenEvent, onCreateAt, now }: TimeGridProps) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const [crowded, setCrowded] = useState<{ date: string; segments: TimedSegment[] } | null>(null)
  const [pressed, setPressed] = useState<{ date: string; slot: number } | null>(null)
  const firstDate = days[0]?.date

  useLayoutEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = Math.max(0, minutesToPx(initialScrollMinutes(days, now)) - SCROLL_MARGIN_PX)
    }
    // Only when a new period is shown; reloading the same period keeps the user's scroll position.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firstDate])

  const tallest = Math.max(...days.map((d) => d.lengthMinutes))
  const gutterDay = days.reduce((a, b) => (b.lengthMinutes > a.lengthMinutes ? b : a), days[0])

  return (
    <div className="time-scroll" ref={scrollRef}>
      <div className="time-grid" style={{ gridTemplateColumns: `3.5rem repeat(${days.length}, minmax(0, 1fr))` }}>
        <div className="time-gutter" aria-hidden="true" style={{ height: `${minutesToPx(tallest)}px` }}>
          {gutterDay.hourMarks.map((mark) => (
            <span key={mark.offsetMinutes} className="hour-label" style={{ top: `${minutesToPx(mark.offsetMinutes)}px` }}>
              {mark.offsetMinutes === 0 ? '' : formatHourLabel(mark.label)}
            </span>
          ))}
        </div>

        {days.map((day) => (
          <div
            key={day.date}
            role="group"
            aria-label={formatFullDate(day.date)}
            className="time-column"
            data-date={day.date}
            data-today={day.isToday || undefined}
            style={{ height: `${minutesToPx(day.lengthMinutes)}px` }}
          >
            {onCreateAt && (
              // One large target per column; keyboard users have the New event button instead (research V9).
              <div
                className="time-slots"
                aria-hidden="true"
                onPointerDown={(event) => {
                  const slot = slotAt(event, day)
                  if (slot !== null) setPressed({ date: day.date, slot })
                }}
                onPointerUp={() => setPressed(null)}
                onPointerLeave={() => setPressed(null)}
                onPointerCancel={() => setPressed(null)}
                onClick={(event) => {
                  const slot = slotAt(event, day)
                  if (slot === null) return
                  onCreateAt(toLocalInputValue(instantAfter(day.dayStart, slot * SLOT_MINUTES), timeZone))
                }}
              >
                {pressed?.date === day.date && (
                  <div
                    className="slot-highlight"
                    style={{
                      top: `${minutesToPx(pressed.slot * SLOT_MINUTES)}px`,
                      height: `${minutesToPx(SLOT_MINUTES)}px`,
                    }}
                  />
                )}
              </div>
            )}

            {day.hourMarks.map((mark) => (
              <div
                key={mark.offsetMinutes}
                className="hour-line"
                aria-hidden="true"
                style={{ top: `${minutesToPx(mark.offsetMinutes)}px` }}
              />
            ))}

            {day.timed.map((segment) => (
              <button
                key={eventKey(segment.event)}
                type="button"
                className="time-block"
                aria-label={describeEvent(segment.event, timeZone)}
                data-continues-before={segment.continuesBefore || undefined}
                data-continues-after={segment.continuesAfter || undefined}
                style={{
                  top: `${minutesToPx(segment.offsetMinutes)}px`,
                  height: `${Math.max(minutesToPx(segment.durationMinutes), MIN_BLOCK_PX)}px`,
                  left: `${(100 * segment.column) / segment.columnCount}%`,
                  width: `${100 / segment.columnCount}%`,
                }}
                onClick={() => onOpenEvent(segment.event, day.date)}
              >
                <span className="event-title">{segment.event.title}</span>
                {segment.event.isRecurring && <RepeatIcon />}{' '}
                {!segment.continuesBefore && segment.event.start && (
                  <span className="event-time">{formatTimeShort(segment.event.start, timeZone)}</span>
                )}
              </button>
            ))}

            {clustersOf(day.timed)
              .filter((cluster) => cluster.columnCount >= CROWDED_COLUMNS)
              .map((cluster) => {
                const from = formatTimeShort(instantAfter(day.dayStart, cluster.startMinutes), timeZone)
                const to = formatTimeShort(instantAfter(day.dayStart, cluster.endMinutes), timeZone)
                return (
                  <button
                    key={cluster.startMinutes}
                    type="button"
                    className="cluster-chip"
                    aria-label={`${cluster.segments.length} events between ${from} and ${to} on ${formatFullDate(day.date)}`}
                    style={{ top: `${minutesToPx(cluster.startMinutes)}px` }}
                    onClick={() => setCrowded({ date: day.date, segments: cluster.segments })}
                  >
                    +{cluster.segments.length}
                  </button>
                )
              })}

            {day.isToday && now && <NowLine dayStart={day.dayStart} now={now} />}
          </div>
        ))}
      </div>

      {crowded && (
        <DayOverflowDialog
          date={crowded.date}
          events={crowded.segments.map((s) => s.event)}
          timeZone={timeZone}
          onOpenEvent={(event) => onOpenEvent(event, crowded.date)}
          onClose={() => setCrowded(null)}
        />
      )}
    </div>
  )
}
