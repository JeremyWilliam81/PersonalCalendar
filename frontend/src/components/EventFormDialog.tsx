import { useId, useRef, useState, type FormEvent } from 'react'
import { useCalendarApi } from '../api/ApiContext'
import type { EventDetails, EventInput, FieldErrors, LocalDateTimeString } from '../api/types'
import { describeEvent } from '../lib/describe'
import { formatFullDate, formatLocalTime, toLocalInputValue } from '../lib/format'
import { messageFor } from '../lib/messages'
import { newEventDefaults } from '../lib/newEventDefaults'
import { ConfirmDialog } from './ConfirmDialog'
import { Modal } from './Modal'
import { useAnnounce } from './useAnnounce'

interface EventFormDialogProps {
  /** Day the event is created on (create mode). */
  initialDate: string
  timeZone: string
  onSaved: (details: EventDetails) => void
  onClose: () => void
  /** Injected for tests; defaults to the current time. */
  now?: Date
  /** The event being edited (edit mode). */
  event?: EventDetails
  /** Start and end for a new event, e.g. from a tapped time slot (FR-013b); overrides the FR-005 defaults. */
  initialTimes?: { start: LocalDateTimeString; end: LocalDateTimeString }
}

interface FormValues {
  title: string
  isAllDay: boolean
  start: string
  end: string
  startDate: string
  endDate: string
  location: string
  notes: string
}

type Field = Exclude<keyof FormValues, 'isAllDay'>

// Order in which the first invalid field receives focus.
const FIELD_ORDER: Field[] = ['title', 'start', 'end', 'startDate', 'endDate', 'location', 'notes']

interface Adjustment {
  adjustedStart: string
  adjustedEnd: string
}

type FormError = 'saveFailed' | 'timeZone' | 'conflict' | 'gone' | null

function newEventValues(initialDate: string, now: Date, initialTimes?: { start: string; end: string }): FormValues {
  const defaults = initialTimes ?? newEventDefaults(initialDate, now)
  return {
    title: '',
    isAllDay: false,
    start: defaults.start,
    end: defaults.end,
    startDate: '',
    endDate: '',
    location: '',
    notes: '',
  }
}

function valuesFromEvent(event: EventDetails, timeZone: string): FormValues {
  return {
    title: event.title,
    isAllDay: event.isAllDay,
    start: event.start ? toLocalInputValue(event.start, timeZone) : '',
    end: event.end ? toLocalInputValue(event.end, timeZone) : '',
    startDate: event.startDate ?? '',
    endDate: event.endDate ?? '',
    location: event.location ?? '',
    notes: event.notes ?? '',
  }
}

function nullIfBlank(value: string): string | null {
  return value.trim() === '' ? null : value
}

export function EventFormDialog({
  initialDate,
  timeZone,
  onSaved,
  onClose,
  now,
  event,
  initialTimes,
}: EventFormDialogProps) {
  const api = useCalendarApi()
  const announce = useAnnounce()
  const titleId = useId()
  const idPrefix = useId()
  const fieldRefs = useRef<Partial<Record<Field, HTMLInputElement | HTMLTextAreaElement | null>>>({})
  const titleRef = useRef<HTMLElement | null>(null)

  // The event as last loaded; its version guards against overwriting changes made elsewhere (R8).
  const [current, setCurrent] = useState<EventDetails | undefined>(event)
  const [initial, setInitial] = useState(() =>
    event ? valuesFromEvent(event, timeZone) : newEventValues(initialDate, now ?? new Date(), initialTimes),
  )
  const [values, setValues] = useState<FormValues>(initial)
  const [errors, setErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<FormError>(null)
  const [adjustment, setAdjustment] = useState<Adjustment | null>(null)
  const [confirmingDiscard, setConfirmingDiscard] = useState(false)
  const [saving, setSaving] = useState(false)

  const dirty = (Object.keys(initial) as (keyof FormValues)[]).some((key) => values[key] !== initial[key])

  const fieldId = (field: Field) => `${idPrefix}-${field}`
  const errorId = (field: Field) => `${idPrefix}-${field}-error`

  const update = (field: Field, value: string) => {
    setValues((current) => ({ ...current, [field]: value }))
    setAdjustment(null)
  }

  // Switching to all-day keeps the dates; switching back requires new times (User Story 4, scenario 5).
  const setAllDay = (isAllDay: boolean) => {
    setValues((current) => {
      if (!isAllDay) return { ...current, isAllDay, start: '', end: '' }
      const startDate = current.start.slice(0, 10) || current.startDate || initialDate
      const endDate = current.end.slice(0, 10) || current.endDate || startDate
      return { ...current, isAllDay, startDate, endDate }
    })
    setErrors({})
    setAdjustment(null)
  }

  const requestClose = () => {
    if (dirty) setConfirmingDiscard(true)
    else onClose()
  }

  const buildInput = (acceptAdjustedTimes: boolean): EventInput => ({
    title: values.title,
    location: nullIfBlank(values.location),
    notes: nullIfBlank(values.notes),
    isAllDay: values.isAllDay,
    timeZone,
    start: values.isAllDay ? null : values.start || null,
    end: values.isAllDay ? null : values.end || null,
    startDate: values.isAllDay ? values.startDate || null : null,
    endDate: values.isAllDay ? values.endDate || null : null,
    acceptAdjustedTimes,
  })

  const showValidation = (fieldErrors: FieldErrors) => {
    const { timeZone: zoneErrors, ...rest } = fieldErrors
    setErrors(rest)
    setFormError(zoneErrors ? 'timeZone' : null)
    const count = Object.values(fieldErrors).reduce((total, codes) => total + codes.length, 0)
    announce(`${count} ${count === 1 ? 'error' : 'errors'}. Fix the highlighted fields.`)
    const first = FIELD_ORDER.find((field) => rest[field]?.length)
    if (first) fieldRefs.current[first]?.focus()
  }

  const save = async (acceptAdjustedTimes: boolean) => {
    setSaving(true)
    const input = buildInput(acceptAdjustedTimes)
    const result = current
      ? await api.updateEvent(current.id, { ...input, version: current.version })
      : await api.createEvent(input)
    setSaving(false)

    switch (result.kind) {
      case 'ok':
        announce(`Saved ${describeEvent(result.value, result.value.timeZone)}.`)
        onSaved(result.value)
        return
      case 'validation':
        setAdjustment(null)
        showValidation(result.errors)
        return
      case 'dstAdjustment':
        setErrors({})
        setFormError(null)
        setAdjustment({ adjustedStart: result.adjustedStart, adjustedEnd: result.adjustedEnd })
        return
      case 'conflict':
        setAdjustment(null)
        setFormError('conflict')
        announce('This event was changed in another window.')
        return
      case 'notFound':
        setAdjustment(null)
        setFormError('gone')
        announce('This event no longer exists.')
        return
      default:
        setAdjustment(null)
        setFormError('saveFailed')
        announce("Couldn't save the event.")
    }
  }

  const reloadEvent = async () => {
    if (!current) return
    const result = await api.getEvent(current.id, timeZone)
    if (result.kind !== 'ok') {
      setFormError(result.kind === 'notFound' ? 'gone' : 'saveFailed')
      return
    }
    const reloaded = valuesFromEvent(result.value, timeZone)
    setCurrent(result.value)
    setInitial(reloaded)
    setValues(reloaded)
    setErrors({})
    setFormError(null)
    announce('Reloaded the latest version of the event.')
    titleRef.current?.focus()
  }

  const handleSubmit = (submitEvent: FormEvent) => {
    submitEvent.preventDefault()
    if (!saving) void save(false)
  }

  const adjustmentMessage = adjustment ? describeAdjustment(values, adjustment) : null

  const inputProps = (field: Field) => {
    const fieldErrors = errors[field]
    return {
      id: fieldId(field),
      ref: (element: HTMLInputElement | HTMLTextAreaElement | null) => {
        fieldRefs.current[field] = element
        if (field === 'title') titleRef.current = element
      },
      'aria-invalid': fieldErrors?.length ? true : undefined,
      'aria-describedby': fieldErrors?.length ? errorId(field) : undefined,
    }
  }

  const fieldError = (field: Field) =>
    errors[field]?.length ? (
      <p id={errorId(field)} className="field-error">
        {errors[field].map(messageFor).join(' ')}
      </p>
    ) : null

  return (
    <Modal labelledBy={titleId} onCancel={requestClose} initialFocusRef={titleRef}>
      <form className="event-form" noValidate onSubmit={handleSubmit}>
        <h2 id={titleId}>{current ? 'Edit event' : 'New event'}</h2>

        {formError === 'saveFailed' && (
          <p className="form-error">Couldn&apos;t save the event. Your changes are still here. Try again.</p>
        )}
        {formError === 'timeZone' && <p className="form-error">{messageFor('timeZone.unknown')}</p>}
        {formError === 'conflict' && (
          <div className="form-error">
            <p>This event was changed in another window.</p>
            <button type="button" onClick={() => void reloadEvent()}>
              Reload event
            </button>
          </div>
        )}
        {formError === 'gone' && <p className="form-error">This event no longer exists.</p>}

        <div className="field">
          <label htmlFor={fieldId('title')}>Title</label>
          <input
            {...inputProps('title')}
            type="text"
            required
            maxLength={200}
            value={values.title}
            onChange={(e) => update('title', e.target.value)}
          />
          {fieldError('title')}
        </div>

        <div className="checkbox-field">
          <input
            id={`${idPrefix}-all-day`}
            type="checkbox"
            checked={values.isAllDay}
            onChange={(e) => setAllDay(e.target.checked)}
          />
          <label htmlFor={`${idPrefix}-all-day`}>All day</label>
        </div>

        {values.isAllDay ? (
          <div className="field-row">
            <div className="field">
              <label htmlFor={fieldId('startDate')}>Start date</label>
              <input
                {...inputProps('startDate')}
                type="date"
                required
                value={values.startDate}
                onChange={(e) => update('startDate', e.target.value)}
              />
              {fieldError('startDate')}
            </div>
            <div className="field">
              <label htmlFor={fieldId('endDate')}>End date</label>
              <input
                {...inputProps('endDate')}
                type="date"
                required
                value={values.endDate}
                onChange={(e) => update('endDate', e.target.value)}
              />
              {fieldError('endDate')}
            </div>
          </div>
        ) : (
          <div className="field-row">
            <div className="field">
              <label htmlFor={fieldId('start')}>Start</label>
              <input
                {...inputProps('start')}
                type="datetime-local"
                required
                value={values.start}
                onChange={(e) => update('start', e.target.value)}
              />
              {fieldError('start')}
            </div>
            <div className="field">
              <label htmlFor={fieldId('end')}>End</label>
              <input
                {...inputProps('end')}
                type="datetime-local"
                required
                value={values.end}
                onChange={(e) => update('end', e.target.value)}
              />
              {fieldError('end')}
            </div>
          </div>
        )}

        {adjustmentMessage && (
          <div className="adjustment">
            <p>{adjustmentMessage}</p>
            <div className="dialog-actions">
              <button type="button" className="button-primary" onClick={() => void save(true)}>
                Save with adjusted time
              </button>
              <button
                type="button"
                onClick={() => {
                  setAdjustment(null)
                  fieldRefs.current.start?.focus()
                }}
              >
                Change time
              </button>
            </div>
          </div>
        )}

        <div className="field">
          <label htmlFor={fieldId('location')}>Location</label>
          <input
            {...inputProps('location')}
            type="text"
            maxLength={200}
            value={values.location}
            onChange={(e) => update('location', e.target.value)}
          />
          {fieldError('location')}
        </div>

        <div className="field">
          <label htmlFor={fieldId('notes')}>Notes</label>
          <textarea
            {...inputProps('notes')}
            rows={4}
            maxLength={5000}
            value={values.notes}
            onChange={(e) => update('notes', e.target.value)}
          />
          {fieldError('notes')}
        </div>

        <div className="dialog-actions">
          <button type="submit" className="button-primary" disabled={saving}>
            Save
          </button>
          <button type="button" onClick={requestClose}>
            Cancel
          </button>
        </div>
      </form>

      {confirmingDiscard && (
        <ConfirmDialog
          title="Discard your changes?"
          confirmLabel="Discard"
          cancelLabel="Keep editing"
          initialFocus="cancel"
          onConfirm={onClose}
          onCancel={() => setConfirmingDiscard(false)}
        />
      )}
    </Modal>
  )
}

/** "2:30 AM doesn't exist on Sunday, March 14, 2027 because of the daylight saving time change. …" (FR-017) */
function describeAdjustment(values: FormValues, adjustment: Adjustment): string {
  const startMoved = adjustment.adjustedStart !== values.start
  const original = startMoved ? values.start : values.end
  const adjusted = startMoved ? adjustment.adjustedStart : adjustment.adjustedEnd
  return (
    `${formatLocalTime(original)} doesn't exist on ${formatFullDate(original.slice(0, 10))} ` +
    `because of the daylight saving time change. ` +
    `The event will ${startMoved ? 'start' : 'end'} at ${formatLocalTime(adjusted)}.`
  )
}
