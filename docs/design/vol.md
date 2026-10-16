# Volunteer Scheduling Design

**Project:** ReFrog
**Team:** Team 3
**Area:** `UC-VOL`

> **Realizes:** `UC-VOL-signup`, `UC-VOL-cancel-shift`, `UC-VOL-view-schedule`
> **Depends on:** `BR-volunteer-assignment`, `BR-location-staffing`, `BR-event-schedule`, `FR-AUTH-enforce-permissions`, `FR-VAL-server-validation`, `FR-SAVE-confirmed-success`, `FR-SAVE-unknown-outcome`, `FR-SAVE-duplicate-submission`, `ROB-duplicate-submission`, `USE-volunteer-signup-completion`, `PER-form-submission`, `SEC-least-privilege`, `UI-volunteer-schedule`
> **See:** [use cases](../requirements/use-cases.md#area-volunteer-scheduling-vol), [SRS non-use-case FRs](../requirements/software-requirements-specification.md#52-non-use-case-functional-requirements), [data dictionary](../requirements/software-requirements-specification.md#72-data-dictionary), [architecture-of-record](architectural-design.md) (§5.2 components, §8 crosscutting concepts)
> **Designed against:** `main` at `4323d3d` (2026-10-16). No application code exists yet; every class below is new.

---

_**How to use this template.** Copy it to `docs/design/<area>.md`, named for the lowercase area code (`ord.md` for `UC-ORD-*`). Instructions appear in italic square brackets; leave them in place until the document is stable, because the agent reads them on every later pass. The full worked example is Project Pulse's [`not.md`](https://github.com/Washingtonwei/project-pulse/blob/main/docs/design/not.md); read it for the shape, not for the answers._

_**What this document is.** The **design-of-record** for one use case area: how the code will realize its use cases, written **before** the code and approved before anyone builds from it. It is the last document your agent reads before it writes code, so anything it leaves out, the agent invents. Your [architecture-of-record](architectural-design.md) is the map of the whole system and stops at each component's responsibility; this document goes one level down, inside one area, against real code._

_**What it is not.** A copy of your use cases. It **cites** `UC-*`, `FR-*`, and `BR-*` by identifier and never restates what the system shall do; it says only how. It is also not a transcript of the code. Once code exists, link to the files rather than describing every class, column, or getter._

_**One document per area, not per use case.** Your area gains use cases over the term. The overview, the class diagram, and the data model are **revised in place** when it does. The sequence diagrams, the API contract rows, and the test rows are **appended**, one set per use case. Never add a `## UC-…` section that repeats the whole skeleton._

_**How much to write.** Pin down what a wrong guess would break: a requirement, a business rule, a quality attribute, or the contract between two parts of your team. Leave the rest for the agent to derive while it builds: variable names, field types, exact JSON, column lengths. When you are unsure, ask which side of that line a detail falls on, not whether it is interesting._

_**Write it in this order.** Each step is one person's or one pair's work, and the order is the point:_

1. _**Sketch it yourselves first.** Before you open the agent, draw the main success scenario's sequence diagram and write the API contract rows by hand, from the use case and your architecture-of-record. Twenty minutes, on paper or in this file._
2. _**Have the agent draft it.** Give the agent this template, the use case, the business rules it cites, your architecture-of-record's section 8 (Crosscutting Concepts), and the relevant existing code, and ask for a draft of this document. Use plan mode, so it proposes before it writes._
3. _**Compare the two.** Where the agent's draft differs from your sketch, decide which is right and why. What it found that you missed goes in; what you knew that it could not have known is the context your specification is missing, so fix the specification too._
4. _**Run the questions test** (the last section) and fix what it finds._
5. _**Open a pull request.** A teammate who did not write it reviews and approves it. Nobody starts an implementation branch from this design until it is merged._

---

## Overview

_[One paragraph: what this area does, which components of your [architecture-of-record](architectural-design.md) it lives in, and what it reads from or calls in other areas. Link the container diagram rather than redrawing it. If this design changes the architecture-of-record, say so here and list the change under "Changes to the architecture-of-record" below.]_

Volunteer Scheduling lets a signed-in volunteer claim an open slot on a shift, give a slot back, and see their own upcoming shifts and credited hours. It is the `VOL` module of the single Application container ([containers](architectural-design.md#51-containers), [use case areas](architectural-design.md#52-use-case-areas-and-components), `KD-deployment-shape`). It **owns one thing: the shift assignment**, the record that one volunteer holds one slot on one shift. It does not own shifts. Shifts, with their location, time, and required volunteer count, are created by Administration (`UC-ADM-manage-shifts`). VOL reads them through Administration's service and takes a row lock on a shift while it changes that shift's assignments. Location names and addresses come from Location Info. The signed-in user comes from Authentication. VOL sends no notifications. Cancelling does not notify anyone (`OI-7`, resolved). Shift reminders and admin-triggered notifications belong to the Notifications component, which reads VOL's assignments, not the other way round.

The hard part of this area is three invariants that must hold under concurrent and repeated requests:

1. **No overbooking.** A shift never has more active assignments than its required volunteer count, even when two volunteers claim the last slot at the same moment (`UC-VOL-signup` 4a, POST-2).
2. **No duplicate assignment.** One volunteer holds at most one slot on one shift, however many times the request arrives (4b, 5b, `FR-SAVE-duplicate-submission`, `ROB-duplicate-submission`).
3. **Hours stay consistent with assignments.** Credited hours are never stored, so they cannot disagree with the assignments they come from (`UC-VOL-view-schedule`).

## Components & classes

_[One class diagram for the whole area, and one line per class or file saying what it does and whether it is **new** or **reused**. Show only what a reader needs to see how the parts relate: classes, their key methods, their dependencies. Fields and getters belong to the code._

_**If your repository has no code for this area yet**, which is the normal case for your proving slice, list the files you expect the agent to create, with the paths your architecture-of-record's conventions give them, and mark them all new. The agent then builds into the structure you chose rather than one it improvised.]_

The repository has no code yet, so every file is new. The technology stack is not chosen yet (architecture §5.1). Names are given by module and class here, and file paths will follow from whichever stack the team records.

```mermaid
classDiagram
    direction LR
    class VolunteerShiftController {
        <<new, volunteer>>
        +listOpenShifts() Response
        +signUp(shiftId) Response
        +cancel(shiftId) Response
        +mySchedule() Response
    }
    class AssignmentService {
        <<new, volunteer>>
        +listOpenShifts(volunteerId) List~OpenShift~
        +signUp(volunteerId, shiftId) SignUpResult
        +cancel(volunteerId, shiftId) CancelResult
    }
    class ScheduleService {
        <<new, volunteer>>
        +scheduleFor(volunteerId) Schedule
    }
    class AssignmentRepository {
        <<new, volunteer>>
        +findByShiftAndVolunteer(shiftId, volunteerId) Assignment
        +countActiveByShift(shiftId) int
        +findActiveByVolunteer(volunteerId) List~Assignment~
    }
    class AssignmentQueryService {
        <<new, volunteer, read-only>>
        +isActive(shiftId, volunteerId) bool
        +activeCountsByShift(shiftIds) Map
    }
    class ShiftAssignment {
        <<new, entity>>
        shiftId
        volunteerId
        status: ACTIVE | CANCELLED
    }
    class ShiftService {
        <<reused, administration>>
        +findSignupCandidates(now) List~Shift~
        +findById(shiftId) Shift
        +lockForAssignment(shiftId) Shift
    }
    class LocationService {
        <<reused, location>>
        +findByIds(ids) List~Location~
    }
    class CurrentUser {
        <<reused, authentication>>
        +id() String
    }
    class Clock {
        <<reused, crosscutting 8.2.2>>
        +now() Instant
    }
    VolunteerShiftController --> AssignmentService
    VolunteerShiftController --> ScheduleService
    VolunteerShiftController --> CurrentUser
    AssignmentService --> AssignmentRepository
    AssignmentService --> ShiftService
    AssignmentService --> LocationService
    AssignmentService --> Clock
    ScheduleService --> AssignmentRepository
    ScheduleService --> ShiftService
    ScheduleService --> LocationService
    ScheduleService --> Clock
    AssignmentQueryService --> AssignmentRepository
    AssignmentRepository --> ShiftAssignment
```

- `volunteer/VolunteerShiftController` (new): the HTTP entry point for all four endpoints. It takes the volunteer id from `CurrentUser`, never from the path or body. It checks the request shape and nothing else.
- `volunteer/AssignmentService` (new): owns the three invariants. Each `signUp` and `cancel` call is one transaction.
- `volunteer/ScheduleService` (new): read-only. It builds the volunteer's upcoming shifts and computes credited hours from assignments and shift times at read time.
- `volunteer/AssignmentRepository` and the `ShiftAssignment` entity (new): the only code that touches the `shift_assignment` table.
- `volunteer/AssignmentQueryService` (new): the read-only face VOL shows other areas, so they never touch VOL's table. ADM's dashboard (`UC-ADM-view-dashboard`, `UC-ADM-notify-volunteers`) and the shift reminder's recheck (`FR-NOTIFY-current-state`) call it. Only the two methods shown are in VOL's scope. Any other method another area needs is added by that area's design, not here.
- `administration/ShiftService` (reused, **owned by ADM**): VOL needs three methods from it. `findSignupCandidates(now)` returns the shifts that have not started, in an event whose status is `planned` or `active`. `findById` is the plain lookup. `lockForAssignment` is the same lookup but holds a pessimistic write lock on the shift row until the caller's transaction ends. All three return a shift's start and end as **instants** (UTC), not as the data dictionary's local date and `HH:MM`. ADM converts from `America/Chicago` when it stores a shift (§8.2.2), and VOL compares them only with `Clock.now()`. These three methods are a contract with ADM's design (`adm.md`). See Open questions.
- `location/LocationService` (reused, owned by LOC): location name and address for display.
- `authentication/CurrentUser` (reused): the signed-in account's id. Any signed-in account can volunteer, including administrators. VOL keeps no separate volunteer table (see Data model).
- `Clock` (reused, architecture §8.2.2): the injected clock, so tests can fix "now" for "has the shift started" and "has the shift ended".
- **Front end** (new): the `UI-volunteer-schedule` view, with an open-shifts list, a sign-up action, and a my-schedule list with a cancel action. Sign-up is a single action on the open-shifts list, with no confirm dialog before it is sent, so a volunteer claims a shift within the two interactions `UI-volunteer-schedule` allows. The "signed up" confirmation shows only after the server answers `201` or `200` (step 6, `FR-SAVE-confirmed-success`). A confirm-before-send dialog is used only for cancel (`UC-VOL-cancel-shift` step 4). While a sign-up or cancel request is in flight, its button is disabled.

### The assignment's states

An assignment is never deleted. Cancelling flips it to `CANCELLED`, which keeps "cancellation status" for the coverage report (SRS §7.3.2). Signing up again reactivates the same row, which is why one row per (shift, volunteer) is enough.

```mermaid
stateDiagram-v2
    [*] --> ACTIVE: signUp, slot free (UC-VOL-signup step 5)
    ACTIVE --> CANCELLED: cancel, before shift start (UC-VOL-cancel-shift step 6)
    CANCELLED --> ACTIVE: signUp again, slot free
    ACTIVE --> ACTIVE: signUp again (5b, no change)
    CANCELLED --> CANCELLED: cancel again (duplicate, no change)
```

## Sequence

_[One sequence diagram per use case for its main success scenario, plus one for each extension that does not just return an error. **Label each interaction with the use case step it implements**, so a reviewer can check that every step is realized and nothing extra was invented. Use `alt` blocks for extensions that branch off the main flow. Give each use case its own `###` heading; that heading is the anchor your traceability matrix cites.]_

### UC-VOL-signup: signing up for a shift

```mermaid
sequenceDiagram
    actor V as Volunteer
    participant WEB as Web front end
    participant C as VolunteerShiftController
    participant AS as AssignmentService
    participant SH as ShiftService (ADM)
    participant R as AssignmentRepository
    V->>WEB: View open shifts (step 1)
    WEB->>C: GET /api/v1/shifts/open
    C->>AS: listOpenShifts(volunteerId from session)
    AS->>SH: findSignupCandidates(clock.now())
    AS->>R: active counts for those shifts
    AS-->>C: shifts with openSlots > 0, each with assignedToMe
    C-->>WEB: 200, list (possibly empty)
    WEB-->>V: Shifts by date and location, open slots shown (step 2), or "no shifts are open" (2a)
    V->>WEB: Select a shift (step 3)
    WEB->>C: POST /api/v1/shifts/{shiftId}/assignments
    C->>AS: signUp(volunteerId, shiftId)
    Note over AS,R: one transaction from here to commit
    AS->>SH: lockForAssignment(shiftId), row locked until commit
    alt shift unknown
        AS-->>C: refused, SHIFT_NOT_FOUND
    else
        AS->>R: findByShiftAndVolunteer(shiftId, volunteerId)
        alt already ACTIVE (4b, 5b)
            AS-->>C: existing assignment, alreadyAssigned = true
        else shift started, or event not planned/active (4a)
            AS-->>C: refused, SHIFT_NOT_OPEN
        else no row, or CANCELLED
            AS->>R: countActiveByShift(shiftId) (step 4)
            alt count >= requiredCount (4a)
                AS-->>C: refused, SHIFT_FULL
            else slot free
                AS->>R: insert ACTIVE, or reactivate the CANCELLED row (step 5)
                alt write or commit fails (5a)
                    AS-->>C: failure, nothing written
                else committed
                    AS-->>C: assignment, openSlots after, alreadyAssigned = false
                end
            end
        end
    end
    C-->>WEB: 201 new / 200 already assigned / 409 / 404 / 5xx
    WEB-->>V: Confirmation (step 6, 5b2), "already signed up" (4b1), "no longer available", back to list (4a1, 4a2), or "not completed, no assignment made", retry (5a1, 5a2)
```

_The own-assignment check comes **before** both the not-open check and the capacity check. A retry on a shift that this volunteer's first attempt has just filled, or a retry that arrives after the shift has started, returns "already signed up" (5b). It never returns "shift full" or "not open" (4a)._

**Unknown outcome (`FR-SAVE-unknown-outcome`).** If the connection drops before the response arrives, the front end does not report a failure. It calls `GET /api/v1/me/schedule` and reports a sign-up if the shift is there. If it is not, it says no assignment was made. Retrying the `POST` is also safe, because it is idempotent.

### UC-VOL-cancel-shift: cancelling a shift

```mermaid
sequenceDiagram
    actor V as Volunteer
    participant WEB as Web front end
    participant C as VolunteerShiftController
    participant AS as AssignmentService
    participant SH as ShiftService (ADM)
    participant R as AssignmentRepository
    V->>WEB: View my shifts (step 1)
    WEB->>C: GET /api/v1/me/schedule
    C-->>WEB: 200, upcoming shifts (step 2; see UC-VOL-view-schedule)
    V->>WEB: Cancel this shift (step 3)
    WEB-->>V: "Cancel your shift at <location>, <date time>?" (step 4)
    alt volunteer does not confirm (5a)
        WEB-->>V: Nothing sent, no change (5a1)
    else confirms (step 5)
        WEB->>C: DELETE /api/v1/shifts/{shiftId}/assignments/me
        C->>AS: cancel(volunteerId, shiftId)
        Note over AS,R: one transaction
        AS->>SH: lockForAssignment(shiftId)
        AS->>R: findByShiftAndVolunteer(shiftId, volunteerId)
        alt no row (PRE-2 fails)
            AS-->>C: refused, NOT_ASSIGNED
        else CANCELLED already (duplicate submission)
            AS-->>C: assigned = false, no change
        else ACTIVE, but shift has started
            AS-->>C: refused, SHIFT_STARTED
        else ACTIVE, shift not started
            AS->>R: set CANCELLED (step 6), the freed slot shows in the next open-shift list
            alt write or commit fails (6a)
                AS-->>C: failure, still ACTIVE
            else committed
                AS-->>C: assigned = false
            end
        end
        C-->>WEB: 200 / 404 / 409 / 5xx
        WEB-->>V: "Cancelled" (step 7), or "not completed, you are still assigned", retry (6a1, 6a2)
    end
```

_No notification is sent and nothing is queued on cancel (`OI-7`). The shift simply counts one fewer active assignment, which is how it "reappears as open" (POST-2)._

**Unknown outcome (`FR-SAVE-unknown-outcome`).** If the connection drops before the `DELETE`'s response arrives, the front end does not report a failure. It calls `GET /api/v1/me/schedule`. If the shift is gone, it reports "Cancelled" (step 7). If the shift is still there, it shows the 6a1 message. Retrying the `DELETE` is also safe, because it is idempotent.

### UC-VOL-view-schedule: viewing my shifts and hours

```mermaid
sequenceDiagram
    actor V as Volunteer
    participant WEB as Web front end
    participant C as VolunteerShiftController
    participant SS as ScheduleService
    participant R as AssignmentRepository
    participant SH as ShiftService (ADM)
    participant L as LocationService (LOC)
    V->>WEB: View my schedule (step 1)
    WEB->>C: GET /api/v1/me/schedule
    C->>SS: scheduleFor(volunteerId from session)
    SS->>R: findActiveByVolunteer(volunteerId) (step 2)
    SS->>SH: shift times for those assignments
    SS->>L: location names and addresses
    SS->>SS: upcoming = shift end > clock.now(); finishedHours = sum of (end - start) for ended shifts; plannedHours = sum of (end - start) for upcoming shifts
    SS-->>C: upcoming shifts, finishedHours, plannedHours
    C-->>WEB: 200
    alt no upcoming shifts (2a)
        WEB-->>V: Empty schedule, finished hours, link to open shifts (2a1)
    else
        WEB-->>V: Upcoming shifts with location, address, date, time, plus finished and planned hours (step 3)
    end
```

## API contract

_[One row per endpoint, scheduled job, or message this area adds or changes, appended per use case. **This section is never dropped**: before the code exists it is the only thing the person (or agent session) building the front end and the one building the back end both read. Name the caller and who may call it, the request fields, the success response, and the error each extension returns. Leave field types and exact JSON to the agent unless a wrong guess would break something, such as a format another system depends on. Use the error shape your architecture-of-record's section 8.2 fixes; do not invent one here.]_

The architecture's §8.2.1 and §8.2.3 don't fix an error shape yet (see Open questions). So this table fixes only the error **codes** the front end branches on, not the response envelope around them.

| Endpoint | Caller (who may) | Request | Success | Errors, by extension |
|---|---|---|---|---|
| `GET /api/v1/shifts/open` | Any signed-in user | None | `200`: a list (empty is valid, 2a) of shifts with `openSlots > 0` that have not started, in a `planned` or `active` event. Each has shift id, location id, name and address, date, start and end in `America/Chicago`, `openSlots`, and `assignedToMe` | Not signed in: `401` |
| `POST /api/v1/shifts/{shiftId}/assignments` | Any signed-in user, for themself only. The volunteer comes from the session, never the path or body | None | `201`: new or reactivated assignment, with shift summary and `openSlots` after. `200`: the volunteer already held the slot (4b, 5b), same body with `alreadyAssigned: true`, and nothing is written | 4a: `409 SHIFT_FULL`. 4a: `409 SHIFT_NOT_OPEN` (started, or event not `planned`/`active`; never returned when the volunteer already holds the slot). Unknown shift: `404 SHIFT_NOT_FOUND`. 5a: `5xx`, no assignment written. Not signed in: `401` |
| `DELETE /api/v1/shifts/{shiftId}/assignments/me` | Any signed-in user, for their own assignment only | None | `200`: `{ assigned: false }`. Also returned when the assignment is already `CANCELLED` (duplicate cancel, no change) | PRE-2: `404 NOT_ASSIGNED` (never signed up for this shift). `409 SHIFT_STARTED` (shift start ≤ now). 6a: `5xx`, assignment unchanged. Not signed in: `401` |
| `GET /api/v1/me/schedule` | Any signed-in user, their own schedule only | None | `200`: upcoming shifts (active assignments whose shift has not ended, ordered by start), each with shift id, location name and address, date, start, end. Also `finishedHours` (shifts that have ended) and `plannedHours` (upcoming shifts), each a decimal number of hours | Not signed in: `401` |

There is no endpoint that takes a volunteer id. One volunteer cannot read or change another volunteer's assignments through this API, by construction (`SEC-least-privilege`, architecture §8.1 Authorization). Administrators see assignments through ADM's endpoints, which call `AssignmentQueryService`.

## Key decisions

_[Only the decisions a reader could not recover from the code: an invariant, who may do this and how it is enforced, a trade-off you chose, a reason you reused something rather than writing it. **Each one names what you chose, what you rejected, and why.** If you cannot name an alternative you considered, it probably was not a decision; leave it out._

_A decision that affects the whole system rather than this area is an architecture decision. It belongs in your architecture-of-record as a `KD-*` entry, not here.]_

**How overbooking is prevented.** Inside the sign-up transaction, VOL locks the shift's row (`ShiftService.lockForAssignment`, a pessimistic write lock, e.g. `SELECT … FOR UPDATE` or the chosen database's equivalent). Then it counts active assignments and inserts. Two concurrent claims for the last slot run one after the other: the second sees the count at capacity and gets `SHIFT_FULL`. Cancel takes the same lock, so a cancel and a claim on the same shift cannot interleave.
Rejected: a stored `filledCount` column decremented on cancel, which can drift from the real assignments after a partial failure. Rejected: an optimistic version check with retry, which the SRS §5.2 scoping note cut as more than this event's load needs (`SCA-event-capacity`: 200 volunteers), and which surfaces as spurious failures under the sign-up rush. Rejected: an in-process lock, which breaks as soon as the application runs more than one instance. Contention is per shift, so locking one shift row never blocks sign-ups for other shifts (`PER-form-submission`).

**How duplicates are prevented.** Two layers. (1) The service looks up the volunteer's row first and returns `200 alreadyAssigned` if it is active (4b, 5b, `FR-SAVE-duplicate-submission`). (2) A database **unique constraint on (shift_id, volunteer_id)** is the backstop if two identical requests ever get past the lock. A violation of that constraint is caught and answered as `alreadyAssigned`, not as an error.
Rejected: a client-generated idempotency key per submission, which adds a table and key handling for a case that "(shift, volunteer) is unique" already identifies exactly. Rejected: relying only on disabling the button, which does not survive a page reload or a retry after a lost response (`FR-SAVE-unknown-outcome`).

**4b and 5b get the same response.** The server cannot tell "already signed up and selected it again" (4b) apart from "retry of a sign-up that succeeded" (5b). The state is identical. Both get `200` with `alreadyAssigned: true`, and the UI's "You're already signed up for this shift" satisfies both 4b1 and 5b2.
Rejected: returning `409` for 4b, which would make a lost-response retry look like a failure and break `FR-SAVE-unknown-outcome`.

**Cancel soft-deletes, and signing up again reactivates the same row.** Keeping a `CANCELLED` row gives the coverage report its cancellation status (SRS §7.3.2), lets a duplicate cancel answer `200` instead of `404`, and keeps the unique constraint simple.
Rejected: deleting the row, which loses cancellation status and makes a duplicate cancel indistinguishable from "never assigned". Rejected: a new row per sign-up cycle, which needs a filtered unique index (database-specific, and the database is not yet chosen) and keeps a history no requirement asks for.

**Hours are computed on read, never stored, and shown as two numbers.** Both come from scheduled shift length (end − start), with no check-in, per the use case's assumption. `finishedHours` sums the volunteer's `ACTIVE` assignments whose shift has **ended** by the injected clock. That is `UI-volunteer-schedule`'s "hours credited so far". `plannedHours` sums the `ACTIVE` assignments whose shift has not ended. A cancelled assignment counts toward neither. Because hours are derived from the assignments, a cancel or a failed save can never leave them inconsistent.
Rejected: an hours column updated on sign-up, cancel, and shift end, which needs a scheduled job and can drift. Rejected: one total that adds upcoming shifts to worked ones, which would credit hours not yet worked. Rejected: finished hours alone, which hides the volunteer's planned contribution (team decision, resolves Open questions item 1).

**A volunteer cannot cancel once the shift has started.** Cancel is allowed up to the shift's start time, and refused with `SHIFT_STARTED` after. This matches step 2 ("upcoming shifts"). It also keeps a started or past shift's assignment, and so its credited hours and coverage record, from being erased after the fact. There is no earlier cut-off, because last-minute cancellations are exactly the client's pain point that this use case answers.
Rejected: no cut-off at all, which would let a volunteer wipe a worked shift from the hours and coverage report (see Open questions, item 2).

**A shift is "open" only if it has not started and its event is `planned` or `active`.** Volunteer recruitment runs for a month or more *before* the event (`AS-seasonal-readiness`). If sign-up were limited to `active` events, as the data dictionary's "only active records may accept live submissions" would suggest, sign-up could never happen. VOL does not read the location's status. The event runs for one move-out week, every location is open for all of it, and a location starts open when an administrator creates it.
Rejected: following the data dictionary literally (see Open questions, item 4). Rejected: filtering on `DonationLocation.status`, whose live values (`open`, `closed`, `maintenance`, `capacity`) say nothing about a shift weeks ahead, and which would hide shifts a location still needs staffed (`BR-location-staffing`).

**Any signed-in account can volunteer; VOL keeps no Volunteer table.** The assignment references the Authentication account id. Name and email come from that account when ADM's views need them.
Rejected: a separate `volunteer` profile with the data dictionary's `role` enum, which no `UC-VOL-*` reads.

## Data model

_[The **change** this area makes to the data: new tables or entities, new columns, the migration that adds them. Draw an ER diagram when there is more than one new table. Do not redraw the domain model; your [specification](../requirements/software-requirements-specification.md) owns it, so link it and show only the change. If the area changes nothing, say "No change" and give the reason in one line; that is a design decision too.]_

One new table, `shift_assignment`, owned by VOL:

| Column | Meaning |
|---|---|
| `id` | Surrogate key |
| `shift_id` | References ADM's shift table |
| `volunteer_id` | References Authentication's user account |
| `status` | `ACTIVE` or `CANCELLED` |
| `assigned_at` | When it last became `ACTIVE` (UTC, §8.2.2) |
| `cancelled_at` | When it last became `CANCELLED`, null while active |

Constraint: **unique (`shift_id`, `volunteer_id`)**. Index on (`shift_id`, `status`) for the count under lock, and on (`volunteer_id`, `status`) for the schedule.

This **realizes** the domain model's `Volunteer "*" --> "*" VolunteerShift : assigned to` association as its own record. The data dictionary's `VolunteerShift.assignmentStatus` (`assigned, claimed, cancelled, filled`) mixes the shift's state with each assignment's. Here "filled" is derived (active count ≥ required count), "claimed" and "assigned" are the same `ACTIVE` state (see the use-cases note on `UC-VOL-claim-open-shift`), and "cancelled" lives on the assignment. The shift table itself, including `required_count`, is ADM's.

## Reuse & cross-cutting

_[Which existing services, components, and conventions this area uses rather than rebuilding: authentication, the error handler, email, the clock, another area's service. Cite your architecture-of-record's section 8.2 for each convention by name rather than restating it.]_

- **Authentication** (architecture §8.1): `CurrentUser` for the volunteer id; every VOL endpoint requires sign-in (`FR-AUTH-enforce-permissions`).
- **Authorization** (§8.1): own-records-only is enforced by endpoint shape. No VOL endpoint accepts a volunteer id.
- **Error handling** (§8.2.1): a failed write is reported as "not recorded", never as success (`FR-SAVE-confirmed-success`). The `201`/`200` is sent only after commit.
- **Time** (§8.2.2): shift times are stored in UTC and shown in `America/Chicago` on a 12-hour clock. "Started" and "ended" are decided by the injected `Clock`.
- **Validation** (§8.2.5): VOL requests carry no body. The only inputs are the path's `shiftId` (must exist: `404`) and the session.
- **Persistence and concurrency** (§8.2.8): one transaction per sign-up or cancel, covering lock, check, and write. This document fixes the duplicate-submission strategy that §8.2.8 leaves provisional, for VOL.
- **Logging** (§8.2.7): log the shift id and refusal code on refusals. Never log the volunteer's email.
- **ADM** `ShiftService`, **LOC** `LocationService`: called through their services, never their repositories.

## Tests

_[One row per flow: the main success scenario and **every** extension of every use case this document realizes, appended per use case. Name the level (unit or integration) and what the test asserts. No code. This list is the definition of done that the reviewer approves; the agent writes these tests when it builds, and a reviewer can see at a glance whether an extension has no test.]_

| Use case, flow | Level | Asserts |
|---|---|---|
| UC-VOL-signup main | integration | A volunteer claims a shift with a free slot: `201`, one `ACTIVE` row, `openSlots` decreases by one |
| UC-VOL-signup step 2 | integration | The open list excludes full shifts, shifts that have started (fixed clock), and shifts in a `closed`/`archived` event. It includes a `planned` event's shifts |
| UC-VOL-signup step 2, `assignedToMe` | integration | A shift the volunteer holds that still has free slots is listed with `assignedToMe: true` |
| UC-VOL-signup 2a | integration | No open shifts: `200` with an empty list |
| UC-VOL-signup 4a, full | integration | A shift at capacity: `409 SHIFT_FULL`, nothing written |
| UC-VOL-signup 4a, race | integration | Two different volunteers claim the last slot concurrently: exactly one `201`, one `409 SHIFT_FULL`, active count equals required count |
| UC-VOL-signup 4a, not open | unit | A shift whose start equals the fixed clock's now: `409 SHIFT_NOT_OPEN` |
| UC-VOL-signup 4b | integration | Signing up for a shift already held: `200 alreadyAssigned`, still one row, `openSlots` unchanged |
| UC-VOL-signup 5a | integration | Write fails (simulated): `5xx`, no row, no success shown |
| UC-VOL-signup 5b, sequential | integration | The same `POST` sent twice: one `201`, one `200 alreadyAssigned`, one row, slot counted once (`FR-SAVE-duplicate-submission` verification) |
| UC-VOL-signup 5b, concurrent | integration | The same volunteer's two concurrent `POST`s: one row, slot counted once, neither response is an error |
| UC-VOL-signup 5b, filled by own retry | integration | The volunteer took the last slot, then retries: `200 alreadyAssigned`, not `409 SHIFT_FULL` |
| UC-VOL-signup 5b, retry after start | unit | The volunteer holds the slot and the retry arrives after the fixed clock passes the shift's start: `200 alreadyAssigned`, not `409 SHIFT_NOT_OPEN` |
| UC-VOL-signup step 6 | unit (front end) | Selecting a shift sends the `POST` with no confirm dialog. "Signed up" is shown only after a `201` or `200` |
| UC-VOL-signup, re-sign-up | integration | Cancel, then sign up again: the same row is `ACTIVE` again, still one row |
| UC-VOL-signup, unknown shift | integration | Nonexistent `shiftId`: `404 SHIFT_NOT_FOUND` |
| UC-VOL-cancel-shift main | integration | Cancel before start: `200`, row `CANCELLED`, shift's open slots increase by one and it appears in the open list |
| UC-VOL-cancel-shift 5a | unit (front end) | Declining the confirm sends no request |
| UC-VOL-cancel-shift 6a | integration | Write fails (simulated): `5xx`, row still `ACTIVE` |
| UC-VOL-cancel-shift, duplicate | integration | Cancel sent twice: both `200`, row `CANCELLED`, open slots increased once |
| UC-VOL-cancel-shift, PRE-2 | integration | Cancelling a shift never signed up for: `404 NOT_ASSIGNED` |
| UC-VOL-cancel-shift, started | unit | Shift start ≤ fixed clock's now: `409 SHIFT_STARTED`, row unchanged |
| UC-VOL-cancel-shift, unknown outcome | unit (front end) | The `DELETE`'s response is lost and the schedule no longer lists the shift: shows "Cancelled", not the 6a1 failure |
| UC-VOL-cancel-shift, no notification | integration | A successful cancel sends and queues no notification (`OI-7`) |
| UC-VOL-view-schedule main | integration | Lists only the volunteer's `ACTIVE` assignments whose shift has not ended, ordered by start, with location address |
| UC-VOL-view-schedule 2a | integration | No upcoming shifts: `200`, empty list, `finishedHours` still returned, `plannedHours` is 0 |
| UC-VOL-view-schedule, hours | unit | With a fixed clock: two ended 3-hour shifts and one upcoming 3-hour shift give `finishedHours` 6 and `plannedHours` 3. A cancelled shift, ended or upcoming, adds to neither |
| UC-VOL-view-schedule, display time | unit | A shift stored as 19:00 UTC shows as 2:00 p.m. on an event date in CDT (§8.2.2) |
| Authorization | integration | Every VOL endpoint without sign-in: `401`. No endpoint accepts or exposes another volunteer's id |

## Changes to the architecture-of-record

_[Designing an area against real code is when the architecture's guesses meet reality. If this design needs a component the map does not have, moves a responsibility, or adds a dependency, **change [the architecture-of-record](architectural-design.md) in the same pull request** and list each change here in one line. If nothing changed, write "None". A design that silently disagrees with the architecture leaves two maps, and the agent will follow whichever it reads last.]_

- [§5.2](architectural-design.md#52-use-case-areas-and-components), `VOL` row: *Depends on* changed from "Administration, Authentication, Notifications" to "Administration, Location Info, Authentication". VOL calls no notification code (`OI-7`). Notifications depends on VOL, not the reverse. Location names and addresses come from Location Info.

## Open questions & risks

_[What this design could not settle and who can, and what could go wrong that you are accepting. A question for the client also goes in [`OPEN-ISSUES.md`](../requirements/OPEN-ISSUES.md).]_

**Challenge-loop findings.** These are gaps in the requirements. The design picks a default for each, but the answer belongs in the specification.

1. **What counts as credited hours: ended shifts only, or all signed-up shifts?** The use case says "total hours"; the UI spec says "hours credited so far". *Resolved by the team:* show both, as `finishedHours` and `plannedHours` (see Key decisions). *Optional team fix:* make `UC-VOL-view-schedule` step 3 say so.
2. **Can a volunteer cancel after a shift has started, or must they cancel some time before it?** The use case is silent. Design default: up to the start time, no earlier cut-off. *Client question.*
3. **May one volunteer hold two shifts that overlap in time?** Nothing forbids it, but a double-booked volunteer makes one location look staffed when it is not (`BR-location-staffing`). Design default: **not blocked**, because no requirement says so and adding a refusal would invent a rule. If the client says no, it becomes a new extension in `UC-VOL-signup` and a `409 SHIFT_OVERLAP` row here. *Client question.*
4. **Data dictionary vs. recruitment window.** `ReFrogEvent.status`: "only active records may accept live submissions" contradicts sign-up during recruitment, which happens before the event. *Team fix to the SRS §7.2:* sign-ups are accepted while the event is `planned` or `active`.
5. **Data dictionary's `VolunteerShift.assignmentStatus`** conflates shift and assignment state (see Data model). *Team fix to the SRS §7.2:* move the status onto the assignment.
6. **`UC-VOL-signup` 4b and 5b describe the same system state.** Not a defect, but the use case could say the system's answer is the same. *Optional team fix.*

**Coordination with other areas** (contract, not client):

- **ADM (Ananye):** `ShiftService.findSignupCandidates`, `findById`, `lockForAssignment` as described in Components, returning start and end as UTC instants converted from `America/Chicago`. ADM's `UC-ADM-manage-shifts` 6a also has to decide what happens to `ACTIVE` assignments when a shift is edited below its active count or removed. VOL does not handle this; it only reads `required_count`. Whatever ADM decides, it must not leave active count > required count without saying so.
- **Notifications / `FR-NOTIFY-current-state`:** the reminder rechecks with `AssignmentQueryService.isActive` before sending.
- **IDV:** architecture §5.2 makes "registered volunteer" a route to shopping eligibility. VOL exposes nothing for that yet, and any signed-in account can sign up for a shift. IDV's design defines "registered volunteer" and adds any `AssignmentQueryService` method it needs. Holding a shift alone should not be read as proof of `BR-shopper-tcu-affiliation`.
- **Team-wide:** the error envelope in architecture §8.2.1/8.2.3 is still provisional. VOL fixes only the codes. The team should fix the envelope once, before the first endpoint is built.

**Risks accepted:**

- The row lock relies on the chosen database supporting pessimistic row locks inside a transaction. Every likely candidate under `CO-azure-database-hosting` does (Azure SQL, Azure PostgreSQL). If the stack choice makes this awkward, the fallback is a conditional `UPDATE … WHERE active_count < required_count` on a counter, with the drift risk named in Key decisions.
- `FR-NOTIFY-preserve-business-state` mentions notifications after a sign-up or cancellation. None exist today (`OI-7`), so VOL has nothing to preserve. If one is added later, the saved change must stand regardless.

---

## Working this document with your agent

_[Delegate: drafting the diagrams from your sketch and the use case; listing the existing files a class should extend; filling the contract rows' request and response fields; proposing the test row for each extension; drafting the rejected alternative for a decision you have already made; checking that every identifier this document cites exists in the document that owns it._

_Keep human: which side of the "would a wrong guess break something" line each detail falls on; every key decision; and the approval, which is a teammate's review of the pull request, not the drafter's own reading._

_**The specific failure to watch for: scope that nobody asked for.** An agent drafting a design adds what designs it has read usually have: an admin override, a retry queue, a status field, a notification "while we are here". Each looks reasonable and none is in your use case, and once it is in the design the agent will build it, test it, and you will maintain it. Read the sequence labels: every interaction should name the use case step it implements. An interaction with no step is either a missing requirement, which goes to your specification and your client, or scope creep, which comes out.]_

---

## The questions test (before you open the pull request)

_[Start a **fresh** agent session in plan mode, so it carries nothing from the session that drafted this document. Give it only the use case, the business rules it cites, your architecture-of-record's section 8, and this document, and ask:_

> _List every point you would still have to guess to implement `<UC-ID>`. For each, say what you would guess and whether a wrong guess could violate a requirement. Do not write code._

_For each item on its list, either fix this document or keep the guess and write one line saying why a wrong guess breaks no requirement. Paste the list, with your answer to each item, into the pull request description under How it was verified, inside a collapsed `<details><summary>Questions test</summary> … </details>` block so the description stays short. The design is ready when nothing on the list could break a requirement.]_

For VOL, `<UC-ID>` is `UC-VOL-signup`, `UC-VOL-cancel-shift`, and `UC-VOL-view-schedule`. The rules to give the session are `BR-volunteer-assignment`, `BR-location-staffing`, and `BR-event-schedule`.
