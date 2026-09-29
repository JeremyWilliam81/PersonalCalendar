// Plain calendar-date (yyyy-MM-dd) arithmetic for grid navigation.
// Computed in UTC so no zone or DST rule can shift a date.

function toUtc(date: string): Date {
  return new Date(`${date}T00:00:00Z`)
}

function fromUtc(value: Date): string {
  return value.toISOString().slice(0, 10)
}

export function addDays(date: string, days: number): string {
  const value = toUtc(date)
  value.setUTCDate(value.getUTCDate() + days)
  return fromUtc(value)
}

/** Same day number in another month, or that month's last day when it has fewer days. */
export function addMonthsClamped(date: string, months: number): string {
  const [year, month, day] = date.split('-').map(Number)
  const target = new Date(Date.UTC(year, month - 1 + months, 1))
  const daysInTarget = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate()
  target.setUTCDate(Math.min(day, daysInTarget))
  return fromUtc(target)
}

/** 0 = Sunday. */
export function dayOfWeek(date: string): number {
  return toUtc(date).getUTCDay()
}

export function startOfWeek(date: string): string {
  return addDays(date, -dayOfWeek(date))
}

export function endOfWeek(date: string): string {
  return addDays(date, 6 - dayOfWeek(date))
}

export function monthOf(date: string): { year: number; month: number } {
  const [year, month] = date.split('-').map(Number)
  return { year, month }
}

export function isWithin(date: string, first: string, last: string): boolean {
  return date >= first && date <= last
}
