import { addDays } from './dates'

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

/**
 * Default times for a new event on `day` (FR-005): the time of day of the next whole hour after `now`,
 * lasting one hour. `now` is read in the device zone, which is also the zone the event is entered in.
 */
export function newEventDefaults(day: string, now: Date): { start: string; end: string } {
  const startHour = (now.getHours() + 1) % 24
  const endHour = (startHour + 1) % 24
  const endDay = endHour === 0 ? addDays(day, 1) : day
  return {
    start: `${day}T${pad(startHour)}:00`,
    end: `${endDay}T${pad(endHour)}:00`,
  }
}
