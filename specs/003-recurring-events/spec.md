# Feature Specification: Recurring Events

**Feature Branch**: `003-recurring-events`

**Created**: 2026-10-06

**Status**: Draft

**Input**: User description: "Recurring events. When creating or editing an event I can make it repeat daily, weekly on chosen weekdays, monthly, or yearly, with an optional end date or number of occurrences. Recurring events appear on every matching date in the day, week, and month views. I can edit or delete a single occurrence, this and all following occurrences, or the whole series."

## Clarifications

### Session 2026-10-06

- Q: When a monthly event falls on the 29th, 30th or 31st, what should happen in months that don't have that day? → A: Move it to the last day of those months (e.g., Feb 28, or 29 in leap years, Apr 30, Jun 30).
- Q: Should monthly events also be able to repeat on a weekday position, such as "every second Tuesday" or "the last Friday of the month"? → A: Yes. The user chooses between the day number and the weekday position, both taken from the start date. "Last" is offered when the start date is in the month's last 7 days.
- Q: Should you be able to set how often a series repeats, such as "every 2 weeks" or "every 3 months", rather than always every day, week, month or year? → A: Yes, for all four frequencies, as "Every [N] days/weeks/months/years" with − / + buttons. N runs from 1 to 99 and defaults to 1.
- Q: When you edit an occurrence of a series, should the calendar ask "This event / This and following / All events" before the edit form opens, or when you press Save? → A: At Save. The form always shows the repeat options. If they were changed, only "This and following events" and "All events" are offered.
- Q: If you change an occurrence's date (for example, moving Wednesday's to Thursday) and then choose "All events" or "This and following events", what should happen to the series? → A: That choice isn't available. When an occurrence's start date is changed, only "This event" is offered at Save. To shift a series, the user edits its repeat options instead.
- Q: An event from 6:00 PM on the 8th to 2:00 AM on the 9th was listed on the 9th as "6:00 PM", so it looked like a separate evening event on the 9th. How should it show on the next day? → A: It shows on the day it starts, at its start time. On each following day it covers, it shows as starting at 12:00 AM and is ordered among that day's events by that time. An event that ends exactly at midnight shows only on its start day (FR-032).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Make an event repeat (Priority: P1)

When I create or edit an event, I can set it to repeat daily, weekly on the weekdays I choose, monthly, or yearly. By default it repeats forever, but I can make it stop on a date or after a set number of times. The event then appears on every matching date in the day, week, and month views.

**Why this priority**: This is the core of the feature. Without it, I have to enter a weekly class or a yearly birthday by hand, once for every date. Every other story in this feature depends on a series existing.

**Independent Test**: Create a weekly event on Monday and Wednesday that ends after 6 occurrences. Confirm it appears on exactly those 6 dates in the month, week, and day views, and that it is still there after the calendar is closed and reopened.

**Acceptance Scenarios**:

1. **Given** I am creating a timed event "Gym" on Monday, October 12, 2026 from 7:00 AM to 8:00 AM, **When** I set it to repeat weekly on Monday, Wednesday, and Friday with no end, **Then** "Gym" appears at 7:00 AM on every Monday, Wednesday, and Friday from October 12 onward, in every view and every period I navigate to.
2. **Given** I am creating an event on October 6, 2026, **When** I set it to repeat daily and end after 5 occurrences, **Then** it appears on October 6, 7, 8, 9, and 10 only.
3. **Given** I am creating an event on October 6, 2026, **When** I set it to repeat weekly and end on October 27, 2026, **Then** it appears on October 6, 13, 20, and 27 and on no later date. The end date is included.
4. **Given** I am creating an all-day event "Mom's birthday" on March 14, 2027, **When** I set it to repeat yearly, **Then** it appears as all-day on March 14 of every year from 2027 onward.
5. **Given** I am creating an event on October 14, 2026, **When** I set it to repeat monthly, **Then** it appears on the 14th of every month.
6. **Given** I am creating an event on Wednesday, October 14, 2026, **When** I set it to repeat monthly and choose "On the second Wednesday", **Then** it appears on November 11, December 9, 2026, January 13, 2027, and so on.
7. **Given** I am creating an event on Friday, October 30, 2026, **When** I set it to repeat monthly, **Then** I can choose "On day 30" or "On the last Friday". If I choose "On the last Friday", it appears on November 27, December 25, 2026, January 29, 2027, and so on.
8. **Given** I am creating an event on Tuesday, October 6, 2026, **When** I set it to repeat every 2 weeks on Tuesday and Thursday, **Then** it appears on October 6 and 8, October 20 and 22, November 3 and 5, and so on, and not in the weeks between.
9. **Given** I am creating an event on January 15, 2027, **When** I set it to repeat every 3 months on day 15, **Then** it appears on January 15, April 15, July 15, and October 15, 2027, and so on.
10. **Given** I open the repeat options, **When** I choose "Weekly", **Then** the start date's weekday is already selected, and I can select or clear any of the seven weekdays by tapping or clicking them.
11. **Given** I am setting a weekly repeat, **When** I clear every weekday, **Then** the event is not saved and I see a message saying at least one weekday must be chosen.
12. **Given** a saved non-repeating event, **When** I edit it and set it to repeat daily, **Then** it becomes a series that starts on the event's original date.
13. **Given** I am setting an end or an interval, **When** I enter an end date before the start date, a number of occurrences that is less than 1 or more than the limit (FR-007), or an interval outside 1 to 99 (FR-005), **Then** the event is not saved and I see a message next to the field at fault.
14. **Given** I am setting a repeat, **When** I look at the repeat area of the form, **Then** I see a plain-language summary of the rule, such as "Weekly on Monday, Wednesday, and Friday, 10 times", "Every 2 weeks on Tuesday and Thursday", or "Monthly on day 14, until December 31, 2026".

---

### User Story 2 - Recognize and view a recurring event (Priority: P1)

When I see an event in any view, I can tell whether it is part of a series. When I open its details, I see the date and time of that particular occurrence and how the series repeats.

**Why this priority**: Without this, I can't tell a one-off event from a repeating one. I need that to know what an edit or delete will affect. It is P1 because Stories 3 and 4 start from the details of an occurrence.

**Independent Test**: With one weekly series and one one-time event on the same day, confirm the series occurrence is marked as repeating in each view and the one-time event is not. Open an occurrence and confirm its details show that occurrence's date and the repeat summary.

**Acceptance Scenarios**:

1. **Given** a weekly series and a one-time event on the same day, **When** I view that day in the day, week, or month view, **Then** the occurrence carries a repeat indicator that does not rely on color alone, and the one-time event does not.
2. **Given** the weekly "Gym" series, **When** I open the occurrence on Wednesday, October 21, 2026, **Then** the details show Wednesday, October 21, 2026, 7:00 AM to 8:00 AM, and the summary "Weekly on Monday, Wednesday, and Friday".
3. **Given** I use a screen reader, **When** I reach a series occurrence in any view, **Then** its accessible name says that it repeats, in addition to its title and full date and time.
4. **Given** the month view on a phone-sized screen, **When** a day has a series occurrence, **Then** it is counted in that day's markers and event count, like any other event.

---

### User Story 3 - Change a single occurrence, or this and following (Priority: P2)

When I edit an occurrence of a series, I choose whether the change applies to only this occurrence, to this and all following occurrences, or to the whole series.

**Why this priority**: Real schedules have exceptions. A class moves one week, or a weekly meeting changes its time from next month on. Without this, I would have to delete and recreate the whole series. It comes after P1 because a series that is only created and viewed already saves time.

**Independent Test**: Create a weekly series. Move one occurrence to a different time, and confirm only that date changes. Then change the title from a later occurrence with "This and following events", and confirm earlier occurrences keep the old title and later ones get the new title.

**Acceptance Scenarios**:

1. **Given** I am editing an occurrence of a series and press Save, **When** the change would apply to the series, **Then** I am asked to choose "This event", "This and following events", or "All events", each as a large, clearly labeled button, with a Cancel option that returns me to the form with my changes kept.
2. **Given** the weekly "Gym" series, **When** I change the October 21 occurrence to 6:00 PM – 7:00 PM and choose "This event", **Then** only October 21 shows 6:00 PM, and every other occurrence stays at 7:00 AM.
3. **Given** the October 21 occurrence was changed on its own, **When** I view its details, **Then** it still shows as part of the series, and the repeat summary is still shown.
4. **Given** the weekly "Gym" series, **When** I change the title of the November 2 occurrence to "Swim" and choose "This and following events", **Then** occurrences before November 2 keep the title "Gym", and November 2 and every later occurrence show "Swim".
5. **Given** a series that ends after 10 occurrences, **When** I change the 4th occurrence with "This and following events", **Then** the 1st to 3rd occurrences keep their old details, and the 4th to 10th get the new details. The series still has 10 occurrences in total.
6. **Given** the weekly "Gym" series, **When** I change the time of any occurrence to 6:30 AM and choose "All events", **Then** every occurrence, past and future, shows 6:30 AM.
7. **Given** I edit the first occurrence of a series, **When** I am asked where to apply the change, **Then** "This and following events" has the same effect as "All events".
8. **Given** I am editing one occurrence, **When** I change its date to another day, **Then** only "This event" is offered at Save. After I choose it, that occurrence moves to the new day and no longer appears on its original day. The rest of the series is unchanged.
9. **Given** I am editing an occurrence and I changed its repeat options, **When** I press Save, **Then** only "This and following events" and "All events" are offered, because a single occurrence cannot have its own repeat rule.
10. **Given** I am editing an occurrence and I changed both its start date and the repeat options, **When** I press Save, **Then** nothing is saved, and I see a message saying that a new date can only apply to this event while repeat changes apply to the series, so I must undo one of the two changes.
11. **Given** I open an occurrence to edit it, **When** the form appears, **Then** it opens straight away, without asking first where the change will apply, and it shows the occurrence's own date and time and the series' repeat options.

---

### User Story 4 - Delete a single occurrence, this and following, or the whole series (Priority: P2)

When I delete an occurrence of a series, I choose whether to delete only that occurrence, that one and all the following ones, or the whole series.

**Why this priority**: Cancelling one class on a holiday, or ending a series early, are common. It ranks with Story 3 because both handle changes to an existing series.

**Independent Test**: Create a daily series with 10 occurrences. Delete the 3rd with "This event" and confirm only that date is gone. Delete the 7th with "This and following events" and confirm the 7th to 10th are gone. Delete any remaining occurrence with "All events" and confirm the series is gone from every view, including after reopening the calendar.

**Acceptance Scenarios**:

1. **Given** I choose to delete an occurrence of a series, **When** the confirmation appears, **Then** I choose "This event", "This and following events", or "All events", each as a large, clearly labeled button, or Cancel to keep everything unchanged.
2. **Given** the weekly "Gym" series, **When** I delete the October 21 occurrence with "This event", **Then** October 21 no longer shows "Gym", and every other occurrence is unchanged.
3. **Given** the weekly "Gym" series, **When** I delete the November 2 occurrence with "This and following events", **Then** "Gym" appears up to October 30 and on no date from November 2 onward.
4. **Given** the weekly "Gym" series, **When** I delete any occurrence with "All events", **Then** "Gym" disappears from every date in every view.
5. **Given** I delete the first occurrence with "This and following events", **When** the delete completes, **Then** the whole series is deleted, as with "All events".
6. **Given** a series with only one remaining occurrence, **When** I delete that occurrence with "This event", **Then** the whole series is deleted, and nothing about it is left behind.
7. **Given** I deleted occurrences in an earlier session, **When** I reopen the calendar, **Then** the deleted occurrences are still gone.

---

### User Story 5 - Change or stop repeating for a whole series (Priority: P3)

I can change how a series repeats, or make it stop repeating, by editing it with "All events" or "This and following events".

**Why this priority**: Schedules change their rhythm, such as a weekly meeting that becomes every Monday and Thursday. This is less common than one-off exceptions, and deleting and recreating the series is a workaround.

**Independent Test**: Create a weekly Monday series, change it to Monday and Thursday with "All events", and confirm Thursdays are added. Then set it to "Does not repeat" with "All events" and confirm only the first occurrence remains, as a one-time event.

**Acceptance Scenarios**:

1. **Given** a weekly series on Mondays, **When** I edit it with "All events" to repeat on Monday and Thursday, **Then** occurrences appear on every Monday and Thursday from the series start.
2. **Given** a series in which some occurrences were changed or deleted on their own, **When** I change its repeat rule or its times with "All events" or "This and following events", **Then** I am warned before saving that individual changes to the affected occurrences will be discarded. After I confirm, the affected occurrences all follow the new rule.
3. **Given** a series in which some occurrences were changed on their own, **When** I change only the title, location, or notes with "All events", **Then** the new values apply to every occurrence, including the individually changed ones. Their individual dates and times are kept, and deleted occurrences stay deleted.
4. **Given** a series, **When** I edit it with "All events" and set it to "Does not repeat", **Then** I am warned that every occurrence except the first will be removed. After I confirm, only the first occurrence remains, as a one-time event.
5. **Given** a series, **When** I edit an occurrence with "This and following events" and set it to "Does not repeat", **Then** the series ends before that occurrence, and that occurrence remains as a one-time event.

---

### Edge Cases

- **Monthly on days 29–31**: A monthly series on a day that some months don't have moves to the last day of those months. A series on the 31st that starts January 31, 2027 occurs on January 31, February 28, March 31, April 30, and so on. A series on the 30th occurs on February 28, 2027 and February 29, 2028. Each month has exactly one occurrence. The repeat summary says so (e.g., "Monthly on day 31, or the last day of shorter months").
- **Yearly on February 29**: A yearly series that starts on February 29, 2028 occurs only on February 29 in leap years (2032, 2036, …), following the iCalendar standard required by the constitution. The repeat summary says so (e.g., "Yearly on February 29 (leap years only)").
- **Weekly start not on a chosen weekday**: If the start date's weekday is not one of the chosen weekdays (e.g., the start is Tuesday and only Thursday is chosen), the series starts on the first chosen weekday after the start date. The form shows the adjusted first date before saving.
- **DST changes**: A timed series keeps its local clock time across DST changes. A 9:00 AM weekly event stays at 9:00 AM before and after the change, even though the gap between the two moments is 167 or 169 hours.
- **Occurrence in a DST gap or overlap**: If an occurrence's local time doesn't exist on a spring-forward day (e.g., 2:30 AM), it is moved forward by the length of the gap (to 3:30 AM). If the time occurs twice on a fall-back day, the earlier one is used. This matches feature 001's rules for single events.
- **Device time zone changes**: A timed series repeats at its local clock time in the time zone where it was created. If the device moves to another zone, each occurrence keeps its moment in time and is shown in the new zone, as in feature 001 (FR-015). For example, a 9:00 AM Chicago weekly event shows at 10:00 AM in New York. All-day series never move.
- **Occurrences spanning midnight or several days**: A series whose event spans midnight (e.g., 10:00 PM – 1:00 AM) or several days (e.g., a 3-day all-day event each month) shows every occurrence on each day it covers, as in features 001 and 002. Occurrences may overlap one another. On every day after the first, the event shows as starting at 12:00 AM, not at its original start time (FR-032).
- **Count and end-date limits**: A series with a set number of occurrences shows exactly that number, counting occurrences deleted on their own toward the total. A series with an end date never shows an occurrence that starts after the end date.
- **Repeat with no end**: A series with no end shows occurrences on every matching date up to the end of the supported range (December 31, 2199, from feature 002).
- **Occurrence moved onto another occurrence's date**: Moving one occurrence onto a date where the series already has an occurrence is allowed. Both are shown.
- **Occurrence moved outside the series range**: Moving one occurrence before the series start or after its end is allowed. It is still part of the series and still follows "This and following events" based on its original date.
- **Saving fails**: If saving a change to a series or occurrence fails, I am told, no occurrence is shown as changed, and my entries stay in the form, as in feature 001 (FR-014). A change that affects several occurrences either applies to all of them or to none.
- **Many series**: A day with many occurrences from different series follows the crowding rules of features 001 and 002 ("N more" and "+N").

## Requirements *(mandatory)*

### Functional Requirements

**Setting a repeat**

- **FR-001**: When creating an event, or editing an event or a whole series, users MUST be able to choose one of: Does not repeat (the default), Daily, Weekly, Monthly, or Yearly. This applies to timed and all-day events.
- **FR-002**: Weekly repeats MUST let the user choose one or more of the seven weekdays, as a row of individually tappable weekday buttons. The start date's weekday MUST be selected by default. At least one weekday MUST be chosen.
- **FR-003**: Monthly repeats MUST let the user choose, as large tappable options filled in from the start date, between:
  - **On a day number** (the default), e.g., "On day 14". In months without that day, the occurrence MUST fall on the month's last day (see Edge Cases).
  - **On a weekday position**, e.g., "On the second Wednesday". The position is the start date's first, second, third, or fourth weekday of its month. When the start date is in the month's last 7 days, "On the last Wednesday" MUST also be offered. When the start date is the month's fifth such weekday, "last" is the only weekday-position option, because many months have no fifth one.
- **FR-003a**: If the start date changes after a monthly option is chosen, the options MUST be refilled from the new start date, keeping the same kind of choice (day number or weekday position) where possible.
- **FR-004**: Yearly repeats MUST repeat on the start date's month and day (e.g., every March 14).
- **FR-005**: Every frequency MUST let the user set an interval, shown as "Every [N] days", "weeks", "months", or "years". N is a whole number from 1 to 99 and defaults to 1. It can be changed with large − and + buttons or typed in. Values outside that range MUST be rejected with a clear message. The interval is counted from the series' first occurrence. For weekly repeats, it counts Sunday-to-Saturday weeks (matching the calendar's week layout), and every chosen weekday in an included week occurs. Monthly day-number repeats with an interval still move to the last day of shorter months (FR-003). Yearly repeats on February 29 occur only in included years that are leap years.
- **FR-006**: The user MUST be able to choose how a series ends: Never (the default), On a date (included), or After a number of occurrences.
- **FR-007**: An end date MUST be on or after the start date. A number of occurrences MUST be a whole number from 1 to 999. Invalid values MUST be rejected with a clear message next to the field at fault.
- **FR-008**: The form MUST show a plain-language summary of the repeat rule as the user sets it, such as "Weekly on Monday, Wednesday, and Friday, until December 31, 2026". The same summary MUST be shown in the details of every occurrence.
- **FR-009**: The first occurrence of a series MUST be the start date the user entered, or, for weekly repeats where the start date's weekday is not chosen, the first chosen weekday after it. The form MUST show the adjusted date before saving.

**Showing occurrences**

- **FR-010**: Every occurrence of a series MUST appear in the day, week, and month views on each date it covers, with the same layout, sorting, crowding, and phone-sized rules as one-time events from features 001 and 002.
- **FR-011**: Each occurrence MUST show a repeat indicator in every view where the event's label is shown, and in its details. The indicator MUST NOT rely on color alone. In the phone-sized month view, where events show as markers, the indicator is shown in the details only.
- **FR-012**: Selecting an occurrence MUST open its details, showing that occurrence's own date and time, its title, location, and notes, and the series' repeat summary.
- **FR-013**: Timed occurrences MUST keep the series' local clock time across DST changes, in the time zone in which the series was created. They MUST be shown in the device's current time zone, as in feature 001 (FR-015). All-day occurrences MUST keep their calendar dates in every time zone.
- **FR-014**: Occurrences MUST follow the iCalendar (RFC 5545) repeat rules, as the constitution requires, including the Feb 29 rule in Edge Cases.

**Editing**

- **FR-015**: Editing an occurrence MUST open the form directly, showing that occurrence's own date and time and the series' repeat options. When the user saves changes to an occurrence of a series, and only then, the system MUST ask whether to apply them to "This event", "This and following events", or "All events", or to cancel and return to the form with the changes kept. Each choice MUST be a large, clearly labeled button.
- **FR-016**: "This event" MUST change only the chosen occurrence. Its title, location, notes, start, end, and all-day flag MAY all be changed, including moving it to another date. If the user changed the repeat options in the form, "This event" MUST NOT be offered at Save, because a single occurrence cannot have its own repeat rule. The occurrence MUST stay part of the series.
- **FR-016a**: If the user changed the occurrence's start date, only "This event" MUST be offered at Save. Moving a whole series or its following occurrences is done by changing the repeat options (e.g., the weekdays), not by moving one occurrence's date. If the user changed both the start date and the repeat options, the system MUST NOT save, and MUST explain that one of the two changes has to be undone. Changing only the time of day, the end date, or the all-day flag does not count as changing the start date.
- **FR-017**: "This and following events" MUST end the original series before the chosen occurrence and start a new series from it with the changes. Earlier occurrences MUST keep their old details. If the original series ended after a number of occurrences, the new series MUST get the remaining number, so the total stays the same. Applied to the first occurrence, it MUST act as "All events".
- **FR-018**: "All events" MUST apply the changes to every occurrence, past and future.
- **FR-019**: When a change to "All events" or "This and following events" changes only the title, location, or notes, the new values MUST apply to every affected occurrence, including individually changed ones, which keep their own dates and times. Deleted occurrences MUST stay deleted.
- **FR-020**: When a change to "All events" or "This and following events" changes the repeat rule or the times, the system MUST warn the user that individual changes and deletions among the affected occurrences will be discarded, and MUST ask them to confirm before saving.
- **FR-021**: Setting a series to "Does not repeat" with "All events" MUST keep only the first occurrence, as a one-time event. With "This and following events", it MUST end the series before the chosen occurrence and keep that occurrence as a one-time event. The user MUST be warned before saving.
- **FR-022**: All validation from feature 001 (title, length limits, end after start, DST adjustments) MUST apply to series and to individual occurrences.

**Deleting**

- **FR-023**: When the user deletes an occurrence of a series, the system MUST ask whether to delete "This event", "This and following events", or "All events", or to cancel. Each choice MUST be a large, clearly labeled button.
- **FR-024**: "This event" MUST remove only the chosen occurrence. "This and following events" MUST end the series before the chosen occurrence. "All events" MUST remove the whole series. Applied to the first occurrence, "This and following events" MUST act as "All events". Deleting the last remaining occurrence MUST remove the whole series.
- **FR-025**: Deleting is permanent, as in feature 001 (FR-011).

**Persistence**

- **FR-026**: Series, individual changes, and individual deletions MUST be kept between sessions on the device, as in feature 001 (FR-013).
- **FR-027**: A change that affects several occurrences MUST be saved completely or not at all. If a save fails, the system MUST tell the user, keep their entries, and MUST NOT show any occurrence as changed (feature 001, FR-014).

**Touch, pointer, and accessibility**

- **FR-028**: Every control in this feature, including the repeat choice, weekday buttons, end options, and the scope choices ("This event", "This and following events", "All events"), MUST meet the touch target size of feature 002 (FR-013c) and MUST work by tapping and by clicking. On phone-sized screens, the repeat options MUST fit the screen width without sideways scrolling.
- **FR-029**: Every control MUST work with the keyboard alone, with visible focus, and MUST have an accessible name. Each weekday button MUST announce its full weekday name and whether it is selected.
- **FR-030**: The repeat summary MUST be readable by screen readers in full. Each occurrence's accessible name MUST include that it repeats. Saves, deletes, and the scope choice result (e.g., "Changed this and following events") MUST be announced.
- **FR-031**: Dates, weekday names, and times in the repeat options and summaries MUST follow the device's language and region settings, as in feature 001 (FR-021).

**Events that run past midnight** (one-time events and occurrences alike)

- **FR-032**: A timed event MUST appear on the day it starts, at its start time. If it runs past midnight, it MUST also appear on each following day it covers, as starting at 12:00 AM on that day. It MUST NOT show its original start time on those days. In the month view, the phone week list, and the "+N more" list, it MUST be ordered among that day's timed events as if it started at 12:00 AM (then by title, as for any two events starting at the same time). On those days its accessible name MUST still give its full start and end, followed by "continues from the previous day". An event that ends exactly at midnight MUST appear only on its start day (feature 001). In the phone week list, the time shown on those days runs from 12:00 AM to when the event ends that day.

### Key Entities

- **Series**: A recurring event. It has everything an event from feature 001 has (title, all-day flag, start and end of the first occurrence, location, notes), plus a repeat rule and the time zone in which it was created. Its occurrences follow from the rule.
- **Repeat rule**: How a series repeats. It has a frequency (daily, weekly, monthly, or yearly), an interval (every 1 to 99 of those), the chosen weekdays for weekly repeats, the kind of monthly repeat (day number, or weekday position: first to fourth, or last), and an end (never, on a date, or after a number of occurrences).
- **Occurrence**: One appearance of a series on the calendar, identified by the series and its original date. It is shown with the series' details unless it has its own change.
- **Occurrence change**: A record that one occurrence was changed on its own (new title, location, notes, start, end, or all-day flag) or deleted. It belongs to a series and refers to the occurrence's original date.
- **Event**: A one-time event, as in feature 001. Setting it to repeat turns it into a series, and setting a series to "Does not repeat" turns it back into a one-time event.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can turn a new or existing event into a weekly series on chosen weekdays, with an end, in under 30 seconds, using taps or clicks only.
- **SC-002**: In 100% of tested cases, occurrences appear on exactly the dates the repeat rule defines, and on no others, in the day, week, and month views. This includes DST change days, Feb 29, months of different lengths, year boundaries, and end dates and counts.
- **SC-003**: In 100% of tested cases, "This event", "This and following events", and "All events" change or delete exactly the intended occurrences and leave every other occurrence unchanged.
- **SC-004**: Each view, including all of its occurrences, appears within 1 second of any navigation action, for a calendar with up to 5,000 events and 200 series without an end.
- **SC-005**: Every user story in this spec can be completed by touch alone on a phone-sized screen, by mouse alone, by keyboard alone, and with a screen reader that announces the repeat summary and the result of each scope choice.
- **SC-006**: 100% of series, individual changes, and deletions that were saved successfully are still present and identical after the calendar is closed and reopened.

## Assumptions

- **Builds on features 001 and 002**: Event fields, validation, details, time zone and DST rules, date formats, the three views, navigation, crowding rules, and touch-target sizes come from those features and keep working as specified there.
- **Repeat options are limited**: Only the frequencies listed in FR-001 are offered. Custom rules such as "every weekday except holidays", repeats on several days of the month, weekday positions other than the start date's own, and listing extra one-off dates are out of scope.
- **Series time zone**: A timed series repeats at its local clock time in the device's time zone at the moment the series was created (or last had its times changed). The user does not choose this zone. This is needed so that a 9:00 AM event stays at 9:00 AM across DST changes.
- **Where the scope choice is asked**: The scope choice appears when saving or deleting, not when opening the form. Changes to a one-time event never show it.
- **Past occurrences**: "All events" changes past occurrences too. The calendar does not freeze past occurrences.
- **Out of scope**: Reminders for series, importing or exporting repeat rules (e.g., iCalendar files), dragging occurrences to move them, undoing a scope choice, and showing a list of all occurrences or exceptions. Each is left for a later feature.
- **Occurrence limit**: The 999-occurrence limit applies only to "After a number of occurrences". Series that end on a date or never end have no limit within the supported date range.
- **Single user, single device**: As in features 001 and 002.
