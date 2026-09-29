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
}

export function messageFor(code: string): string {
  return messages[code] ?? 'This value is not valid.'
}
