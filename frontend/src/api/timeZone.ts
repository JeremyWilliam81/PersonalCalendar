/** The device's current IANA zone, read fresh for each page load (clarification Q2, research R7). */
export function currentTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone
}
