import { useId } from 'react'
import type { EventSummary } from '../api/types'
import { formatFullDate } from '../lib/format'
import { EventButton } from './EventButton'
import { Modal } from './Modal'

interface DayOverflowDialogProps {
  date: string
  events: EventSummary[]
  timeZone: string
  onOpenEvent: (id: string) => void
  onClose: () => void
}

/** Lists every event of a day that has more events than fit in its cell (FR-008). */
export function DayOverflowDialog({ date, events, timeZone, onOpenEvent, onClose }: DayOverflowDialogProps) {
  const titleId = useId()

  return (
    <Modal labelledBy={titleId} onCancel={onClose}>
      <h2 id={titleId}>{formatFullDate(date)}</h2>
      <ul className="overflow-list">
        {events.map((event) => (
          <li key={event.id}>
            <EventButton
              event={event}
              timeZone={timeZone}
              tabbable
              onOpen={(id) => {
                onClose()
                onOpenEvent(id)
              }}
            />
          </li>
        ))}
      </ul>
      <div className="dialog-actions">
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}
