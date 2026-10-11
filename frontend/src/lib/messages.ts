// Error codes from data-model.md mapped to the English UI text.
const messages: Record<string, string> = {
  'title.required': 'Title is required.',
  'title.tooLong': 'Title must be 200 characters or fewer.',
  'location.tooLong': 'Location must be 200 characters or fewer.',
  'notes.tooLong': 'Notes must be 5,000 characters or fewer.',
  'start.required': 'Start is required.',
  'end.required': 'End is required.',
  'startDate.required': 'Start date is required.',
  'endDate.required': 'End date is required.',
  'start.invalid': 'Enter a valid start date and time.',
  'end.invalid': 'Enter a valid end date and time.',
  'startDate.invalid': 'Enter a valid start date.',
  'endDate.invalid': 'Enter a valid end date.',
  'end.notAfterStart': 'End must be after start.',
  'endDate.beforeStart': 'End date must be on or after the start date.',
  'timeZone.unknown': 'Unknown time zone.',
  'goToDate.invalid': 'Enter a valid date.',
  'goToDate.outOfRange': 'Choose a date between 1900 and 2199.',
  // Recurring events (specs/003-recurring-events/data-model.md).
  'recurrence.frequency.invalid': 'Choose how often the event repeats.',
  'recurrence.interval.outOfRange': 'Enter a number from 1 to 99.',
  'recurrence.weekdays.required': 'Choose at least one weekday.',
  'recurrence.weekdays.invalid': 'Choose at least one weekday.',
  'recurrence.monthly.invalid': "That monthly option doesn't match the start date.",
  'recurrence.until.beforeStart': 'End date must be on or after the start date.',
  'recurrence.until.invalid': 'Enter a valid end date.',
  'recurrence.count.outOfRange': 'Enter a number from 1 to 999.',
  'recurrence.end.invalid': 'Choose when the series ends.',
  'scope.thisWithRepeatChange':
    "A single event can't have its own repeat settings. Choose This and following events or All events.",
  'scope.dateChangeRequiresThis': 'A new date can only apply to this event.',
  'scope.dateAndRepeatChanged':
    'A new date applies only to this event, but repeat changes apply to the series. Undo one of them.',
}

export function messageFor(code: string): string {
  return messages[code] ?? 'This value is not valid.'
}
