import type { ApiResult, EventDetails, EventInput, MonthView } from './types'

export interface CalendarApi {
  getMonth(timeZone: string, year?: number, month?: number): Promise<ApiResult<MonthView>>
  getEvent(id: string, timeZone: string): Promise<ApiResult<EventDetails>>
  createEvent(input: EventInput): Promise<ApiResult<EventDetails>>
  updateEvent(id: string, input: EventInput): Promise<ApiResult<EventDetails>>
  deleteEvent(id: string, version: number): Promise<ApiResult<null>>
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
  getEvent(id, timeZone) {
    return request('GET', `/api/events/${encodeURIComponent(id)}?${new URLSearchParams({ timeZone })}`)
  },
  createEvent(input) {
    return request('POST', '/api/events', input)
  },
  updateEvent(id, input) {
    return request('PUT', `/api/events/${encodeURIComponent(id)}`, input)
  },
  deleteEvent(id, version) {
    return request('DELETE', `/api/events/${encodeURIComponent(id)}?${new URLSearchParams({ version: String(version) })}`)
  },
}
