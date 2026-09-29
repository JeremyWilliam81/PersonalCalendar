import { useCallback, useEffect, useRef, useState } from 'react'
import { useCalendarApi } from '../api/ApiContext'
import type { EventDetails, MonthView as MonthViewData } from '../api/types'
import { addMonthsClamped, isWithin, monthOf } from '../lib/dates'
import { formatMonthTitle } from '../lib/format'
import { ConfirmDialog } from './ConfirmDialog'
import { EventDetailsDialog } from './EventDetailsDialog'
import { EventFormDialog } from './EventFormDialog'
import { MonthView } from './MonthView'
import { useAnnounce } from './useAnnounce'

type DialogState =
  | { kind: 'create'; date: string }
  | { kind: 'details'; id: string; reloadKey: number; notice?: string; confirmingDelete?: EventDetails }
  | { kind: 'edit'; event: EventDetails }
  | null

interface MonthRequest {
  year?: number
  month?: number
  /** Date to focus once the month is loaded; defaults to today (if shown) or the 1st. */
  focus?: string
  announce: boolean
}

const CONFLICT_NOTICE = 'This event was changed in another window. Showing the latest version.'

function defaultFocus(view: MonthViewData): string {
  const days = view.weeks.flatMap((w) => w.days)
  const today = days.find((d) => d.isToday && d.inMonth)
  return today?.date ?? days.find((d) => d.inMonth)!.date
}

export function CalendarScreen({ timeZone }: { timeZone: string }) {
  const api = useCalendarApi()
  const announce = useAnnounce()
  const [view, setView] = useState<MonthViewData | null>(null)
  const [focusedDate, setFocusedDate] = useState<string | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)
  const [dialog, setDialog] = useState<DialogState>(null)
  const [gridFocusRequest, setGridFocusRequest] = useState(0)
  const latestRequest = useRef(0)

  const load = useCallback(
    async (request: MonthRequest) => {
      const requestId = ++latestRequest.current
      const result = await api.getMonth(timeZone, request.year, request.month)
      if (requestId !== latestRequest.current) return // a newer navigation won

      if (result.kind !== 'ok') {
        setLoadFailed(true)
        announce("Couldn't load the calendar.")
        return
      }

      const loaded = result.value
      const days = loaded.weeks.flatMap((w) => w.days)
      const focus =
        request.focus && isWithin(request.focus, days[0].date, days[days.length - 1].date)
          ? request.focus
          : defaultFocus(loaded)
      setLoadFailed(false)
      setView(loaded)
      setFocusedDate(focus)
      if (request.announce) announce(formatMonthTitle(loaded.year, loaded.month))
    },
    [api, announce, timeZone],
  )

  useEffect(() => {
    // Fetching the month is synchronizing with the API; state is only set after the await.
    // eslint-disable-next-line react/set-state-in-effect
    void load({ announce: false })
  }, [load])

  const reload = async () => {
    if (view) await load({ year: view.year, month: view.month, focus: focusedDate ?? undefined, announce: false })
  }

  const goToMonthOf = (date: string) => {
    const { year, month } = monthOf(date)
    void load({ year, month, focus: date, announce: true })
  }

  const shiftMonth = (delta: number) => {
    if (!view) return
    const first = `${view.year}-${String(view.month).padStart(2, '0')}-01`
    goToMonthOf(addMonthsClamped(first, delta))
  }

  const openDetails = (id: string, notice?: string) =>
    setDialog((current) => ({
      kind: 'details',
      id,
      notice,
      reloadKey: current?.kind === 'details' ? current.reloadKey + 1 : 0,
    }))

  const deleteEvent = async (details: EventDetails) => {
    const result = await api.deleteEvent(details.id, details.version)
    switch (result.kind) {
      case 'ok':
      case 'notFound': // already gone: the outcome the user asked for
        setDialog(null)
        announce(`Deleted ${details.title}.`)
        await reload()
        setGridFocusRequest((n) => n + 1)
        return
      case 'conflict':
        openDetails(details.id, CONFLICT_NOTICE)
        announce('This event was changed in another window.')
        return
      default:
        openDetails(details.id, "Couldn't delete the event. Try again.")
        announce("Couldn't delete the event.")
    }
  }

  if (!view || !focusedDate) {
    return loadFailed ? (
      <div className="load-error">
        <p>Couldn&apos;t load the calendar.</p>
        <button type="button" onClick={() => void load({ announce: false })}>
          Try again
        </button>
      </div>
    ) : (
      <p className="loading">Loading…</p>
    )
  }

  return (
    <>
      {loadFailed && (
        <p className="form-error">
          Couldn&apos;t load that month.{' '}
          <button type="button" onClick={() => void reload()}>
            Try again
          </button>
        </p>
      )}
      <MonthView
        month={view}
        focusedDate={focusedDate}
        focusRequest={gridFocusRequest}
        onMoveFocus={(date, changeMonth) => (changeMonth ? goToMonthOf(date) : setFocusedDate(date))}
        onPreviousMonth={() => shiftMonth(-1)}
        onNextMonth={() => shiftMonth(1)}
        onToday={() => void load({ announce: true })}
        onCreate={(date) => setDialog({ kind: 'create', date })}
        onOpenEvent={(id, date) => {
          setFocusedDate(date)
          openDetails(id)
        }}
      />

      {dialog?.kind === 'details' && (
        <>
          <EventDetailsDialog
            key={dialog.id}
            eventId={dialog.id}
            timeZone={timeZone}
            reloadKey={dialog.reloadKey}
            notice={dialog.notice}
            onClose={() => setDialog(null)}
            onEdit={(details) => setDialog({ kind: 'edit', event: details })}
            onDelete={(details) => setDialog({ ...dialog, notice: undefined, confirmingDelete: details })}
          />
          {dialog.confirmingDelete && (
            <ConfirmDialog
              title={`Delete "${dialog.confirmingDelete.title}"?`}
              description="This can't be undone."
              confirmLabel="Delete"
              cancelLabel="Cancel"
              initialFocus="cancel"
              onConfirm={() => void deleteEvent(dialog.confirmingDelete!)}
              onCancel={() => setDialog({ ...dialog, confirmingDelete: undefined })}
            />
          )}
        </>
      )}

      {(dialog?.kind === 'create' || dialog?.kind === 'edit') && (
        <EventFormDialog
          initialDate={dialog.kind === 'create' ? dialog.date : focusedDate}
          event={dialog.kind === 'edit' ? dialog.event : undefined}
          timeZone={timeZone}
          onClose={() => setDialog(null)}
          onSaved={() => {
            setDialog(null)
            void reload()
          }}
        />
      )}
    </>
  )
}
