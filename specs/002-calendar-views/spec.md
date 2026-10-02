# Feature Specification: Calendar Views and Navigation

**Feature Branch**: `002-calendar-views`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Calendar navigation and views. I can switch between day, week, and month views of my events. In each view I can move to the previous or next period, jump back to today, and jump to a specific date. The current day is visually highlighted. Clicking a day in the month view opens that day in the day view. The view I'm on is reflected in the URL so I can refresh or bookmark it. Everything is usable with the keyboard."

## Clarifications

### Session 2026-10-02

- Q: In feature 001, choosing a day in the month view starts a new event on that day. Now that choosing a day opens the day view, how should creating an event on a day work? → A: *(Superseded by the next answer.)* Split the day cell: the date number opens the day view and the empty part of the cell starts a new event.
- Priority set by the user: Clicking and touchscreen use come first. Keyboard use must still work, to the baseline the constitution requires (Principle IV), but it is secondary. Extra keyboard-only features, such as a set of shortcuts, are not required.
- Q: On a touchscreen, what should tapping a day in the month view do, and how should a new event be started on a day? → A: Tapping or clicking anywhere on a day opens it in the day view. An always-visible "New event" button in every view starts an event on the selected date. In the day and week views, tapping or clicking an empty time slot starts an event at that time.
- Q: On a touchscreen, should swiping left or right on the calendar move to the next or previous day, week, or month? → A: Yes, in every view. The Previous and Next buttons stay visible, and vertical scrolling in the day and week views is unaffected.
- Q: How should the week view look on a phone-sized screen (narrower than about 600 px)? → A: As seven stacked day sections, each listing its events in time order with all-day events first. Tapping a day's heading opens it in the day view. Wider screens use the hourly grid.
- Q: On a phone-sized screen, what should each day in the month view show about its events? → A: Small markers, one per event up to 3, then "+N", with all-day events marked differently. Tapping the day opens the day view, and screen readers hear the count (e.g., "3 events"). Wider screens keep the labeled events from feature 001.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Switch between day, week, and month views (Priority: P1)

I can look at my events one day at a time, one week at a time, or one month at a time, and switch between these views whenever I want. The new view shows the period that contains the date I was looking at, so I don't lose my place.

**Why this priority**: The month view from feature 001 shows only short labels. Day and week views show when events happen and how long they last, which is how I plan a day or a week. Without them, the rest of this feature has nothing to navigate.

**Independent Test**: With events saved on several days of one week, open the month view, switch to the week view and then to the day view, and confirm each view shows the right period with the right events at the right times. Switch back to month and confirm the same month is shown.

**Acceptance Scenarios**:

1. **Given** I am on the month view for October 2026 with Wednesday, October 14 as the selected date, **When** I switch to the week view, **Then** I see Sunday, October 11 through Saturday, October 17, 2026, with each day's events.
2. **Given** I am on the week view for October 11–17, 2026 with October 14 as the selected date, **When** I switch to the day view, **Then** I see Wednesday, October 14, 2026 and its events.
3. **Given** I am on the day view for October 14, 2026, **When** I switch to the month view, **Then** I see October 2026.
4. **Given** a timed event from 9:00 AM to 10:30 AM, **When** I view its day in the day or week view (the week view on a wider screen), **Then** the event is placed on a time-of-day scale so that its position and size show when it starts and ends, and it shows its title and time range.
5. **Given** a day with an all-day event and timed events, **When** I view that day in the day or week view, **Then** the all-day event is shown in a separate all-day area above the timed events.
6. **Given** two timed events that overlap in time on the same day, **When** I view that day in the day or week view, **Then** both events are fully visible side by side and neither hides the other.
7. **Given** I am in the day or week view, **When** I select an event, **Then** its details open, just as they do in the month view.
8. **Given** I am on the week view on a phone-sized screen, **When** the view shows October 11–17, 2026, **Then** I see seven stacked sections, one per day, each listing that day's events in time order (all-day first, then timed with their time range). **When** I tap the heading "Wednesday, October 14", **Then** the day view opens for October 14.
9. **Given** the week view on a phone-sized screen, **When** a day has no events, **Then** its section is still shown, with a short "No events" note.

---

### User Story 2 - Move through time and jump to a date (Priority: P1)

In whichever view I'm on, I can step to the previous or next period (day, week, or month), jump back to today, or jump straight to any date I choose.

**Why this priority**: A view that is stuck on one period is not useful. Moving through time is how I find past and upcoming events. It is equal in priority to Story 1 because each view is useless without it.

**Independent Test**: In each of the three views, step forward and back, jump to today, and jump to a chosen date. Confirm that each action shows the expected period and its events.

**Acceptance Scenarios**:

1. **Given** I am on the day view for October 14, 2026, **When** I go to the next period, **Then** I see October 15, 2026. **When** I go to the previous period twice, **Then** I see October 13, 2026.
2. **Given** I am on the week view for October 11–17, 2026, **When** I go to the next period, **Then** I see October 18–24, 2026.
3. **Given** I am on the week view for December 27, 2026 – January 2, 2027, **When** I go to the next period, **Then** I see January 3–9, 2027.
4. **Given** I am on the month view for January 2026 with January 31 selected, **When** I go to the next period, **Then** I see February 2026, and the selected date becomes February 28, 2026.
5. **Given** I am on any view showing a period in the past or future, **When** I choose "Today", **Then** the same view shows the period containing today, and today becomes the selected date.
6. **Given** I am on the week view, **When** I jump to March 3, 2027, **Then** I stay on the week view and see the week of February 28 – March 6, 2027, with March 3 as the selected date.
7. **Given** I am choosing a date to jump to, **When** I enter something that is not a valid date (e.g., February 30), **Then** the view does not change and I see a message saying the date is not valid.
8. **Given** I have just moved to a new period, **When** the view updates, **Then** the period's title (e.g., "October 2026", "October 11 – 17, 2026", or "Wednesday, October 14, 2026") shows which period I am on.
9. **Given** I am on the week view for October 11–17, 2026 on a touchscreen, **When** I swipe left, **Then** I see October 18–24, 2026. **When** I swipe right, **Then** I am back on October 11–17, 2026.
10. **Given** I am on the day view on a touchscreen, **When** I scroll up or down through the hours, **Then** the view scrolls and the period does not change, even if my finger drifts slightly sideways.

---

### User Story 3 - Open a day from the month view (Priority: P2)

When I see something on a day in the month view that I want to look at more closely, I choose that day and it opens in the day view.

**Why this priority**: It is the fastest way from the month overview to the detail of one day. Stories 1 and 2 already make every day reachable, so this is a shortcut, not a necessity.

**Independent Test**: On the month view, choose a day with several events and confirm the day view opens for exactly that date with all its events. Use the browser's back action and confirm the month view returns.

**Acceptance Scenarios**:

1. **Given** I am on the month view for October 2026, **When** I choose October 14, **Then** the day view opens for Wednesday, October 14, 2026.
2. **Given** I am on the month view for October 2026, **When** I choose a day shown from a neighboring month (e.g., November 1), **Then** the day view opens for that date (November 1, 2026).
3. **Given** I opened a day from the month view, **When** I use the browser's back action, **Then** I return to the month view for the month I was on.
4. **Given** I am on the month view on a phone, **When** I tap anywhere on October 14 other than one of its events (the date number or empty space), **Then** the day view opens for October 14.
5. **Given** I am on any view with October 14 as the selected date, **When** I tap or click "New event", **Then** the form for a new event opens with the start date set to October 14.
6. **Given** I am on the day view for October 14, **When** I tap or click the empty 2:00 PM slot, **Then** the form for a new event opens, starting at 2:00 PM on October 14 and lasting one hour.
7. **Given** I am on the month view on a phone-sized screen and October 14 has 5 events, one of them all-day, **When** I look at October 14, **Then** I see three event markers, one of them styled as all-day, followed by "+2". A screen reader announces "Wednesday, October 14, 2026, 5 events". **When** I tap anywhere on the day, including on a marker, **Then** the day view opens for October 14.

---

### User Story 4 - See where today is (Priority: P2)

Whichever view I'm on, I can tell at a glance which day is today, if it is in the period shown.

**Why this priority**: It orients me instantly, but every other part of this feature works without it.

**Independent Test**: With today's date fixed, open each view on a period that contains today and confirm today is highlighted. Open a period that does not contain today and confirm no day is highlighted as today.

**Acceptance Scenarios**:

1. **Given** today is October 14, 2026, **When** I view October 2026 in the month view, **Then** October 14 is highlighted as today, and no other day is.
2. **Given** today is October 14, 2026, **When** I view the week of October 11–17 or the day October 14, **Then** October 14 is highlighted as today.
3. **Given** today is October 14, 2026, **When** I view November 2026, **Then** no day is highlighted as today.
4. **Given** the calendar is open across midnight, **When** the date changes, **Then** the highlight moves to the new day without my having to reload the calendar.
5. **Given** I use a screen reader, **When** I reach today's date in any view, **Then** it is announced as today in addition to its full date.

---

### User Story 5 - Refresh and bookmark the view I'm on (Priority: P2)

The address of the page shows which view and which date I am looking at. If I refresh the page, or open a bookmark or saved link, I see that same view and date again.

**Why this priority**: It saves me from getting lost on refresh and lets me bookmark views I use often. The calendar works without it, but it is jarring to be sent back to the current month on every refresh.

**Independent Test**: Go to the week view for a past week, refresh, and confirm the same week view is shown. Copy the address, open it in a new tab, and confirm the same view and week appear. Edit the address by hand to a different date and view and confirm the calendar shows what it names.

**Acceptance Scenarios**:

1. **Given** I am on the week view for October 11–17, 2026, **When** I refresh the page, **Then** I see the week view for October 11–17, 2026.
2. **Given** I bookmarked the day view for March 3, 2027, **When** I open the bookmark later, **Then** I see the day view for March 3, 2027.
3. **Given** I have a saved link that names a view but no date, **When** I open it, **Then** I see that view for the period containing today, so a bookmark for "this week" always shows the current week.
4. **Given** I open the calendar's address with no view or date, **When** it loads, **Then** I see the month view for the current month (as in feature 001).
5. **Given** I open an address whose view or date is not valid (e.g., an unknown view name or a date such as 2026-02-30), **When** it loads, **Then** I see the month view for the current month, I am told the link could not be understood, and the address is corrected to match what is shown.
6. **Given** I have moved through several views and periods, **When** I use the browser's back and forward actions, **Then** I step through the views and periods I visited, in order.

---

### Edge Cases

- **Events spanning midnight**: In the day and week views, a timed event from 10:00 PM on October 14 to 1:00 AM on October 15 appears at the end of October 14 and at the start of October 15. Each part shows the event's full start and end in its details.
- **Events spanning views**: A multi-day all-day event (e.g., October 16–20) appears in the all-day area of each day it covers. In the week view it appears as one continuous bar across the days of the week that it covers, and it is cut off clearly at the edges of the week.
- **DST spring-forward day**: On a day when clocks jump from 2:00 AM to 3:00 AM, the day and week views show the day as 23 hours long. No hour is shown for 2:00–3:00 AM, and events are placed at their correct local times.
- **DST fall-back day**: On a day when clocks fall back from 2:00 AM to 1:00 AM, the day and week views show the day as 25 hours long. The 1:00 AM hour appears twice, and events in each occurrence of that hour are placed in the correct one.
- **Leap years**: Moving forward one day from February 28, 2028 shows February 29, 2028. Moving forward one month from January 31, 2028 shows February 2028 with February 29 selected. Jumping to February 29, 2027 is rejected as not a valid date.
- **Month-length clamping**: When moving by month from a date that doesn't exist in the target month (e.g., March 31 → April), the selected date becomes the last day of the target month (April 30).
- **Year boundaries**: Moving between December and January, and showing a week that spans two years (e.g., December 27, 2026 – January 2, 2027), shows the correct dates and events, and the period title names both years.
- **Very early and very late dates**: Navigation and jumping work for any date from January 1, 1900 to December 31, 2199. Dates outside that range are rejected as out of range when jumping, and the previous/next controls stop at the ends of the range.
- **Device time zone changes while the calendar is open**: The selected date stays the same calendar date. Timed events are shown in the new zone, following feature 001 (FR-015), and the "today" highlight follows the new zone's current date.
- **Many events on one day in the day or week view**: When many events overlap in the same time, every event stays reachable. Events too narrow to show their title still open their details when tapped. A "+N" control on a crowded cluster of overlapping events lists every event in it, as full-size rows.
- **Very short events**: A timed event lasting only a few minutes is still shown large enough to see and select.
- **Long or busy day**: The day and week views can be scrolled to see all hours of the day. When a view opens on a day that contains today, it initially shows the current time. Otherwise, it initially shows the earliest timed event, or 8:00 AM if there are no timed events.
- **Link from another time zone or device**: A saved link names calendar dates, not moments in time. Opening it always shows that calendar date in the device's current time zone.

## Requirements *(mandatory)*

### Functional Requirements

**Views**

- **FR-001**: The system MUST offer three views of events: day, week, and month. The user MUST be able to switch between them at any time with a clearly labeled control that shows which view is active.
- **FR-002**: The system MUST keep a single selected date across all views. Switching views MUST show the period that contains the selected date, and the selected date MUST NOT change when switching views.
- **FR-003**: The day view MUST show one day. The week view MUST show seven consecutive days starting on Sunday, matching the month view's week layout from feature 001. On wider screens, the month view MUST behave as described in feature 001.
- **FR-003a**: On phone-sized screens (see FR-004a), each day in the month view MUST show small markers instead of event titles: one marker per event, up to 3, then a "+N" count for the rest. Markers for all-day events MUST look different from timed events by shape, not color alone. Markers are not separate tap targets: tapping anywhere on the day opens the day view (FR-013). Each day's accessible name MUST include its event count (e.g., "Wednesday, October 14, 2026, 5 events"), or "no events".
- **FR-004**: The day view, and the week view on wider screens (see FR-004a), MUST show timed events on a time-of-day scale covering the whole day. Each event's position and size MUST reflect its start and end, and it MUST show its title and start time when there is room. Overlapping events MUST be placed side by side so that each one is visible and selectable.
- **FR-004a**: On screens narrower than about 600 pixels (phone-sized), the week view MUST instead show the seven days as stacked sections, from Sunday to Saturday. Each section has the day's full date as a heading and lists its events in time order: all-day events first, then timed events with their start and end times. Days without events show "No events". Tapping or clicking a day's heading MUST open that day in the day view. Tapping an event MUST open its details. Multi-day events appear in each day they cover. Turning or resizing the screen across the width threshold MUST switch layouts without changing the view or the selected date.
- **FR-005**: Wherever the time-of-day scale is shown, all-day events MUST appear in a separate area above it. In the week view, an all-day event covering several days MUST appear as one continuous item across those days.
- **FR-006**: Selecting an event in any view MUST open its details, as in feature 001 (FR-009). Editing or deleting it MUST update the current view without changing the view or the selected date.
- **FR-007**: Each view MUST show a title for the period it covers: the full date for the day view, the date range for the week view (naming both months and both years when the week crosses them), and the month and year for the month view.

**Navigation**

- **FR-008**: Every view MUST have visible Previous and Next buttons that go to the previous and next period. A period is one day in the day view, one week in the week view, and one month in the month view.
- **FR-008a**: On touchscreens, swiping horizontally in any view MUST go to the next period (swipe left) or the previous period (swipe right), exactly as the Next and Previous buttons do. A swipe counts only when it is clearly horizontal, so that vertical scrolling in the day and week views never changes the period by accident. The swipe MUST NOT be the only way to navigate.
- **FR-009**: Moving by day or week MUST move the selected date by 1 or 7 calendar days. Moving by month MUST keep the same day number, or use the last day of the target month if that day doesn't exist.
- **FR-010**: Every view MUST have a "Today" control that shows the period containing today in the same view and makes today the selected date.
- **FR-011**: Every view MUST let the user jump to a specific date by entering or choosing it. The view MUST stay the same, show the period containing that date, and make it the selected date. A date that doesn't exist (e.g., February 30) or is outside the supported range MUST be rejected with a clear message and MUST NOT change the view.
- **FR-012**: Navigation MUST support every date from January 1, 1900 to December 31, 2199. The previous and next controls MUST be unavailable when they would leave that range.
- **FR-013**: In the month view, tapping or clicking anywhere on a day other than one of its event labels (on wider screens) MUST open that date in the day view. This includes days shown from the previous or next month. The whole day cell is the target. Each day's accessible name MUST say that it opens the day (e.g., "Wednesday, October 14, 2026, open in day view"). With the keyboard, Enter or Space on a focused day does the same. This replaces feature 001's behavior, where choosing a day started a new event.
- **FR-013a**: Every view MUST show a "New event" button at all times. It starts a new event on the selected date, using the defaults from feature 001 (FR-005).
- **FR-013b**: Wherever the time-of-day scale is shown, tapping or clicking an empty time slot MUST start a new event on that day. The event starts at the beginning of that slot (slots are 30 minutes long) and lasts one hour.
- **FR-013c**: Every tappable control, day cell, and time slot MUST present a touch target of at least 44 × 44 pixels (about the size of a fingertip), or be spaced so that neighboring targets cannot be hit by mistake. One exception: event blocks in the day and week views MAY be smaller when their duration or overlap leaves less room, as long as the block itself or a full-size "+N" list still gives access to them (see Edge Cases).

**Today**

- **FR-014**: In every view, today's date MUST be visibly highlighted when it is in the period shown, and no other date MUST be highlighted as today. The highlight MUST NOT rely on color alone.
- **FR-015**: "Today" MUST be the current date in the device's current time zone. If the date changes while the calendar is open (at midnight, or because the time zone changes), the highlight MUST move to the new date within one minute, without a reload, and without changing the view or the selected date.
- **FR-016**: In the day and week views, when today is shown, the current time of day MUST be marked on the time-of-day scale.

**Address (URL)**

- **FR-017**: The page address MUST always reflect the current view and selected date, and MUST be updated whenever either changes.
- **FR-018**: Opening an address that names a view and a date (by refreshing, using a bookmark, or pasting a link) MUST show that view with that date selected.
- **FR-019**: Opening an address that names a view but no date MUST show that view for today. Opening the calendar's address with neither MUST show the month view for today.
- **FR-020**: Opening an address whose view or date is not valid MUST show the month view for today, tell the user that the link could not be understood, and correct the address to match what is shown.
- **FR-021**: Each change of view or period MUST add a step to the browser history, so that the browser's back and forward actions move between the views and periods visited. Moving the focus between days within the period already shown MUST NOT add history steps.
- **FR-022**: The address MUST name calendar dates, not moments in time, so that the same link shows the same calendar date on any device and in any time zone.

**Accessibility**

- **FR-023**: Every action in this feature MUST work by tapping on a touchscreen and by clicking with a mouse. Every action MUST also work with the keyboard alone, with focus always visible: switching views, previous, next, today, jumping to a date, choosing a day in the month view, and moving between days and events in each view.
- **FR-024**: With the keyboard, the arrow keys MUST move between days in each view, as in the month view from feature 001 (Constitution IV). Moving past the edge of the shown period MUST move to the neighboring period.
- **FR-025**: After any view or period change, the new period's title MUST be announced to screen reader users, and the focus MUST land on the selected date (or stay on the control the user used, if that control is still present).
- **FR-026**: Today's date MUST be announced as "today" by screen readers in addition to its full date. Each event in the day and week views MUST have an accessible name with its title and full date and time range, as in feature 001 (FR-020).
- **FR-027**: Dates, times, and period titles MUST follow the device's language and region settings, as in feature 001 (FR-021).

### Key Entities

- **View state**: What the user is currently looking at. It has a view type (day, week, or month) and a selected date (a calendar date, not a moment in time). The period shown is derived from these two: the selected date itself, the Sunday-to-Saturday week containing it, or the month containing it. The view state is what the page address represents. It is not saved anywhere else.
- **Today**: The current calendar date in the device's current time zone. It is not stored; it is read from the device and updates when the date or time zone changes.
- **Event**: As defined in feature 001. This feature only displays events and does not change what an event is.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From any view, the user can reach any other view, today, or a chosen date in at most 2 taps or clicks, not counting entering the date.
- **SC-002**: The new view or period, including all of its events, appears within 1 second of any navigation action, for a calendar with up to 5,000 events.
- **SC-003**: In 100% of tested cases, refreshing the page or opening a saved link shows the same view and selected date that the address names.
- **SC-004**: Every user story in this spec can be completed by touch alone on a phone-sized screen, by mouse alone, by keyboard alone, and with a screen reader that announces each new period and identifies today.
- **SC-005**: Navigation and event placement are correct in 100% of the listed edge cases: DST change days, Feb 29, month-end clamping, year boundaries, events across midnight, and multi-day events.
- **SC-006**: A first-time user can identify today's date in any view that contains it in under 3 seconds.

## Assumptions

- **Builds on feature 001**: The month view, event details, creating, editing, and deleting events, time zone handling, and date and time formatting all come from feature 001 and keep working as specified there. This feature adds views and navigation around them.
- **Week layout**: Weeks start on Sunday in both the week view and the month view, as in feature 001. Letting the user choose the first day of the week is out of scope.
- **Default view**: Opening the calendar with no view in the address shows the month view, as in feature 001. The calendar does not remember the last view used, apart from what the address and browser history keep.
- **Selected date**: A selected date is always present. It starts as today when no date is given, and it is the date the user focused or navigated to most recently.
- **Supported range**: January 1, 1900 to December 31, 2199 is assumed to cover any personal use.
- **Scope of "choosing a day"**: Choosing a day opens it in the day view from the month view and from the day headings of the phone-sized week view (FR-004a). Opening a day from the headings of the wide week view is not required by this feature.
- **Out of scope**: Agenda or list views, multi-week or year views, custom view lengths (e.g., 3-day or work-week), drag-and-drop to move or resize events, creating events by selecting a time range in the day or week view, and showing week numbers. Each is left for a later feature.
- **Single user, single device**: As in feature 001. Bookmarks and links are for the user's own use on the device where the calendar runs. They carry no events or private data, only a view and a date.
