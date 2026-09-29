import { useState } from 'react'
import { currentTimeZone } from './api/timeZone'
import { CalendarScreen } from './components/CalendarScreen'
import { LiveRegionProvider } from './components/LiveRegion'

export default function App() {
  const [timeZone] = useState(currentTimeZone)

  return (
    <LiveRegionProvider>
      <main className="app">
        <h1 className="visually-hidden">Personal Calendar</h1>
        <CalendarScreen timeZone={timeZone} />
      </main>
    </LiveRegionProvider>
  )
}
