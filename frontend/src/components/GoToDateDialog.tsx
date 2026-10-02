import { useId, useRef, useState, type FormEvent } from 'react'
import { isInSupportedRange, isValidDateString, MAX_DATE, MIN_DATE } from '../lib/dates'
import { messageFor } from '../lib/messages'
import { Modal } from './Modal'

interface GoToDateDialogProps {
  initialDate: string
  onGo: (date: string) => void
  onClose: () => void
}

/** Jump to a date with the browser's own date picker, which is the easiest to use on touch (FR-011, research V8). */
export function GoToDateDialog({ initialDate, onGo, onClose }: GoToDateDialogProps) {
  const titleId = useId()
  const fieldId = useId()
  const errorId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const input = inputRef.current!
    const value = input.value
    if (input.validity.badInput || !isValidDateString(value)) {
      setError(messageFor('goToDate.invalid'))
    } else if (!isInSupportedRange(value)) {
      setError(messageFor('goToDate.outOfRange'))
    } else {
      onGo(value)
      return
    }
    input.focus()
  }

  return (
    <Modal labelledBy={titleId} onCancel={onClose} initialFocusRef={inputRef}>
      <form onSubmit={submit} noValidate>
        <h2 id={titleId}>Go to date</h2>
        <div className="field">
          <label htmlFor={fieldId}>Date</label>
          <input
            ref={inputRef}
            id={fieldId}
            type="date"
            min={MIN_DATE}
            max={MAX_DATE}
            defaultValue={initialDate}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? errorId : undefined}
            onChange={() => setError(null)}
          />
          {error && (
            <p id={errorId} className="field-error">
              {error}
            </p>
          )}
        </div>
        <div className="dialog-actions">
          <button type="submit" className="button-primary">
            Go
          </button>
          <button type="button" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Modal>
  )
}
