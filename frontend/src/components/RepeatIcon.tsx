/**
 * Marks an occurrence of a series by shape, not color (003 FR-011, research S12). Decorative: the accessible name
 * of the event already ends with ", repeats".
 */
export function RepeatIcon() {
  return (
    <svg className="repeat-icon" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" focusable="false">
      <path
        d="M3 7a5 5 0 0 1 8.6-3.5L13 5M13 2v3h-3M13 9a5 5 0 0 1-8.6 3.5L3 11M3 14v-3h3"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
