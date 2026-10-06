import type { ReactNode, RefObject } from 'react'
import { formatPeriodTitle } from '../lib/format'
import { VIEW_TYPES, type ViewState, type ViewType } from '../lib/viewState'

const VIEW_NAMES: Record<ViewType, string> = { day: 'Day', week: 'Week', month: 'Month' }

interface ViewHeaderProps {
  state: ViewState
  titleId: string
  titleRef?: RefObject<HTMLHeadingElement | null>
  /** The day view shows today: a visible badge, read as ", today" (FR-014, FR-026). */
  isToday?: boolean
  canGoPrevious: boolean
  canGoNext: boolean
  onPrevious: () => void
  onNext: () => void
  onToday: () => void
  onNewEvent: () => void
  onChangeView: (view: ViewType) => void
  /** Go to date and the view switcher sit between the navigation and New event. */
  actions?: ReactNode
}

/** The header shared by every view (contracts/ui-interaction "Shared header"). */
export function ViewHeader({
  state,
  titleId,
  titleRef,
  isToday,
  canGoPrevious,
  canGoNext,
  onPrevious,
  onNext,
  onToday,
  onNewEvent,
  onChangeView,
  actions,
}: ViewHeaderProps) {
  return (
    <div className="view-header">
      <h2 id={titleId} ref={titleRef} tabIndex={-1}>
        {formatPeriodTitle(state.view, state.date)}
        {isToday && (
          <>
            <span className="visually-hidden">, today</span>
            <span className="today-badge" aria-hidden="true">
              Today
            </span>
          </>
        )}
      </h2>
      <div className="view-nav">
        <button type="button" aria-label={`Previous ${state.view}`} disabled={!canGoPrevious} onClick={onPrevious}>
          <span aria-hidden="true">‹</span>
        </button>
        <button type="button" onClick={onToday}>
          Today
        </button>
        <button type="button" aria-label={`Next ${state.view}`} disabled={!canGoNext} onClick={onNext}>
          <span aria-hidden="true">›</span>
        </button>
      </div>
      {actions}
      <div role="group" aria-label="View" className="view-switcher">
        {VIEW_TYPES.map((view) => (
          <button
            key={view}
            type="button"
            aria-pressed={state.view === view}
            aria-label={VIEW_NAMES[view]}
            onClick={() => onChangeView(view)}
          >
            <span className="view-name-long" aria-hidden="true">
              {VIEW_NAMES[view]}
            </span>
            <span className="view-name-short" aria-hidden="true">
              {VIEW_NAMES[view][0]}
            </span>
          </button>
        ))}
      </div>
      <button type="button" className="button-primary new-event" onClick={onNewEvent}>
        New event
      </button>
    </div>
  )
}
