import { useEffect, useState } from 'react'
import { currentTimeZone } from '../api/timeZone'
import { todayIn } from '../lib/today'

// Re-checked often enough to move the today highlight within a minute (FR-015, research V11).
const CHECK_INTERVAL_MS = 30_000

export interface Today {
  /** The device's current IANA zone. */
  timeZone: string
  /** yyyy-MM-dd in that zone. */
  today: string
  now: Date
}

interface Clock {
  now?: () => Date
  readZone?: () => string
}

function read(now: () => Date, readZone: () => string): Today {
  const instant = now()
  const timeZone = readZone()
  return { timeZone, today: todayIn(timeZone, instant), now: instant }
}

/** The device zone, today's date and the current time, kept current while the page stays open. */
export function useToday({ now = () => new Date(), readZone = currentTimeZone }: Clock = {}): Today {
  const [value, setValue] = useState(() => read(now, readZone))

  useEffect(() => {
    const check = () => setValue(read(now, readZone))
    const onVisible = () => {
      if (document.visibilityState !== 'hidden') check()
    }
    const timer = setInterval(check, CHECK_INTERVAL_MS)
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      clearInterval(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
    // The clock functions are fixed for the app's lifetime.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return value
}
