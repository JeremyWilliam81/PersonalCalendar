import { createContext, useContext } from 'react'
import { httpCalendarApi, type CalendarApi } from './client'

/** Lets tests swap in a stub API. */
export const ApiContext = createContext<CalendarApi>(httpCalendarApi)

export function useCalendarApi(): CalendarApi {
  return useContext(ApiContext)
}
