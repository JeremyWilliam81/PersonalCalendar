<!--
Sync Impact Report
==================
Version change: (unversioned template) → 1.0.0
Modified principles: none (initial adoption; all placeholders replaced)
Added principles:
  - I. Test-First Date & Time Logic (NON-NEGOTIABLE)
  - II. UTC Storage with Separate IANA Time Zone
  - III. Single-Developer Simplicity
  - IV. Accessible by Default
  - V. Spec-First, Branch-Per-Feature
  - VI. Clean Architecture
Added sections:
  - Date & Time Standards
  - Development Workflow & Quality Gates
  - Governance
Removed sections: none
Follow-up TODOs: none
-->

# PersonalCalendar Constitution

## Core Principles

### I. Test-First Date & Time Logic (NON-NEGOTIABLE)

All code that computes, parses, formats, compares, or converts dates and times MUST be
developed test-first:

- A failing automated test MUST exist before the implementing code is written
  (Red → Green → Refactor).
- Tests MUST cover the known hazard cases relevant to the change: DST transitions
  (spring-forward gaps and fall-back overlaps), month and year boundaries, leap years
  (including Feb 29), all-day events, events spanning midnight, and at least one time zone
  with a non-hour offset (e.g., `Asia/Kolkata`, `Australia/Adelaide`).
- Tests MUST NOT depend on the machine's local clock or time zone; the current time and
  the zone MUST be injected or fixed.
- A bug fix in date/time logic MUST ship with a regression test that reproduced it.

**Rationale**: Date/time errors are the most common and least visible defects in a calendar.
They are cheap to prevent with tests and expensive to diagnose in production.

### II. UTC Storage with Separate IANA Time Zone

- Every persisted instant (event start/end, reminders, created/updated timestamps) MUST be
  stored in UTC.
- The user's time zone MUST be stored separately as an IANA identifier
  (e.g., `America/Chicago`). Fixed offsets (e.g., `-05:00`) and abbreviations
  (e.g., `CST`) MUST NOT be stored as a substitute for a zone.
- Conversion to local time MUST happen only at the presentation/input boundary, using the
  stored IANA zone.
- Where a local wall-clock meaning must survive rule changes (e.g., recurring events,
  all-day dates), the original local value and its IANA zone MUST be stored alongside or
  instead of the UTC instant, and the choice MUST be documented in the feature's plan.

**Rationale**: UTC gives one unambiguous ordering of instants; the IANA zone preserves
the user's intent across DST and political offset changes.

### III. Single-Developer Simplicity

- The application MUST remain maintainable by one developer. Every dependency, service,
  and moving part MUST justify its cost in the feature's plan.
- Prefer the standard library and well-established, actively maintained packages over
  custom infrastructure. Do not add a new runtime, datastore, or deployment target without
  a written justification in the plan's Complexity Tracking section.
- Build only what a current spec requires (YAGNI). Speculative abstractions MUST NOT be
  added "for later."

**Rationale**: A personal project dies when its upkeep exceeds the time one person has.
Simplicity is what keeps it alive.

### IV. Accessible by Default

- All interactive UI MUST be fully operable by keyboard alone, with a visible focus
  indicator and a logical tab order. Calendar grids MUST support arrow-key navigation.
- Every control, icon button, and form field MUST have an accessible name
  (visible label, `aria-label`, or `aria-labelledby`). Dynamic changes (e.g., saved event,
  validation errors) MUST be announced to screen readers.
- Dates and times presented to users MUST be readable by screen readers in full
  (not only as abbreviated visual text).
- UI work SHOULD target WCAG 2.2 AA; any known exception MUST be recorded in the spec.

**Rationale**: A calendar is a daily-use tool; if it cannot be used without a mouse or
without sight, it is broken for those users.

### V. Spec-First, Branch-Per-Feature

- Every feature MUST begin with a written spec (`/speckit-specify`) before implementation
  code is written.
- Every feature MUST be developed on its own dedicated branch and merged to `main` only
  through a pull request.
- `main` MUST remain in a working, releasable state.

**Rationale**: Specs force the problem to be understood before it is solved; isolated
branches keep unfinished work from breaking the working app.

### VI. Clean Architecture

- Code MUST be organized in layers with dependencies pointing inward:
  domain (calendar rules, date/time logic) → application (use cases) →
  adapters (UI, persistence, external APIs).
- Domain and application code MUST NOT import UI frameworks, databases, or network
  clients; they depend on interfaces implemented by the adapters.
- Date/time domain logic MUST be pure and deterministic so that Principle I is testable
  without infrastructure.
- Layering MUST be applied proportionately: introduce an abstraction only when it isolates
  a real boundary (storage, clock, time zone data, external service), consistent with
  Principle III.

**Rationale**: Clear boundaries let the app grow (new views, sync, storage) without
rewrites, while keeping the core logic small and easy to test.

## Date & Time Standards

- Instants are exchanged in ISO 8601 with an explicit `Z` or offset; bare local strings
  without a zone MUST NOT cross layer boundaries.
- A single, well-maintained time zone–aware library (or the platform's native Temporal /
  Intl APIs where available) MUST be used for all zone conversions; hand-rolled offset math
  is prohibited.
- The current time MUST be obtained through an injectable clock abstraction, never read
  directly inside domain logic.
- Recurrence rules, if implemented, MUST follow RFC 5545 (iCalendar RRULE) semantics and
  be expanded in the event's IANA zone.

## Development Workflow & Quality Gates

1. **Specify** – create the feature spec on a new feature branch.
2. **Plan** – the plan's Constitution Check MUST confirm compliance with Principles I–VI or
   record justified exceptions in Complexity Tracking.
3. **Tasks** – date/time tasks MUST list their test tasks before implementation tasks.
4. **Implement** – tests written first for date/time logic; all tests pass locally.
5. **Merge** – a pull request into `main` MUST have passing tests and a self-review against
   this constitution, including a keyboard-only and screen-reader-label check for any UI
   change.

## Governance

- This constitution supersedes other project practices. Where guidance conflicts, this
  document wins.
- Amendments are made via `/speckit-constitution`, committed on a branch, and merged by
  pull request with a summary of what changed and why.
- Versioning follows semantic versioning:
  - **MAJOR**: a principle is removed or redefined in a backward-incompatible way.
  - **MINOR**: a principle or section is added or materially expanded.
  - **PATCH**: clarifications, wording, and typo fixes.
- Compliance is reviewed at every plan (Constitution Check) and every pull request.
  Unjustified complexity or unrecorded exceptions are grounds to block a merge.

**Version**: 1.0.0 | **Ratified**: 2026-09-29 | **Last Amended**: 2026-09-29
