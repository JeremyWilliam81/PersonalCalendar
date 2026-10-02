import { addDays, addMonthsClamped, endOfWeek, isInSupportedRange, isValidDateString, startOfWeek } from './dates'

// What the user is looking at: a view and a selected calendar date (data-model "ViewState").

export const VIEW_TYPES = ['day', 'week', 'month'] as const
export type ViewType = (typeof VIEW_TYPES)[number]

export interface ViewState {
  view: ViewType
  /** yyyy-MM-dd, always within the supported range. */
  date: string
}

export function periodOf(state: ViewState): { first: string; last: string } {
  switch (state.view) {
    case 'day':
      return { first: state.date, last: state.date }
    case 'week':
      return { first: startOfWeek(state.date), last: endOfWeek(state.date) }
    case 'month': {
      const first = `${state.date.slice(0, 8)}01`
      return { first, last: addDays(addMonthsClamped(first, 1), -1) }
    }
  }
}

/** The previous or next period (FR-009), or null when it would leave the supported range (FR-012). */
export function stepPeriod(state: ViewState, delta: 1 | -1): ViewState | null {
  const date =
    state.view === 'month'
      ? addMonthsClamped(state.date, delta)
      : addDays(state.date, state.view === 'week' ? 7 * delta : delta)
  return isInSupportedRange(date) ? { view: state.view, date } : null
}

const PATH = /^\/(day|week|month)(?:\/(\d{4}-\d{2}-\d{2}))?\/?$/

/** The canonical address for a view state (FR-017). */
export function toPath(state: ViewState): string {
  return `/${state.view}/${state.date}`
}

/**
 * Reads an address (FR-018 – FR-020): `/` is this month, `/{view}` is that view for today, and anything that
 * is not exactly `/{view}/{a real date in range}` falls back to this month and reports an invalid link.
 */
export function parsePath(pathname: string, today: string): { state: ViewState; error?: 'invalid-link' } {
  if (pathname === '/' || pathname === '') return { state: { view: 'month', date: today } }
  const match = PATH.exec(pathname)
  const date = match?.[2] ?? today
  if (!match || !isValidDateString(date) || !isInSupportedRange(date)) {
    return { state: { view: 'month', date: today }, error: 'invalid-link' }
  }
  return { state: { view: match[1] as ViewType, date } }
}
