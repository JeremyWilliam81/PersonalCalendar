import { minutesToPx } from '../lib/timeline'

interface NowLineProps {
  /** The day's real start instant (ISO 8601 with offset). */
  dayStart: string
  now: Date
}

/** The current time on today's column (FR-016). Elapsed time from the day's start, so DST days are right. */
export function NowLine({ dayStart, now }: NowLineProps) {
  const minutes = (now.getTime() - Date.parse(dayStart)) / 60_000
  return <div className="now-line" aria-hidden="true" style={{ top: `${minutesToPx(minutes)}px` }} />
}
