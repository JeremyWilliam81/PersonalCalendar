import { useEffect, useId, useRef, useState } from 'react'
import type { DateString, FieldErrors, Frequency, Recurrence, RepeatEnd } from '../api/types'
import { addDays, isValidDateString } from '../lib/dates'
import { messageFor } from '../lib/messages'
import {
  WEEKDAYS,
  defaultRecurrence,
  describeRuleFrom,
  intervalUnit,
  monthlyOptions,
  weekdayName,
  weekdayOf,
} from '../lib/recurrence'

interface RepeatFieldsProps {
  /** null: does not repeat. */
  value: Recurrence | null
  /** The date the rule is filled in from: the form's start date, or the series' first date when editing a series. */
  startDate: DateString
  onChange: (value: Recurrence | null) => void
  /** Server errors keyed like `recurrence.interval`. */
  errors?: FieldErrors
}

const FREQUENCIES: { value: Frequency | 'none'; label: string }[] = [
  { value: 'none', label: 'Does not repeat' },
  { value: 'daily', label: 'Daily' },
  { value: 'weekly', label: 'Weekly' },
  { value: 'monthly', label: 'Monthly' },
  { value: 'yearly', label: 'Yearly' },
]

const MAX_INTERVAL = 99
const MAX_COUNT = 999
const DEFAULT_COUNT = 10

function clamp(value: number, max: number): number {
  return Number.isFinite(value) ? Math.min(max, Math.max(1, Math.round(value))) : 1
}

/**
 * The repeat section of the event form (contracts/ui-interaction.md "Repeat section"). Touch first: every choice
 * is one tap on a fingertip-sized target, built from native radios and toggle buttons (research S10).
 */
export function RepeatFields({ value, startDate, onChange, errors = {} }: RepeatFieldsProps) {
  const id = useId()
  const validStart = isValidDateString(startDate)
  // Weekdays follow the start date until the user picks them (FR-002, FR-003a).
  const weekdaysTouched = useRef(false)

  // Refill the defaults from a new start date, keeping the kind of choice (FR-003a).
  const lastStart = useRef(startDate)
  useEffect(() => {
    if (startDate === lastStart.current || !isValidDateString(startDate)) return
    lastStart.current = startDate
    if (!value) return
    if (value.frequency === 'weekly' && !weekdaysTouched.current) {
      onChange({ ...value, weekdays: [weekdayOf(startDate)] })
    } else if (value.frequency === 'monthly' && value.monthly?.type === 'weekdayPosition') {
      const ordinal = value.monthly.ordinal
      const positions = monthlyOptions(startDate).filter((o) => o.type === 'weekdayPosition')
      const keep = positions.find((o) => o.type === 'weekdayPosition' && o.ordinal === ordinal)
      const next = keep ?? (ordinal === -1 ? positions[positions.length - 1] : positions[0])
      if (next && next.type === 'weekdayPosition' && next.ordinal !== ordinal) {
        onChange({ ...value, monthly: { type: 'weekdayPosition', ordinal: next.ordinal } })
      }
    }
  }, [startDate, value, onChange])

  const setFrequency = (frequency: Frequency | 'none') => {
    weekdaysTouched.current = false
    if (frequency === 'none') onChange(null)
    else onChange({ ...defaultRecurrence(frequency, validStart ? startDate : '2026-01-01'), end: value?.end ?? { type: 'never' } })
  }

  const errorId = (field: string) => `${id}-${field}-error`
  const errorFor = (field: string, extra: string[] = []) => {
    const codes = [...(errors[`recurrence.${field}`] ?? []), ...extra]
    return codes.length ? (
      <p id={errorId(field)} className="field-error">
        {[...new Set(codes.map(messageFor))].join(' ')}
      </p>
    ) : null
  }
  const describedBy = (field: string, extra = false) =>
    errors[`recurrence.${field}`]?.length || extra ? errorId(field) : undefined

  const summary = value && validStart ? describeRuleFrom(value, startDate) : ''
  const announced = useDebounced(summary, 500)

  return (
    <div className="repeat-fields">
      <div role="radiogroup" aria-label="Repeat" className="segmented">
        <span className="segmented-label" aria-hidden="true">
          Repeat
        </span>
        {FREQUENCIES.map((option) => (
          <label key={option.value} className="segmented-option">
            <input
              type="radio"
              name={`${id}-frequency`}
              value={option.value}
              checked={(value?.frequency ?? 'none') === option.value}
              onChange={() => setFrequency(option.value)}
            />
            <span>{option.label}</span>
          </label>
        ))}
      </div>
      {errorFor('frequency')}

      {value && (
        <>
          <Stepper
            label="Repeat every"
            prefix="Every"
            suffix={intervalUnit(value.frequency, value.interval)}
            value={value.interval}
            max={MAX_INTERVAL}
            fewerLabel="Fewer"
            moreLabel="More"
            describedBy={describedBy('interval')}
            onChange={(interval) => onChange({ ...value, interval })}
          />
          {errorFor('interval')}

          {value.frequency === 'weekly' && (
            <>
              <div role="group" aria-label="Repeat on" className="weekday-toggles" aria-describedby={describedBy('weekdays', value.weekdays.length === 0)}>
                {WEEKDAYS.map((day) => {
                  const pressed = value.weekdays.includes(day)
                  return (
                    <button
                      key={day}
                      type="button"
                      className="weekday-toggle"
                      aria-label={weekdayName(day)}
                      aria-pressed={pressed}
                      onClick={() => {
                        weekdaysTouched.current = true
                        const weekdays = pressed ? value.weekdays.filter((d) => d !== day) : [...value.weekdays, day]
                        onChange({ ...value, weekdays: WEEKDAYS.filter((d) => weekdays.includes(d)) })
                      }}
                    >
                      <span aria-hidden="true">{weekdayName(day, 'narrow')}</span>
                    </button>
                  )
                })}
              </div>
              {errorFor('weekdays', value.weekdays.length === 0 ? ['recurrence.weekdays.required'] : [])}
            </>
          )}

          {value.frequency === 'monthly' && validStart && (
            <>
              <div role="radiogroup" aria-label="Monthly on" className="radio-cards" aria-describedby={describedBy('monthly')}>
                {monthlyOptions(startDate).map((option) => {
                  const checked =
                    option.type === 'dayOfMonth'
                      ? value.monthly?.type !== 'weekdayPosition'
                      : value.monthly?.type === 'weekdayPosition' && value.monthly.ordinal === option.ordinal
                  return (
                    <label key={option.label} className="radio-card">
                      <input
                        type="radio"
                        name={`${id}-monthly`}
                        checked={checked}
                        onChange={() =>
                          onChange({
                            ...value,
                            monthly:
                              option.type === 'dayOfMonth'
                                ? { type: 'dayOfMonth' }
                                : { type: 'weekdayPosition', ordinal: option.ordinal },
                          })
                        }
                      />
                      <span>{option.label}</span>
                    </label>
                  )
                })}
              </div>
              {errorFor('monthly')}
            </>
          )}

          <EndsFields
            id={id}
            end={value.end}
            startDate={validStart ? startDate : undefined}
            errors={errors}
            onChange={(end) => onChange({ ...value, end })}
          />

          <p className="repeat-summary" data-testid="repeat-summary">
            {summary}
          </p>
        </>
      )}
      <span className="visually-hidden" aria-live="polite">
        {announced}
      </span>
    </div>
  )
}

function EndsFields({
  id,
  end,
  startDate,
  errors,
  onChange,
}: {
  id: string
  end: RepeatEnd
  startDate?: DateString
  errors: FieldErrors
  onChange: (end: RepeatEnd) => void
}) {
  const untilErrors = errors['recurrence.until'] ?? []
  const countErrors = errors['recurrence.count'] ?? []
  const defaultUntil = startDate ? addDays(startDate, 30) : ''

  return (
    <div role="radiogroup" aria-label="Ends" className="radio-cards ends">
      <span className="segmented-label" aria-hidden="true">
        Ends
      </span>
      <label className="radio-card">
        <input type="radio" name={`${id}-end`} checked={end.type === 'never'} onChange={() => onChange({ type: 'never' })} />
        <span>Never</span>
      </label>

      <div className="radio-card">
        <label>
          <input
            type="radio"
            name={`${id}-end`}
            checked={end.type === 'until'}
            onChange={() => onChange({ type: 'until', until: defaultUntil })}
          />
          <span>On</span>
        </label>
        {end.type === 'until' && (
          <>
            <input
              type="date"
              aria-label="Ends on date"
              value={end.until}
              aria-invalid={untilErrors.length ? true : undefined}
              aria-describedby={untilErrors.length ? `${id}-until-error` : undefined}
              onChange={(e) => onChange({ type: 'until', until: e.target.value })}
            />
            {untilErrors.length > 0 && (
              <p id={`${id}-until-error`} className="field-error">
                {untilErrors.map(messageFor).join(' ')}
              </p>
            )}
          </>
        )}
      </div>

      <div className="radio-card">
        <label>
          <input
            type="radio"
            name={`${id}-end`}
            checked={end.type === 'count'}
            onChange={() => onChange({ type: 'count', count: DEFAULT_COUNT })}
          />
          <span>After</span>
        </label>
        {end.type === 'count' && (
          <>
            <Stepper
              label="Number of times"
              suffix={end.count === 1 ? 'time' : 'times'}
              value={end.count}
              max={MAX_COUNT}
              fewerLabel="Fewer times"
              moreLabel="More times"
              describedBy={countErrors.length ? `${id}-count-error` : undefined}
              onChange={(count) => onChange({ type: 'count', count })}
            />
            {countErrors.length > 0 && (
              <p id={`${id}-count-error`} className="field-error">
                {countErrors.map(messageFor).join(' ')}
              </p>
            )}
          </>
        )}
      </div>
    </div>
  )
}

/** − value + with fingertip-sized buttons; typing is allowed and kept within 1..max. */
function Stepper({
  label,
  prefix,
  suffix,
  value,
  max,
  fewerLabel,
  moreLabel,
  describedBy,
  onChange,
}: {
  label: string
  prefix?: string
  suffix: string
  value: number
  max: number
  fewerLabel: string
  moreLabel: string
  describedBy?: string
  onChange: (value: number) => void
}) {
  return (
    <div className="stepper">
      {prefix && <span aria-hidden="true">{prefix}</span>}
      <button type="button" className="stepper-button" aria-label={fewerLabel} onClick={() => onChange(clamp(value - 1, max))}>
        −
      </button>
      <input
        type="number"
        inputMode="numeric"
        min={1}
        max={max}
        aria-label={label}
        aria-invalid={describedBy ? true : undefined}
        aria-describedby={describedBy}
        value={value}
        onChange={(e) => onChange(clamp(e.target.valueAsNumber, max))}
      />
      <button type="button" className="stepper-button" aria-label={moreLabel} onClick={() => onChange(clamp(value + 1, max))}>
        +
      </button>
      <span>{suffix}</span>
    </div>
  )
}

function useDebounced<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])
  return debounced
}
