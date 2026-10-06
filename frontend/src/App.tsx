import { useToday } from './hooks/useToday'
import { CalendarScreen } from './components/CalendarScreen'
import { LiveRegionProvider } from './components/LiveRegion'

export default function App() {
  const { timeZone, today, now } = useToday()

  return (
    <LiveRegionProvider>
      <main className="app">
        <h1 className="visually-hidden">Personal Calendar</h1>
        <CalendarScreen timeZone={timeZone} today={today} now={now} />
      </main>
    </LiveRegionProvider>
  )
}
