import { useEffect, useId, useState } from 'react'
import { useCalendarApi } from '../api/ApiContext'
import type { EventDetails } from '../api/types'
import { describeRange } from '../lib/describe'
import { localDateInZone } from '../lib/format'
import { describeRule } from '../lib/recurrence'
import { Modal } from './Modal'
import { RepeatIcon } from './RepeatIcon'

interface EventDetailsDialogProps {
  eventId: string
  /** For an occurrence of a series: its original date (003 research S4). */
  occurrenceDate?: string | null
  timeZone: string
  onClose: () => void
  onEdit?: (details: EventDetails) => void
  onDelete?: (details: EventDetails) => void
  /** Shown above the details, e.g. after a failed delete. */
  notice?: string
  /** Changing it re-fetches the event. */
  reloadKey?: number
}

type LoadState = { kind: 'loading' } | { kind: 'loaded'; details: EventDetails } | { kind: 'notFound' } | { kind: 'failed' }

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/** The series' first date, which monthly and yearly summaries take their day from. */
function seriesFirstDate(details: EventDetails): string {
  if (details.seriesStartDate) return details.seriesStartDate
  if (details.seriesStart) return localDateInZone(details.seriesStart, details.recurrence?.timeZone ?? details.timeZone)
  return details.occurrenceDate ?? details.startDate ?? ''
}

/** Shows every field of an event; empty optional fields are left out (FR-009). */
export function EventDetailsDialog({
  eventId,
  occurrenceDate,
  timeZone,
  onClose,
  onEdit,
  onDelete,
  notice,
  reloadKey = 0,
}: EventDetailsDialogProps) {
  const api = useCalendarApi()
  const titleId = useId()
  const [state, setState] = useState<LoadState>({ kind: 'loading' })

  useEffect(() => {
    let cancelled = false
    const request = occurrenceDate ? api.getEvent(eventId, timeZone, occurrenceDate) : api.getEvent(eventId, timeZone)
    void request.then((result) => {
      if (cancelled) return
      if (result.kind === 'ok') setState({ kind: 'loaded', details: result.value })
      else setState({ kind: result.kind === 'notFound' ? 'notFound' : 'failed' })
    })
    return () => {
      cancelled = true
    }
  }, [api, eventId, occurrenceDate, timeZone, reloadKey])

  const details = state.kind === 'loaded' ? state.details : null
  const heading = details?.title ?? (state.kind === 'loading' ? 'Loading event…' : 'Event')

  return (
    <Modal labelledBy={titleId} onCancel={onClose} className="details">
      <h2 id={titleId}>{heading}</h2>

      {notice && <p className="form-error">{notice}</p>}
      {state.kind === 'notFound' && <p>This event no longer exists.</p>}
      {state.kind === 'failed' && <p className="form-error">Couldn&apos;t load the event.</p>}

      {details && (
        <dl>
          <dt>When</dt>
          <dd>{capitalize(describeRange(details, details.timeZone))}</dd>
          {details.recurrence && (
            <>
              <dt>Repeats</dt>
              <dd className="repeat-summary-line">
                <RepeatIcon />
                {describeRule(details.recurrence, seriesFirstDate(details))}
              </dd>
            </>
          )}
          {details.location && (
            <>
              <dt>Location</dt>
              <dd>{details.location}</dd>
            </>
          )}
          {details.notes && (
            <>
              <dt>Notes</dt>
              <dd>{details.notes}</dd>
            </>
          )}
        </dl>
      )}

      <div className="dialog-actions">
        {details && onEdit && (
          <button type="button" className="button-primary" onClick={() => onEdit(details)}>
            Edit
          </button>
        )}
        {details && onDelete && (
          <button type="button" onClick={() => onDelete(details)}>
            Delete
          </button>
        )}
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}
