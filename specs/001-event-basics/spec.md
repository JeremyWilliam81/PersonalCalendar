# Feature Specification: Event Basics

**Feature Branch**: `001-event-basics`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Event basics for a single-user personal calendar. I can create an event with a title, start and end date/time, and optional location and notes. I can edit and delete my events. I can mark an event as all-day. I can see my events in a month view and click an event to see its details. Events persist between sessions. An event's end must be after its start."

## Clarifications

### Session 2026-09-29

- Q: Should your events be stored only on the device you use, or be reachable from any of your devices? → A: Only this device. There is no hosted or remote server and no account, and deleting the device's calendar data removes the events. (Plan-phase follow-up, 2026-09-29: the app's parts may run as local processes on the same machine.)
- Q: If your device's time zone changes, what should happen to timed events you already saved? → A: Follow the device. Timed events keep their moment in time and are shown in the current device zone (9:00 AM Chicago shows as 10:00 AM in New York). All-day events never move.
- Q: How should dates and times be formatted? → A: Follow the device's language and region settings (12- or 24-hour clock, date order, month names).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create an event and see it on the month view (Priority: P1)

As the calendar's only user, I open the calendar, see the current month, create an event with a title and a start and end date/time (plus an optional location and notes), and see it appear on the right day(s) of the month view. When I close the calendar and come back later, the event is still there.

**Why this priority**: Creating events and seeing them is the whole reason for a calendar. Without it, nothing else in this feature is useful. Persistence is part of this story, because a calendar that forgets events has no value.

**Independent Test**: Start with an empty calendar. Create an event and confirm it shows on the correct day in the month view. End the session, start a new one, and confirm the event is still shown with the same details.

**Acceptance Scenarios**:

1. **Given** an empty calendar showing the current month, **When** I create an event titled "Dentist" from 2026-10-14 09:00 to 10:00, **Then** "Dentist" appears on October 14 in the month view with its start time shown.
2. **Given** I am creating an event, **When** I set the end to the same moment as the start or earlier, **Then** the event is not saved and I see a message saying the end must be after the start.
3. **Given** I am creating an event, **When** I leave the title empty or enter only spaces, **Then** the event is not saved and I see a message saying a title is required.
4. **Given** I created events in an earlier session, **When** I open the calendar in a new session, **Then** all of those events are shown with the details I saved.
5. **Given** I am viewing October 2026, **When** I go to the next month, previous month, or back to today, **Then** the view shows that month and its events.
6. **Given** I pick a day in the month view to start a new event, **When** the creation form opens, **Then** the start date is already set to that day.

---

### User Story 2 - View an event's details (Priority: P1)

I select an event in the month view and see all of its details: title, start and end date/time (or dates if all-day), location, and notes.

**Why this priority**: The month view has room only for a short label. Details such as location and notes can only be read here. Editing and deleting (Story 3) also start from this view.

**Independent Test**: With one saved event that has every field filled in, select it in the month view and confirm every field is shown correctly. Then close the details and confirm I am back on the same month.

**Acceptance Scenarios**:

1. **Given** an event with a title, times, location, and notes, **When** I select it in the month view, **Then** I see its title, full start and end date and time, location, and notes.
2. **Given** an event with no location or notes, **When** I view its details, **Then** the empty fields are left out and no blank labels are shown.
3. **Given** I am viewing an event's details, **When** I close them, **Then** I return to the month view I was on.

---

### User Story 3 - Edit and delete events (Priority: P2)

I change any field of an event I created, or delete an event I no longer need.

**Why this priority**: Plans change. Without editing and deleting, the calendar quickly becomes wrong. It comes after P1 because creating and viewing already deliver value on their own.

**Independent Test**: Create an event, change its title, time, and location, and confirm the changes show in the month view and details and are still there in a new session. Delete the event and confirm it is gone, including after a new session.

**Acceptance Scenarios**:

1. **Given** a saved event, **When** I change its title and times and save, **Then** the month view and the details show the new values, and the event moves to the new day if the date changed.
2. **Given** I am editing an event, **When** I set the end to the start time or earlier, **Then** the change is not saved, the event keeps its earlier values, and I see a message saying the end must be after the start.
3. **Given** I am editing an event and have made changes, **When** I cancel, **Then** I am asked to confirm discarding the changes, and after I confirm, the event is unchanged.
4. **Given** a saved event, **When** I choose to delete it, **Then** I am asked to confirm. After I confirm, the event is removed from the month view and does not come back in later sessions.
5. **Given** I am asked to confirm a delete, **When** I cancel, **Then** the event is kept unchanged.

---

### User Story 4 - All-day events (Priority: P2)

I mark an event as all-day, such as a birthday or a vacation. It then has only dates, no times, and it can span several days.

**Why this priority**: All-day events are common, but timed events already cover the core need.

**Independent Test**: Create an all-day event for one day and another from Oct 20 to Oct 23. Confirm that both show as all-day on each day they cover, that no times are shown, and that both are unchanged in a new session.

**Acceptance Scenarios**:

1. **Given** I am creating an event, **When** I mark it as all-day, **Then** I enter only a start date and an end date, with no times.
2. **Given** an all-day event with the same start and end date, **When** I save it, **Then** it is accepted as a one-day event and appears on that day only.
3. **Given** an all-day event from 2026-10-20 to 2026-10-23, **When** I view October, **Then** it appears as all-day on the 20th, 21st, 22nd, and 23rd, and it is shown separately from timed events on those days.
4. **Given** an all-day event, **When** I set its end date before its start date, **Then** it is not saved and I see a message saying the end must be on or after the start.
5. **Given** a timed event, **When** I edit it and mark it as all-day, **Then** its start and end dates are kept and its times are removed. When I switch it back to timed, I must provide times, and the end must be after the start.

---

### Edge Cases

- **Events spanning midnight**: A timed event from 22:00 on Oct 14 to 01:00 on Oct 15 appears on both days in the month view.
- **Events spanning months**: An event from Oct 30 to Nov 2 appears on the right days in both months. It also appears on the leading and trailing days from the next or previous month that the month grid shows.
- **Leap years**: Events on February 29 (e.g., 2028-02-29) can be created, are shown on that day, and persist correctly.
- **DST spring-forward gap**: If I enter a start or end time that does not exist in my time zone (e.g., 02:30 on the day clocks jump from 02:00 to 03:00), the time is moved forward by the length of the gap (to 03:30). I am shown the adjusted time before the event is saved.
- **DST fall-back overlap**: If I enter a time that occurs twice (e.g., 01:30 on the day clocks fall back), the earlier of the two occurrences is used.
- **End after start across DST**: The end-after-start rule compares the actual moments in time, not the clock readings. For example, a timed event from 01:30 (second occurrence) to 01:45 (first occurrence) is rejected.
- **All-day events and DST or time zones**: An all-day event stays on the same calendar dates whatever the DST changes or the time zone, and is never shifted to a neighboring day.
- **Device time zone changes**: When the device moves to another time zone, saved timed events show at the equivalent local time in the new zone. This can move an event onto a different day in the month view (e.g., an event at 11:00 PM on Oct 14 in Los Angeles shows at 2:00 AM on Oct 15 in New York).
- **Many events on one day**: If a day has more events than fit in its cell, the cell shows as many as fit plus an "N more" indicator. Selecting the indicator reveals all of that day's events.
- **Long text**: Long titles are cut short in the month view but shown in full in the details. Inputs over the length limits (FR-004) are refused with a clear message.
- **Saving fails**: If an event cannot be saved (e.g., the storage is full or unavailable), I am told that the save failed. My entered data stays in the form so I can try again, and the calendar never shows an event as saved when it was not.

## Requirements *(mandatory)*

### Functional Requirements

**Creating and validating events**

- **FR-001**: Users MUST be able to create an event with a required title, a start, an end, an optional location, and optional notes.
- **FR-002**: Users MUST be able to mark an event as all-day. An all-day event has a start date and an end date (both included) and no times. A timed event has a start date and time and an end date and time.
- **FR-003**: The system MUST reject a timed event whose end is not strictly after its start, comparing actual moments in time. It MUST reject an all-day event whose end date is before its start date. Each rejection MUST show a clear message next to the field at fault.
- **FR-004**: The system MUST require a title that has at least one non-space character. Titles are limited to 200 characters, locations to 200 characters, and notes to 5,000 characters.
- **FR-005**: When I start creating an event from a chosen day, the start date MUST be preset to that day. The default for a new timed event MUST be the next whole hour on that day, lasting one hour.

**Viewing**

- **FR-006**: The system MUST show a month view with a grid of weeks that covers the whole month. It MUST open on the current month and have controls for the previous month, the next month, and today.
- **FR-007**: The month view MUST show each event on every day it covers. Each event MUST show its title, and timed events MUST also show their start time. All-day events MUST look different from timed events, and events MUST be sorted within a day (all-day events first, then timed events by start time).
- **FR-008**: When a day has more events than fit in its cell, the month view MUST show an "N more" indicator. The indicator MUST give access to all of that day's events.
- **FR-009**: Selecting an event MUST show its details: title, full start and end (dates only for all-day events), location, and notes. Empty optional fields MUST be left out.

**Editing and deleting**

- **FR-010**: Users MUST be able to edit every field of an existing event, including switching between all-day and timed. The same validation as for creating an event applies.
- **FR-011**: Users MUST be able to delete an event after confirming. A deleted event cannot be recovered.
- **FR-012**: If the user cancels an edit or creation after making changes, the system MUST ask them to confirm discarding the changes.

**Persistence and time**

- **FR-013**: The system MUST keep all events between sessions on the device where they were created. A save the system reports as successful MUST NOT be lost when the calendar is closed and reopened. Events are stored only on that device, with no hosted or remote server, account, or sign-in.
- **FR-014**: If a save fails, the system MUST tell the user and keep the data they entered. It MUST NOT show an unsaved event as saved.
- **FR-015**: Timed events MUST be entered and shown in the device's current time zone. An event MUST keep the same moment in time when the calendar reloads. If the device's time zone changes, saved timed events MUST keep their moment in time and be shown in the new zone. For example, an event saved as 9:00 AM in America/Chicago is shown as 10:00 AM in America/New_York.
- **FR-016**: All-day events MUST keep their calendar dates exactly as entered, whatever the time zone or DST changes.
- **FR-017**: The system MUST handle times that don't exist or occur twice during DST changes as described in Edge Cases, and MUST show the user any adjusted time before saving.
- **FR-021**: Dates and times MUST be displayed and entered in the format set by the device's language and region settings, including 12- or 24-hour clock, date order, and month and weekday names. Examples in this spec use US English.

**Accessibility**

- **FR-018**: Every action in this feature MUST work with the keyboard alone: moving between months, moving between days in the month grid with the arrow keys, opening an event, creating, editing, deleting, and confirming or canceling. Focus MUST always be visible.
- **FR-019**: Every control and form field MUST have an accessible name. Validation errors, successful saves, and deletes MUST be announced to screen reader users.
- **FR-020**: Screen readers MUST announce dates and times in full, in the device's format (e.g., in US English, "Wednesday, October 14, 2026, 9:00 AM to 10:00 AM"), not only in their shortened visual form.

### Key Entities

- **Event**: One item on the calendar. Its attributes are a title (required), an all-day flag, a start and an end, a location (optional), and notes (optional). A timed event's start and end are fixed moments in time, entered and shown in the device's current time zone. An all-day event's start and end are calendar dates, with the end date included. Each event also records when it was created and when it was last changed.
- **User time zone**: The device's current time zone, identified by region (e.g., "America/Chicago") rather than by a fixed offset. It is read from the device each session and is not a saved setting. Timed events are entered and shown in this zone.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can create a basic timed event (title plus start and end) in under 30 seconds from the month view.
- **SC-002**: 100% of events that were saved successfully are still present, with identical details, after the calendar is closed and reopened.
- **SC-003**: 100% of attempts to save an event whose end is not after its start (or, for all-day events, whose end date is before its start date) are rejected with a message that names the problem.
- **SC-004**: The month view, including all its events, appears within 1 second of opening the calendar or changing months, for a calendar with up to 5,000 events.
- **SC-005**: Every user story in this spec can be completed using only the keyboard, and with a screen reader that announces all dates and times in full.
- **SC-006**: Events on DST-change days, on Feb 29, across midnight, and across month boundaries appear on the correct day(s) at the correct times in 100% of the acceptance scenarios and edge cases listed.

## Assumptions

- **Single user, single device**: There is exactly one user. No sign-in, sharing, or permissions are needed. Events are kept only on the user's device, and the calendar runs only on that device. Syncing between devices, hosted storage, and backup/export are out of scope. If the user deletes the calendar's data on that device, their events are permanently lost, and this is accepted.
- **Time zone**: The calendar always follows the device's current time zone. Choosing a time zone manually, or giving an event its own separate time zone, is out of scope for this feature.
- **Week layout**: Weeks in the month view start on Sunday, even though date and time formats follow the device's region settings. Letting the user choose the first day of the week is out of scope.
- **Language**: The interface text is in English only. Only date and time formats follow the device's region settings.
- **Out of scope**: Recurring events, reminders and notifications, inviting others, importing or exporting calendars (e.g., iCalendar), search, and day and week views are all excluded. Each is left for a later feature.
- **Deleting**: Deletion is permanent. Confirming before a delete is the safeguard, and there is no undo or trash.
- **Title rule**: Leading and trailing spaces are removed from the title before it is checked and saved.
