import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { useCalendarApi } from '../api/ApiContext'
import type { DaysView, EditScope, EventDetails, EventSummary, MonthView as MonthViewData } from '../api/types'
import { useNarrowScreen } from '../hooks/useNarrowScreen'
import { useSwipe } from '../hooks/useSwipe'
import { useViewState } from '../hooks/useViewState'
import { isWithin } from '../lib/dates'
import { formatPeriodTitle } from '../lib/format'
import { newEventAt } from '../lib/newEventDefaults'
import { ALL_SCOPES } from '../lib/scope'
import { periodOf, stepPeriod, type ViewState, type ViewType } from '../lib/viewState'
import { ConfirmDialog } from './ConfirmDialog'
import { DayView } from './DayView'
import { EventDetailsDialog } from './EventDetailsDialog'
import { EventFormDialog } from './EventFormDialog'
import { GoToDateDialog } from './GoToDateDialog'
import { InvalidLinkNotice } from './InvalidLinkNotice'
import { MonthView } from './MonthView'
import { ScopeChoiceDialog } from './ScopeChoiceDialog'
import { useAnnounce } from './useAnnounce'
import { ViewHeader } from './ViewHeader'
import { WeekList } from './WeekList'
import { WeekView } from './WeekView'

type DialogState =
  | { kind: 'create'; date: string; times?: { start: string; end: string } }
  | {
      kind: 'details'
      id: string
      /** Set for an occurrence of a series (003 research S4). */
      occurrenceDate?: string | null
      reloadKey: number
      notice?: string
      confirmingDelete?: EventDetails
    }
  | { kind: 'edit'; event: EventDetails }
  | null

type Loaded = { view: 'month'; key: string; month: MonthViewData } | { view: 'day' | 'week'; key: string; days: DaysView }

/** Data only needs reloading when the shown period changes, not when the selected date moves inside it. */
function periodKey(state: ViewState): string {
  return `${state.view}:${periodOf(state).first}`
}

/** "Week of October 11 – 17, 2026" when the view changed; otherwise just the period title (FR-025). */
function announcement(next: ViewState, previousView: ViewType): string {
  const title = formatPeriodTitle(next.view, next.date)
  if (next.view === previousView) return title
  return next.view === 'week' ? `Week of ${title}` : `${next.view === 'day' ? 'Day' : 'Month'} view, ${title}`
}

/** Where focus goes once the new period is on screen (contracts/ui-interaction "Focus after navigation"). */
type FocusTarget = 'date' | 'heading'

const CONFLICT_NOTICE = 'This event was changed in another window. Showing the latest version.'

const DELETED: Record<EditScope, string> = {
  this: 'Deleted this event.',
  following: 'Deleted this and following events.',
  all: 'Deleted all events.',
}

/** The selected date if the loaded grid shows it; otherwise today (if shown) or the 1st. */
function focusDateIn(view: MonthViewData, selected: string): string {
  const days = view.weeks.flatMap((w) => w.days)
  if (isWithin(selected, days[0].date, days[days.length - 1].date)) return selected
  const today = days.find((d) => d.isToday && d.inMonth)
  return today?.date ?? days.find((d) => d.inMonth)!.date
}

interface CalendarScreenProps {
  timeZone: string
  /** Today's date in `timeZone` (yyyy-MM-dd). */
  today: string
  /** The current time, for the line on today's column; the server's time is used until it is given. */
  now?: Date
}

export function CalendarScreen({ timeZone, today, now }: CalendarScreenProps) {
  const api = useCalendarApi()
  const announce = useAnnounce()
  const titleId = useId()
  const isNarrow = useNarrowScreen()
  const { state, setState, error: linkError, clearError: dismissLinkError, popCount } = useViewState(today)
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [dialog, setDialog] = useState<DialogState>(null)
  const [gridFocusRequest, setGridFocusRequest] = useState(0)
  const [goingToDate, setGoingToDate] = useState(false)
  const latestRequest = useRef(0)
  const titleRef = useRef<HTMLHeadingElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const pendingFocus = useRef<FocusTarget | null>(null)

  const key = periodKey(state)
  const loadState = useRef(state)
  loadState.current = state

  const load = useCallback(async () => {
    const requestId = ++latestRequest.current
    const target = loadState.current
    const { first } = periodOf(target)
    const result =
      target.view === 'month'
        ? await api.getMonth(timeZone, Number(first.slice(0, 4)), Number(first.slice(5, 7)))
        : await api.getDays(timeZone, first, target.view === 'week' ? 7 : 1)
    if (requestId !== latestRequest.current) return // a newer navigation won

    if (result.kind !== 'ok') {
      setLoadFailed(true)
      announce("Couldn't load the calendar.")
      return
    }
    setLoadFailed(false)
    setLoaded(
      target.view === 'month'
        ? { view: 'month', key: periodKey(target), month: result.value as MonthViewData }
        : { view: target.view, key: periodKey(target), days: result.value as DaysView },
    )
    // The period key is what identifies the request; the ref holds the matching state.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, announce, timeZone, key, today])

  useEffect(() => {
    // Fetching the period is synchronizing with the API; state is only set after the await.
    // eslint-disable-next-line react/set-state-in-effect
    void load()
  }, [load, reloadKey])

  const reload = () => setReloadKey((n) => n + 1)

  // Back and Forward land on the period heading (contracts/ui-interaction "Focus after navigation").
  useEffect(() => {
    if (popCount > 0) pendingFocus.current = 'heading'
  }, [popCount])

  useEffect(() => {
    if (!pendingFocus.current || loaded?.key !== key) return
    const target =
      pendingFocus.current === 'date' && state.view !== 'day'
        ? bodyRef.current?.querySelector<HTMLElement>(
            `[role="gridcell"][data-date="${state.date}"], button.week-list-heading[data-date="${state.date}"]`,
          )
        : titleRef.current
    target?.focus()
    pendingFocus.current = null
  }, [loaded, key, state])

  /** Shows another period or view and announces it (FR-025). */
  const navigate = (next: ViewState, focus?: FocusTarget) => {
    pendingFocus.current = focus ?? null
    setState(next, 'push')
    announce(announcement(next, state.view))
  }

  const step = (delta: 1 | -1) => {
    const next = stepPeriod(state, delta)
    if (next) navigate(next)
  }

  /** Arrow-key moves: inside the period only the selected date changes; past its edge a new period loads. */
  const moveDate = (date: string, leavesPeriod: boolean) =>
    leavesPeriod ? navigate({ view: state.view, date }) : setState({ view: state.view, date }, 'replace')

  const swipe = useSwipe(
    () => step(-1),
    () => step(1),
  )

  const createAt = (localStart: string) =>
    setDialog({ kind: 'create', date: localStart.slice(0, 10), times: newEventAt(localStart) })

  const openEvent = (event: EventSummary, date: string) => {
    setState({ view: state.view, date }, 'replace')
    openDetails(event.id, event.occurrenceDate)
  }

  const openDetails = (id: string, occurrenceDate?: string | null, notice?: string) =>
    setDialog((current) => ({
      kind: 'details',
      id,
      occurrenceDate,
      notice,
      reloadKey: current?.kind === 'details' ? current.reloadKey + 1 : 0,
    }))

  /** For an occurrence of a series, `scope` says what to delete (003 FR-023). */
  const deleteEvent = async (details: EventDetails, scope?: EditScope) => {
    const result =
      scope && details.occurrenceDate
        ? await api.deleteEvent(details.id, details.version, { occurrence: details.occurrenceDate, scope })
        : await api.deleteEvent(details.id, details.version)
    switch (result.kind) {
      case 'ok':
      case 'notFound': // already gone: the outcome the user asked for
        setDialog(null)
        announce(scope ? DELETED[scope] : `Deleted ${details.title}.`)
        reload()
        setGridFocusRequest((n) => n + 1)
        return
      case 'conflict':
        openDetails(details.id, details.occurrenceDate, CONFLICT_NOTICE)
        announce('This event was changed in another window.')
        return
      default:
        openDetails(details.id, details.occurrenceDate, "Couldn't delete the event. Try again.")
        announce("Couldn't delete the event.")
    }
  }

  if (!loaded) {
    return loadFailed ? (
      <div className="load-error">
        <p>Couldn&apos;t load the calendar.</p>
        <button type="button" onClick={reload}>
          Try again
        </button>
      </div>
    ) : (
      <p className="loading">Loading…</p>
    )
  }

  const renderBody = () => {
    if (loaded.view !== state.view) return <p className="loading">Loading…</p>
    if (loaded.view === 'month') {
      return (
        <MonthView
          month={loaded.month}
          titleId={titleId}
          focusedDate={focusDateIn(loaded.month, state.date)}
          focusRequest={gridFocusRequest}
          onMoveFocus={moveDate}
          onOpenDay={(date) => navigate({ view: 'day', date }, 'heading')}
          onOpenEvent={openEvent}
        />
      )
    }
    const nowOnScreen = now ?? new Date(loaded.days.now)
    if (loaded.view === 'day') {
      return <DayView now={nowOnScreen} day={loaded.days} titleId={titleId} onOpenEvent={openEvent} onStep={step} onCreateAt={createAt} />
    }
    const weekProps = {
      week: loaded.days,
      selectedDate: state.date,
      onMoveDate: moveDate,
      onOpenDay: (date: string) => navigate({ view: 'day', date }, 'heading'),
      onOpenEvent: openEvent,
    }
    return isNarrow ? <WeekList {...weekProps} /> : <WeekView {...weekProps} titleId={titleId} onCreateAt={createAt} now={nowOnScreen} />
  }

  return (
    <>
      <ViewHeader
        state={state}
        titleId={titleId}
        titleRef={titleRef}
        isToday={state.view === 'day' && state.date === today}
        canGoPrevious={stepPeriod(state, -1) !== null}
        canGoNext={stepPeriod(state, 1) !== null}
        onPrevious={() => step(-1)}
        onNext={() => step(1)}
        onToday={() => navigate({ view: state.view, date: today })}
        onNewEvent={() => setDialog({ kind: 'create', date: state.date })}
        onChangeView={(view) => view !== state.view && navigate({ view, date: state.date })}
        actions={
          <button type="button" className="go-to-date" onClick={() => setGoingToDate(true)}>
            Go to date
          </button>
        }
      />
      {linkError && <InvalidLinkNotice onDismiss={dismissLinkError} />}
      {loadFailed && (
        <p className="form-error">
          Couldn&apos;t load that period.{' '}
          <button type="button" onClick={reload}>
            Try again
          </button>
        </p>
      )}
      <div className="view-body" ref={bodyRef} {...swipe.handlers} style={swipe.style}>
        {renderBody()}
      </div>

      {goingToDate && (
        <GoToDateDialog
          initialDate={state.date}
          onClose={() => setGoingToDate(false)}
          onGo={(date) => {
            setGoingToDate(false)
            navigate({ view: state.view, date }, 'date')
          }}
        />
      )}

      {dialog?.kind === 'details' && (
        <>
          <EventDetailsDialog
            key={`${dialog.id}:${dialog.occurrenceDate ?? ''}`}
            eventId={dialog.id}
            occurrenceDate={dialog.occurrenceDate}
            timeZone={timeZone}
            reloadKey={dialog.reloadKey}
            notice={dialog.notice}
            onClose={() => setDialog(null)}
            onEdit={(details) => setDialog({ kind: 'edit', event: details })}
            onDelete={(details) => setDialog({ ...dialog, notice: undefined, confirmingDelete: details })}
          />
          {dialog.confirmingDelete?.recurrence && (
            <ScopeChoiceDialog
              mode="delete"
              choices={ALL_SCOPES}
              onChoose={(scope) => void deleteEvent(dialog.confirmingDelete!, scope)}
              onCancel={() => setDialog({ ...dialog, confirmingDelete: undefined })}
            />
          )}
          {dialog.confirmingDelete && !dialog.confirmingDelete.recurrence && (
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
          initialDate={dialog.kind === 'create' ? dialog.date : state.date}
          event={dialog.kind === 'edit' ? dialog.event : undefined}
          initialTimes={dialog.kind === 'create' ? dialog.times : undefined}
          timeZone={timeZone}
          onClose={() => setDialog(null)}
          onSaved={() => {
            setDialog(null)
            reload()
          }}
        />
      )}
    </>
  )
}
