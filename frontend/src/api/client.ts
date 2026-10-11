import type { ApiResult, DateString, DaysView, EditScope, EventDetails, EventInput, MonthView } from './types'

/** Which occurrence of a series a change applies to, and how far (FR-015). Omitted for one-time events. */
export interface SeriesTarget {
  occurrence: DateString
  scope: EditScope
}

export interface CalendarApi {
  getMonth(timeZone: string, year?: number, month?: number): Promise<ApiResult<MonthView>>
  getDays(timeZone: string, start: string, count: 1 | 7): Promise<ApiResult<DaysView>>
  /** `occurrence` is required for a series and must be omitted for a one-time event. */
  getEvent(id: string, timeZone: string, occurrence?: DateString): Promise<ApiResult<EventDetails>>
  createEvent(input: EventInput): Promise<ApiResult<EventDetails>>
  updateEvent(id: string, input: EventInput, target?: SeriesTarget): Promise<ApiResult<EventDetails>>
  deleteEvent(id: string, version: number, target?: SeriesTarget): Promise<ApiResult<null>>
}

interface Problem {
  type?: string
  errors?: Record<string, string[]>
  adjustedStart?: string
  adjustedEnd?: string
  timeZone?: string
}

async function readProblem(response: Response): Promise<Problem> {
  try {
    return (await response.json()) as Problem
  } catch {
    return {}
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<ApiResult<T>> {
  let response: Response
  try {
    response = await fetch(url, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    // The API is not reachable (e.g. stopped). Never throw into the UI (FR-014).
    return { kind: 'network' }
  }

  if (response.ok) {
    const value = response.status === 204 ? null : await response.json()
    return { kind: 'ok', value: value as T }
  }

  const problem = await readProblem(response)
  switch (response.status) {
    case 400:
      return { kind: 'validation', errors: problem.errors ?? {} }
    case 404:
      return { kind: 'notFound' }
    case 409:
      return { kind: 'conflict' }
    case 422:
      return {
        kind: 'dstAdjustment',
        adjustedStart: problem.adjustedStart ?? '',
        adjustedEnd: problem.adjustedEnd ?? '',
        timeZone: problem.timeZone ?? '',
      }
    default:
      return { kind: 'saveFailed' }
  }
}

export const httpCalendarApi: CalendarApi = {
  getMonth(timeZone, year, month) {
    const query = new URLSearchParams({ timeZone })
    if (year !== undefined && month !== undefined) {
      query.set('year', String(year))
      query.set('month', String(month))
    }
    return request('GET', `/api/calendar/month?${query}`)
  },
  getDays(timeZone, start, count) {
    return request('GET', `/api/calendar/days?${new URLSearchParams({ timeZone, start, count: String(count) })}`)
  },
  getEvent(id, timeZone, occurrence) {
    const query = new URLSearchParams({ timeZone })
    if (occurrence) query.set('occurrence', occurrence)
    return request('GET', `/api/events/${encodeURIComponent(id)}?${query}`)
  },
  createEvent(input) {
    return request('POST', '/api/events', input)
  },
  updateEvent(id, input, target) {
    const query = target ? `?${new URLSearchParams({ occurrence: target.occurrence, scope: target.scope })}` : ''
    return request('PUT', `/api/events/${encodeURIComponent(id)}${query}`, input)
  },
  deleteEvent(id, version, target) {
    const query = new URLSearchParams({ version: String(version) })
    if (target) {
      query.set('occurrence', target.occurrence)
      query.set('scope', target.scope)
    }
    return request('DELETE', `/api/events/${encodeURIComponent(id)}?${query}`)
  },
}
