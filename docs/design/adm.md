# Administration Design

**Project:** ReFrog  
**Team:** Team 03  
**Area:** `UC-ADM`  

> **Realizes:** `UC-ADM-view-dashboard`, `UC-ADM-manage-shifts`, `UC-ADM-notify-volunteers`, `UC-ADM-review-volunteer`  
> **Depends on:** `BR-location-staffing`, `BR-event-schedule`, `BR-volunteer-assignment`; `FR-AUTH-enforce-permissions`, `FR-VAL-server-validation`, `FR-VAL-field-feedback`, `FR-SAVE-confirmed-success`, `FR-NOTIFY-preserve-business-state`; `SEC-role-based-access`, `SEC-audit-log`, `ROB-duplicate-submission`, plus the applicable requirements in the [specification](../requirements/software-requirements-specification.md).  
> **See:** [Use cases](../requirements/use-cases.md), [Software Requirements Specification](../requirements/software-requirements-specification.md), [Business Rules](../requirements/business-rules.md), [Architecture of record](architectural-design.md).  
> **Designed against:** **TBD: insert the actual commit SHA of `main` reviewed before opening the PR.** Record the actual main commit used to check the requirements and architecture.

---

## Overview

The Administration (`ADM`) area lets authorized ReFrog organizers view a consolidated dashboard of volunteer coverage, donations and shopping across locations; configure shifts for the event; deliberately send volunteers a message about open shifts, and review new volunteers’ pending shift claims. It lives inside the Application container defined by [architecture section 5.1](architectural-design.md#51-containers) and implements the Administration component in [section 5.2](architectural-design.md#52-use-case-areas-and-components). It relies on cross-cutting Authentication for administrator identity and authorization, reads Volunteer Scheduling assignments and Donation and Shopping event records, reads active locations and event settings, and calls the shared Notifications component for administrator-triggered delivery. **ADM owns shift definitions and review decisions, but VOL owns the `shift_assignment` records and controls all assignment status changes. Authentication owns accounts and roles. ADM does not own volunteer sign-ups, donation/shopping entry, authentication, or the email provider.** Edits to an existing assigned shift require an explicit safeguard: impacted volunteers must be shown before changes are confirmed; the client has not yet decided the notification policy for those edits.

## Components & classes

These are **planned application boundaries**, not claims that the files already exist. The application framework, directory layout, database technology and provider adapter remain provisional in the architecture; the paths below are proposed logical paths and must be aligned to the repo conventions before implementation.

```mermaid
classDiagram
    direction LR
    class AdminController {
        <<new>>
        +getDashboard()
        +createShift(request)
        +previewShiftChange(shiftId, request)
        +changeShift(shiftId, request)
        +sendOpenShiftNotice(audience)
        +listPendingVolunteers()
        +approveVolunteer(volunteerId)
        +denyVolunteerSignup(volunteerId, shiftId)
    }
    class AdminService {
        <<new>>
        +dashboardSummary()
        +createValidatedShift(data)
        +assessShiftChange(id, data)
        +applyConfirmedShiftChange(id, data)
        +setLocationSlotDefault(eventId, locationId, requiredCount)
    }
    class VolunteerReviewService {
        <<new, administration>>
        +listPending() List~PendingVolunteer~
        +approve(volunteerId)
        +deny(volunteerId, shiftId)
    }
    class ShiftService {
        <<new, administration / contract with VOL>>
        +findSignupCandidates(now) List~Shift~
        +findById(shiftId) Shift
        +lockForAssignment(shiftId) Shift
    }
    class AssignmentService {
        <<reused, volunteer>>
        +confirmPending(volunteerId)
        +denyPending(volunteerId, shiftId)
    }
    class AssignmentQueryService {
        <<reused, volunteer / read-only>>
        +isActive(shiftId, volunteerId) bool
        +countsByShift(shiftIds) Map
    }
    class AuthenticationAccount {
        <<reused, authentication>>
        +volunteerStatus NONE_OR_APPROVED
    }
    class AdminNotificationService {
        <<new>>
        +notifyOpenShifts(audience)
    }
    class AdminRepository {
        <<new>>
        +listLocationsAndEvents()
        +readShiftDefinitions()
        +saveShift()
        +readActivityAggregates()
    }
    class Authz {
        <<reused / cross-cutting>>
        +requireAdministrator(session)
    }
    class VolunteerScheduling {
        <<reused / VOL>>
        +getAssignmentsAndCoverage()
        +listVolunteerRecipients(audience)
    }
    class DonationData {
        <<reused / DON>>
        +getCountsByLocation()
    }
    class ShoppingData {
        <<reused / SHP>>
        +getCountsByLocation()
    }
    class Notifications {
        <<reused / cross-cutting>>
        +send(recipientBatch, message)
    }
    class Audit {
        <<reused / cross-cutting>>
        +recordAdminChange()
    }
    AdminController --> Authz
    AdminController --> AdminService
    AdminController --> AdminNotificationService
    AdminController --> VolunteerReviewService
    AdminService --> ShiftService
    AdminService --> AssignmentQueryService
    VolunteerReviewService --> AssignmentService
    VolunteerReviewService --> AuthenticationAccount
    VolunteerReviewService --> Notifications
    AdminService --> AdminRepository
    AdminService --> VolunteerScheduling
    AdminService --> DonationData
    AdminService --> ShoppingData
    AdminService --> Audit
    AdminNotificationService --> VolunteerScheduling
    AdminNotificationService --> AdminRepository
    AdminNotificationService --> Notifications
    AdminNotificationService --> Audit
```

| Planned file / boundary | New or reused | Responsibility |
|---|---|---|
| `administration/AdminController` | New | Admin-only HTTP entry points and field-level error presentation; delegates business decisions |
| `administration/AdminService` | New | Dashboard aggregation, per-location default staffing configuration, per-shift override validation, existing-shift change preview and protected writes |
| `administration/ShiftService` | New (ADM-owned) | `findSignupCandidates(now)`, `findById(id)`, `lockForAssignment(id)`; supplies stable shift IDs, UTC instants and row lock for VOL transactions |
| `administration/VolunteerReviewService` | New (ADM-owned) | Reviews pending volunteers; approves the account and confirms all its pending claims in one transaction, or denies one claim; dispatches outcome message after commit |
| `volunteer/AssignmentService` / `AssignmentQueryService` | Reused, VOL-owned | `confirmPending`, `denyPending` and `countsByShift` / `isActive`; ADM never edits VOL records directly |
| Authentication user account | Reused, Authentication-owned | `volunteerStatus = NONE | APPROVED`; only ADM review awards the Volunteer role |
| `administration/AdminNotificationService` | New | Explicit send action, audience selection, available-shift context, delivery result and retry behavior |
| `administration/AdminRepository` | New | Persistence queries for event/location configuration, shift definitions and dashboard aggregates |
| Authentication/authorization middleware | Reused, cross-cutting | Enforce administrator authorization on *every* ADM endpoint server-side |
| Volunteer Scheduling service | Reused, `VOL` | Own sign-ups, cancellations, assignments, assignment records and derived coverage (ACTIVE and PENDING counted separately) |
| Donation and Shopping services/read models | Reused, `DON`/`SHP` | Source-of-truth item activity used in dashboard totals |
| Notification delivery adapter | Reused, cross-cutting | Selected external delivery provider and its failure handling |
| Shared audit facility | Reused, cross-cutting | Capture administrator changes and authentication audit events without secrets |

**Integration boundary fixed by VOL:** ADM must use `AssignmentQueryService.countsByShift(shiftIds)` for active/pending counts, and VOL must use `ShiftService.findSignupCandidates(now)`, `findById(id)` and `lockForAssignment(id)`. `ShiftService` returns shift start/end as UTC instants; user-facing time is `America/Chicago`. One location-level `required_count` default is configured per event; a shift copies it at creation and may be overridden by an administrator. `ACTIVE + PENDING <= shift.required_count` must hold after every successful change. Assignment approval/denial goes through VOL's `AssignmentService`, **never** direct writes to `shift_assignment`.

## Sequence

### UC-ADM-view-dashboard: View the event dashboard

```mermaid
sequenceDiagram
    actor A as Administrator
    participant UI as Admin web interface
    participant API as AdminController
    participant AUTH as Authorization
    participant ADM as AdminService
    participant LOC as Location/event store
    participant VOL as Volunteer Scheduling
    participant DON as Donation records
    participant SHP as Shopping records
    A->>UI: Open dashboard (step 1)
    UI->>API: GET /api/admin/dashboard
    API->>AUTH: requireAdministrator (step 2)
    alt Not an administrator (extension 2a)
        AUTH-->>API: Denied
        API-->>UI: 403 Forbidden (or 401 if not signed in)
        UI-->>A: Access denied
    else Authorized
        API->>ADM: dashboardSummary (step 3)
        ADM->>LOC: List configured locations
        ADM->>VOL: AssignmentQueryService.countsByShift(shiftIds) (ACTIVE and PENDING separately)
        ADM->>DON: Count donated items by location
        ADM->>SHP: Count shopped items by location
        alt Location has no recorded activity (extension 3a)
            ADM->>ADM: Keep location; set recorded counts to zero and label no activity
        end
        ADM->>ADM: held=ACTIVE+PENDING; open=max(required-held,0); mark shortage and pending approval separately (step 4)
        ADM-->>API: Per-location summary
        API-->>UI: 200 Dashboard data
        UI-->>A: Show counts and unfilled shifts
    end
```

The dashboard includes **every configured relevant location even when no donation or shopping records exist**. Counts represent stored records, not estimated physical inventory; the client said donated goods are not individually tagged. The exact definition of the event/date filter must use the approved event configuration, not an invented calendar window.

### UC-ADM-manage-shifts: Create and configure volunteer shifts

```mermaid
sequenceDiagram
    actor A as Administrator
    participant UI as Admin web interface
    participant API as AdminController
    participant AUTH as Authorization
    participant SVC as AdminService
    participant DB as Shift/location/event store
    participant VOL as Volunteer Scheduling
    participant AUD as Shared audit
    A->>UI: Choose create shift (step 1)
    UI->>API: Load location and event choices (step 2)
    API->>AUTH: requireAdministrator
    alt Not an administrator (extension 2a)
        AUTH-->>API: Denied
        API-->>UI: 401/403
        UI-->>A: Access denied
    else Authorized
        API-->>UI: Active locations, operating windows
        UI-->>A: Show event/location default requiredCount, operating days, date/time; allow per-shift override
        A->>UI: Enter details and confirm (step 3)
        UI->>API: POST /api/admin/shifts
        API->>AUTH: requireAdministrator
        API->>SVC: createValidatedShift (step 4)
        SVC->>DB: Validate operating days/time and positive location-default or overridden requiredCount
        alt Invalid details (extension 4a)
            SVC-->>API: Validation error by field
            API-->>UI: 422 INVALID_SHIFT_DETAILS
            UI-->>A: Preserve inputs; request corrections (step 3)
        else Valid details
            SVC->>DB: Persist shift in one transaction
            alt Save failed (extension 4b)
                DB-->>SVC: Failure / transaction rollback
                SVC-->>API: Save not confirmed
                API-->>UI: 503/500 SAVE_FAILED
                UI-->>A: Not saved; retry possible
            else Saved
                SVC->>AUD: Record shift creation
                SVC-->>API: Created shift
                API-->>UI: 201 Created (step 5)
                UI-->>A: Shift available for sign-up
            end
        end
    end
    opt Edit/remove existing shift with volunteers (extension 6a)
        A->>UI: Request change to existing shift
        UI->>API: Preview affected assignments
        API->>AUTH: requireAdministrator
        API->>VOL: Read ACTIVE and PENDING held counts/impacted assignments
        API-->>UI: Impact preview; confirmation required
        UI-->>A: Show affected volunteers before confirming
        Note over UI,API: Change must not silently remove assigned volunteers;
        Note over UI,API: notification policy requires client approval.
    end
```

**Capacity and concurrency contract:** When configuring an event, an administrator sets a positive default maximum volunteer count for each location. Each new shift copies that location-event default into its `required_count`; admins may override individual shifts. The shift row is the lock VOL uses to serialize sign-ups and cancellations (`lockForAssignment`). Editing capacity to a number below the current **held** count (`ACTIVE + PENDING`) or deleting a shift with held assignments must be **rejected with `409 CAPACITY_BELOW_HELD` / `409 ASSIGNED_SHIFT_CHANGE_UNRESOLVED`**, with no partial write. Changing the location default affects only shifts created afterwards unless the team expressly defines another rule; existing shift capacities are not silently rewritten.

**Scope note:** The use case's main success flow only specifies *creation*. The extension mentions editing/removing already-assigned shifts but does not settle what notification, reassignment, or cancellation is allowed. This design specifies the mandatory impact preview and **does not claim** approval for a destructive edit workflow. Until resolved, requests that would alter an assigned shift should be blocked after preview rather than silently applied.

### UC-ADM-notify-volunteers: Notify volunteers of open shifts

```mermaid
sequenceDiagram
    actor A as Administrator
    participant UI as Admin web interface
    participant API as AdminController
    participant AUTH as Authorization
    participant SVC as AdminNotificationService
    participant VOL as Volunteer Scheduling
    participant MSG as Shared Notifications
    A->>UI: Choose notify about open shifts (step 1)
    UI-->>A: Select everyone or no sign-ups (step 2)
    A->>UI: Choose audience and confirm (step 3)
    UI->>API: POST /api/admin/notifications/open-shifts
    API->>AUTH: requireAdministrator
    alt Not an administrator
        AUTH-->>API: Denied
        API-->>UI: 401/403
    else Authorized
        API->>SVC: notifyOpenShifts(audience) (step 4)
        SVC->>VOL: Resolve audience and open shift summary
        SVC->>MSG: Send using configured delivery channel
        alt Delivery unavailable / not accepted (extension 4a)
            MSG-->>SVC: Failure
            SVC-->>API: Send not confirmed
            API-->>UI: 503 NOTIFICATION_NOT_SENT
            UI-->>A: Not sent; retry after checking
        else Provider accepts delivery
            MSG-->>SVC: Accepted / provider reference
            SVC-->>API: Send accepted
            API-->>UI: 200 Accepted for delivery (step 5)
            UI-->>A: Notification submitted
        end
    end
```

**Delivery semantics:** A provider accepting a message does **not** prove that it reached every volunteer. Report provider acceptance accurately and log failures, avoiding a false statement that every message arrived. An administrator must trigger this operation; **cancelling a shift never automatically broadcasts a notice**. Provider, recipient contact preferences, and precise bulk-send partial-failure behavior require integration decisions; see open questions below.

### UC-ADM-review-volunteer: Approve volunteer or deny a pending shift claim

Administration reviews volunteer participation requests. A new volunteer may have pending shift sign-ups. An administrator either approves the volunteer or denies an individual request. The implementation uses the assignment service to apply each decision, rather than editing volunteer assignments directly. Confirm the final step numbers in `use-cases.md` before merging.

```mermaid
sequenceDiagram
    actor A as Administrator
    participant UI as Admin web interface
    participant C as AdminController
    participant VR as VolunteerReviewService (ADM)
    participant AUTH as Authentication account/role store
    participant VOL as AssignmentService (VOL)
    participant N as Notifications component
    A->>UI: Open pending volunteer review list
    UI->>C: GET /api/v1/admin/volunteer-reviews
    C->>VR: listPending() (admin-only)
    VR-->>UI: Accounts with pending claims and shift details
    alt Approve person (step 4)
        A->>UI: Approve volunteer
        UI->>C: POST /api/v1/admin/volunteers/{volunteerId}/approve
        C->>VR: approve(volunteerId)
        Note over VR,VOL: One database transaction
        VR->>AUTH: Set volunteerStatus=APPROVED and Volunteer role
        VR->>VOL: confirmPending(volunteerId)
        VOL-->>VR: All PENDING claims become ACTIVE; held counts unchanged
    else Deny this pending claim (3a1)
        A->>UI: Deny volunteer's one shift claim
        UI->>C: POST /api/v1/admin/volunteers/{volunteerId}/shifts/{shiftId}/deny
        C->>VR: deny(volunteerId, shiftId)
        Note over VR,VOL: One database transaction
        VR->>VOL: denyPending(volunteerId, shiftId)
        VOL-->>VR: Only selected claim becomes DENIED; slot released; account unchanged
    end
    alt Database write or commit fails (4a)
        VR-->>C: Error; account and claims unchanged
        C-->>UI: 5xx; review not saved
    else Already decided (4b)
        VR-->>C: No change (idempotent)
        C-->>UI: 200 already decided; no duplicate notification
    else Transaction committed
        VR->>N: Notify person of approval/denial (step 5 or 3a2), after commit
        alt Notification delivery fails
            N-->>VR: Failure; record/report to admin; decision remains committed
        else Provider accepts
            N-->>VR: Acceptance receipt, not guaranteed delivery
        end
        C-->>UI: 200 decision saved with notification outcome
    end
```

**Concurrency and ownership:** Approval sets `volunteerStatus=APPROVED` once and calls VOL’s `confirmPending` in the **same transaction**. Denial calls `denyPending(volunteerId, shiftId)` and leaves the user not approved and all other assignments unchanged. `DENIED` is terminal for that one shift/person pair, so subsequent sign-ups for the same shift are rejected by VOL. The operation never increases held counts, because `PENDING → ACTIVE` is one-for-one and `PENDING → DENIED` releases a slot. Send the decision message only after a successful commit; message failure never reverses the approval/denial. The account and VOL database must support a shared transaction; if not, this is an architecture decision to resolve **before implementation**, not a silent eventual-consistency assumption.

## API contract

The endpoints below are **proposed contract names**, not a claim that these routes exist. Final code and OpenAPI should use the architecture's shared response/error conventions (section 8.2), with the same logical outcomes.

| Endpoint or job | Caller (who may) | Request | Success | Errors, by extension |
|---|---|---|---|---|
| `GET /api/admin/dashboard` | Authenticated ReFrog administrator | Optional approved event filter (default current configured event) | `200`: all configured locations, recorded donation counts, shopping counts, shift required count, `ACTIVE` count, `PENDING` count, held count (`ACTIVE+PENDING`), available slots and unfilled indicators; zero/no-activity locations included | `401` unauthenticated; `403` non-admin (2a); `503` if required source unavailable, **not** a misleading partial “current” total |
| `GET /api/admin/shift-options` | Authenticated administrator | None or configured event | `200`: selectable active locations, operating days/windows, event dates | `401`/`403`; `503` configuration unavailable |
| `POST /api/admin/shifts` | Authenticated administrator | Location reference; shift start/end (local event time or approved representation); required volunteer count (defaults to location/event setting unless per-shift override); request-retry key if architecture standardizes it | `201`: created shift ID, location, start/end and capacity, available to `VOL` | `401`/`403` (2a); `422 INVALID_SHIFT_DETAILS` (4a); `409` overlapping/duplicate shift where prohibited by confirmed model; `503/500 SAVE_FAILED` (4b). Do not claim creation without committed storage |
| `GET /api/admin/shifts/{shiftId}/impact` | Authenticated administrator | Intended edit/removal (preview only) | `200`: affected assigned volunteers/assignments and whether the change is currently permitted | `401`/`403`; `404` unknown shift; `409` stale shift state |
| `PATCH /api/admin/shifts/{shiftId}` / `DELETE /api/admin/shifts/{shiftId}` | Authenticated administrator, **only once client policy is approved** | Explicit confirmed edit/removal and expected version | Shift updated/removed only when policy allows; audit entry | `401`/`403`; `404`; `409 CAPACITY_BELOW_HELD` if new cap would be less than ACTIVE+PENDING; `409 ASSIGNED_SHIFT_CHANGE_UNRESOLVED` for other destructive edits until client decides extension 6a; validation/save errors as above |
| `PUT /api/v1/admin/events/{eventId}/locations/{locationId}/staffing` | Authenticated administrator | Positive `defaultRequiredCount` per location/event, location open days/windows | `200`: saved default for *new* shifts; existing shifts remain unchanged | `401`/`403`; `422 INVALID_STAFFING_CONFIG`; `5xx` save failure |
| `GET /api/v1/admin/volunteer-reviews` | Authenticated administrator | Optional event filter | `200`: accounts with pending claims, contact/display fields permitted for review, claimed shifts and counts | `401`/`403`; `503` if required source unavailable |
| `POST /api/v1/admin/volunteers/{volunteerId}/approve` | Authenticated administrator | No body; person chosen from review list | `200`: `volunteerStatus=APPROVED`, all current PENDING assignments become ACTIVE atomically; idempotent repeat changes nothing | `401`/`403`; `404` unknown account; `5xx` transaction failure; notification failure reported separately without rollback |
| `POST /api/v1/admin/volunteers/{volunteerId}/shifts/{shiftId}/deny` | Authenticated administrator | One PENDING signup to deny | `200`: specified signup becomes DENIED, freeing one held slot; account and other sign-ups unchanged; idempotent repeat changes nothing | `401`/`403`; `404` unknown account/assignment; `409` if approval already made claim ACTIVE (policy needs client/team decision); `5xx` failure; notification status separate |
| `POST /api/admin/notifications/open-shifts` | Authenticated administrator, explicit UI confirmation | Audience = `all_volunteers` or `no_shift_signups`; no arbitrary recipient list or automatic trigger | `200` or `202`: provider accepted sending; show number of resolved recipients if available, not guaranteed delivery | `401`/`403`; `422` invalid audience; `503 NOTIFICATION_NOT_SENT` (4a); if partial sends occur, return a **truthful partial result** and do not blindly resend to successful recipients |

**Cross-area contract to confirm:** Shift creation must expose one stable identifier and shared interpretation of required count / assigned count to `VOL`; donation and shopping provide aggregated counts by location and event with no personally identifying data needed by the dashboard. Recipient selection is owned by `VOL`/identity records, while the shared Notification adapter sends messages. API authentication derives administrator identity from the verified session/token; a request body cannot self-declare administrator privileges.

## Key decisions

**1. Dashboard is computed from authoritative records, not an independent mutable counter.** Choose: combine configured locations with persisted donation, shopping and volunteer-assignment records, showing zeros for locations with no activity. Reject: storing a second independent dashboard total that may drift from submitted records. Reason: dashboard counts must agree with confirmed saves and include otherwise-empty sites (`UC-ADM-view-dashboard` steps 3–4, 3a; `FR-SAVE-confirmed-success`).

**2. Central shift definition; Volunteer Scheduling owns enrollments.** Choose: store shift configuration once and have `VOL` reference it, with assigned count read from authoritative assignments. Reject: a separate ADM roster or sign-up counter. Reason: two owners for capacity/assignments cause misleading coverage and booking conflicts (`UC-ADM-manage-shifts`, `UC-VOL-signup`, `BR-location-staffing`).

**3. Server-side, per-location schedule validation.** Choose: require real active location, valid event day/time, positive capacity, and location-specific operating windows in the server transaction; present field corrections in the UI. Reject: a universal 2-volunteer minimum or client-only form checks. Reason: King Family Commons may need 3, other sites 2, and operating days differ (`BR-location-staffing`, `BR-event-schedule`, extension 4a). Use `America/Chicago` per architecture §8.2.2.

**4. Safe assigned-shift changes.** Choose: preview affected volunteer assignments and block changes that would invalidate those assignments until notification/cancellation policy is approved. Reject: silently deleting or moving occupied shifts. Reason: extension 6a expressly requires showing affected volunteers but has an unconfirmed subsequent workflow.

**5. Notifications are deliberate, not cancellation-triggered.** Choose: administrator explicitly chooses one of two audiences, confirms, and invokes the shared notification service. Reject: broadcasting every cancellation or every remaining empty slot. Reason: the client explicitly warned against overwhelming volunteers (`UC-ADM-notify-volunteers`; `OI-7` resolved).

**6. “Sent” means provider-accepted, not necessarily delivered.** Choose: show confirmation only for an acknowledged dispatch, and show failure/partial failure accurately. Reject: treating a request attempt as delivery success or blindly retrying after an ambiguous provider timeout. Reason: extension 4a and robustness requirements; provider and retry semantics still need agreement.

**7. Authorization and auditing are shared.** Choose: enforce administrator role within the server security boundary and record shift/configuration changes through the shared audit facility. Reject: hiding admin buttons as the only security check or building separate ADM authentication/audit stores. Reason: `SEC-role-based-access`, `FR-AUTH-enforce-permissions`, `SEC-audit-log`.

**8. Location default and shift override must not invalidate claims.** Choose: store the per-event per-location default slot count, copy it into each newly created shift, allow explicit per-shift override, and reject changes below `ACTIVE + PENDING` held assignments. VOL locks the shift row when signing up/cancelling, and ADM acquires the same row lock while editing its cap. Reject: editing capacity without reading pending claims, and bulk updating existing shift caps when a default changes. Reason: this is a consistent limit on staffing reservations.

**9. Review approval is per person; denial is per claim.** Choose: in one database transaction grant the Authentication Volunteer role and call VOL `confirmPending(volunteerId)`; for denial call `denyPending(volunteerId, shiftId)` while leaving role and other claims untouched. Reject: per-claim approval, treating PENDING as ACTIVE for hours/reminders, direct ADM edits to `shift_assignment`, and denying the whole account. Reason: approvals should not create inconsistent volunteer permissions or shift records. Pending reservations count against capacity.

**10. Review notification cannot undo a committed decision.** Choose: notify after successful approval/denial, report provider acceptance/failure and avoid repeat sends on idempotent review. Reject: delivering before commit, or rolling back a business decision because messaging failed (`FR-NOTIFY-preserve-business-state`).

## Data model

These are logical changes and ownership boundaries, **not a final migration or mandated schema**. The actual repository schema and framework have to be inspected at the recorded `main` commit before migrations are authored.

```mermaid
erDiagram
    EVENT ||--o{ LOCATION_OPERATING_WINDOW : configures
    LOCATION_OPERATING_WINDOW ||--o{ SHIFT : defaults_capacity
    LOCATION ||--o{ LOCATION_OPERATING_WINDOW : has
    EVENT ||--o{ SHIFT : schedules
    LOCATION ||--o{ SHIFT : hosts
    SHIFT ||--o{ VOLUNTEER_ASSIGNMENT : receives
    USER_ACCOUNT ||--o{ VOLUNTEER_ASSIGNMENT : books
    LOCATION ||--o{ DONATION_RECORD : receives
    LOCATION ||--o{ SHOPPING_RECORD : records
    ADMINISTRATOR ||--o{ ADMIN_AUDIT_EVENT : acts
    EVENT {
        string id
        date start_date
        date end_date
    }
    LOCATION {
        string id
        boolean active
    }
    LOCATION_OPERATING_WINDOW {
        string event_id
        string location_id
        string operating_days_and_times
        int default_required_count
    }
    SHIFT {
        string id
        string event_id
        string location_id
        datetime starts_at
        datetime ends_at
        int required_count
    }
    VOLUNTEER_ASSIGNMENT {
        string shift_id
        string volunteer_id
        string status "PENDING ACTIVE CANCELLED DENIED"
    }
    USER_ACCOUNT {
        string id
        string volunteer_status "NONE or APPROVED"
    }
    DONATION_RECORD {
        string location_id
        int recorded_item_count
    }
    SHOPPING_RECORD {
        string location_id
        int recorded_item_count
    }
    ADMIN_AUDIT_EVENT {
        string actor_id
        string action
        datetime occurred_at
    }
```

- **ADM new or revised data:** shift definition referencing an event and location, with UTC start/end and `required_count` copied from the location/event default or explicitly overridden; configuration for per-location operating days/hours where not already stored; admin audit entries as required by shared security. Choose one canonical home for event/location data with `LOC` before creating tables.
- **Reused data:** Volunteer `shift_assignment` records (`VOL`), Authentication account/role records (volunteer `NONE`/`APPROVED`) and contact data, donation records (`DON`), and shopping records (`SHP`). **No new personal records are needed solely for dashboard totals.**
- **No independent “available slots” table:** compute `max(required_count – (ACTIVE + PENDING), 0)` from one committed snapshot, while showing over-capacity as a separate problem if it occurs. VOL implements sign-up capacity checks while holding the ADM shift row lock; ADM takes that same lock for capacity edits and rejects capacities below held count. `ACTIVE` alone counts for approved staffing/hours; `PENDING` holds capacity but is separately shown as awaiting approval.
- **Notification outcome:** the shared Notifications component may need a minimal dispatch ID, audience and accepted/failed status for accurate reporting and retry; this is an **integration question**, not authorization to build a broad campaign/marketing or analytics system. Any retained recipient-level delivery data follows approved privacy and retention policy (`OI-6`).
- **Migration:** Add or adapt shift/event-location configuration in the chosen database schema, preserving existing records and foreign keys. The migration name and physical columns are intentionally deferred until the actual codebase and approved stack are available.

**VOL contract is authoritative:** ADM's shift table holds `required_count`, and VOL's separate `shift_assignment` table holds `PENDING`, `ACTIVE`, `CANCELLED`, or `DENIED` with unique `(shift_id, volunteer_id)`. ADM does **not** re-create that table. Authentication accounts gain `volunteerStatus=NONE | APPROVED`; approval sets this account status/role in the same transaction in which VOL confirms pending assignments. A location-level event staffing default is stored with the location/event configuration, whose exact canonical owner is to be agreed with LOC.

## Reuse & cross-cutting

- **Authentication/authorization:** Use the architecture's shared signed-in identity/administrator-role mechanism, not an email string supplied by the browser; apply server-side permission checks to reads and writes (architecture §8.1).
- **Time:** Store comparable machine-readable timestamps, interpret/display event time in `America/Chicago`, test with a fixed clock (architecture §8.2.2).
- **Errors and input:** Reuse shared structured error response, server-side input validation, field messages and input preservation (architecture §§8.2.1, 8.2.3, 8.2.5).
- **Persistence and concurrency:** Treat each shift create/edit as one atomic business operation; no acknowledged success before commit. Guard duplicate retries and races with appropriate database constraints/transactions (architecture §8.2.8).
- **Logging/auditing/privacy:** Reuse centralized safe logging and administrator audit events; avoid exposing volunteers' contact data in dashboard or routine error messages. Retention is client/TCU governed, not guessed (architecture §§8.2.6–8.2.9; `OI-6`).
- **VOL/ADM boundary:** Reuse `ShiftService.findSignupCandidates(now)`, `findById(id)`, `lockForAssignment(id)` as called by VOL; `AssignmentQueryService.countsByShift` and `AssignmentService.confirmPending/denyPending` as provided by VOL. Hold the same shift lock for capacity changes.
- **Volunteer approval:** Authenticate administrator; update account Volunteer role and VOL PENDING rows atomically; after commit send review notice and preserve decision if sending fails.
- **Notifications:** Reuse one shared delivery adapter (also used for affiliation email and eventual reminders if specified), with no assumption about the selected provider or automatic push capability.

## Tests

Tests below are **specifications**, not code. They correspond to each main path and **every stated extension**; cross-cutting tests cover role enforcement and unreliable external dependencies.

| Use case, flow | Level | Asserts |
|---|---|---|
| `UC-ADM-view-dashboard` main | Integration | Admin receives all configured locations, correct persisted donation/shopping counts, volunteer required/assigned counts, and an unfilled-shift indicator |
| `UC-ADM-view-dashboard` 2a | Integration | Unauthenticated user gets `401`; signed-in non-admin gets `403`; neither receives dashboard data |
| `UC-ADM-view-dashboard` 3a | Integration | Location with no activity stays visible with zero/no-activity representation, not omitted or treated as unavailable |
| Dashboard source outage | Integration | If mandatory source is unavailable, request clearly fails rather than claiming stale/partial data is complete |
| `UC-ADM-manage-shifts` main | Integration | Admin creates a valid shift; committed shift has correct location/event, timing, required capacity, and appears in `VOL` sign-up; audit event recorded |
| `UC-ADM-manage-shifts` 2a | Integration | Non-admin and unauthenticated users cannot create, edit, delete, or preview protected assigned-volunteer information |
| `UC-ADM-manage-shifts` 4a | Unit + integration | Reject zero/negative capacity, unknown/inactive location, out-of-window day/time, malformed times; retain correct fields for correction; enforce location-specific requirements |
| `UC-ADM-manage-shifts` 4b | Integration | Storage error rolls back, produces no false confirmation or half-saved shift, supports safe retry |
| `UC-ADM-manage-shifts` 6a | Integration | Proposed edit/removal with assigned volunteers first shows impacted people; destructive action is blocked while policy unresolved; no silent assignment loss |
| Shift duplicate/concurrency | Integration | Concurrent identical create/retry cannot produce unintentional duplicate shift definitions; stale edit is rejected where versioning is supported |
| Location/event default staffing | Integration | Administrator sets the location’s default required count; new shifts at that location inherit it; overrides change only selected shifts; existing shift caps do not change when default changes |
| ADM/VOL shared shift contract | Integration | VOL sees `findSignupCandidates`, `findById`, `lockForAssignment`; UTC times are returned; shift row lock serializes sign-up and ADM capacity edit |
| Capacity reduction to held count | Integration | One `ACTIVE` plus one `PENDING`, required 2; lowering to 1 yields `409 CAPACITY_BELOW_HELD`, no shift mutation |
| Capacity concurrent signup | Integration | Concurrent admin cap edit and volunteer sign-up cannot make held `ACTIVE + PENDING` exceed required count |
| `UC-ADM-review-volunteer` main approval | Integration | Admin approving a NONE account sets APPROVED role and all that person's PENDING assignments ACTIVE in one committed transaction; no held count changes |
| `UC-ADM-review-volunteer` deny (3a1) | Integration | Denying a single PENDING claim makes only that assignment DENIED, frees its slot, and does not approve/deny the account or other shifts |
| `UC-ADM-review-volunteer` save failure (4a) | Integration | Simulated failure rolls back both account approval and VOL assignments, with no outcome notification |
| `UC-ADM-review-volunteer` repeat (4b) | Integration | Repeat approval/denial leaves records unchanged and does not send another notice |
| `UC-ADM-review-volunteer` notice (5, 3a2) | Integration | After commit the affected person is notified; provider failure leaves approval/denial committed and is visible to admin |
| `UC-ADM-review-volunteer` unauthorized | Integration | Unauthenticated/non-admin user cannot list pending reviews, approve, or deny, even via direct API request |
| Dashboard status accuracy | Integration | One ACTIVE, one PENDING, one CANCELLED and one DENIED: held 2, approved 1, pending 1, cancelled/denied do not hold slots |
| `UC-ADM-notify-volunteers` main | Integration with fake provider | Authorized admin deliberately chooses `all_volunteers` or `no_shift_signups`; correct audience resolved and one explicit dispatch attempted; UI reports acceptance accurately |
| `UC-ADM-notify-volunteers` 4a | Integration with failing provider | Provider unavailable or rejects send => non-success status, no claim messages were sent; admin can retry appropriately |
| Notification authorization | Integration | Non-admin cannot trigger a send, including direct HTTP requests |
| No automatic broadcast | Integration | Volunteer cancels a shift without an administrator send action => no broadcast to other volunteers |
| Notification partial/ambiguous outcome | Integration with fake provider | Partial acceptance or timeout does not incorrectly report all recipients as sent; retry does not intentionally duplicate previously confirmed recipients |
| Time-zone boundary | Unit | A shift scheduled/displayed across daylight-saving transitions obeys configured `America/Chicago` semantics and validates the actual event operating window |
| Audit/privacy | Integration | Successful admin shift changes generate audit entries; logs and normal dashboard results exclude secrets and unnecessary volunteer PII |

## Changes to the architecture-of-record

**Administration includes `UC-ADM-review-volunteer` in its responsibility in architecture §5.2.** The existing architecture already identifies Administration, Volunteer Scheduling, shared Authentication/Notifications and the supporting Donation/Shopping areas. **A future clarification is needed** on canonical event/location operating-window ownership and notification-provider/dispatch semantics; if this changes component responsibilities or dependencies, update `architectural-design.md` and record an appropriate `KD-*` decision **in the same PR** before implementing that change.

## Open questions & risks

| Question / risk | Owner or source | Impact / interim handling |
|---|---|---|
| **Actual `main` commit and real source paths not verified** | Area owner / repo | Fill in `Designed against` SHA and adjust proposed paths against repository conventions before the PR is submitted |
| **Requirements baseline** | Team owner | Verify the latest approved use cases and architecture before merging this design |
| **Role/account + VOL transactional write boundary** | ADM, VOL and Authentication owners | Approval requires account role change and assignment confirmations in one DB transaction; confirm shared DB/transaction manager |
| **Approval-versus-denial race** | Team and client | A DENY arriving after account approval would target an ACTIVE signup; default is to refuse `409`, but acceptance/alternative must be documented in use case |
| **Assigned-shift edit/removal policy (extension 6a)** | Wendy Macias / ReFrog organizers | Need decision on cancellations, impact confirmation and whether/how assigned volunteers are told; until decided, preview only and block destructive changes |
| **Email/push/provider selection and send result semantics** | Team architecture/operations owner and client | No assumed platform, delivery guarantee, repeat cadence, or partial-retry design; provider adapter must expose reliable acceptance/failure status |
| **Which volunteer contacts are eligible for broad notification** | Client and `VOL`/Identity owners | Resolve recipient eligibility/availability, communication preference and whether “everyone” includes volunteers with no contact method; do not invent opt-in rules |
| **Where event/location schedule settings live** | `ADM` and `LOC` owners | Choose one owner; ensure location operating days/time are consistent for both public view and shift validation |
| **Concurrent staffing edits/assignment and duplicate submits** | `ADM`/`VOL` owners | Agree on shift/capacity invariant, shared IDs, transaction/locking and duplicate request strategy before independent implementations |
| **Retention and hosting ownership (`OI-6`, `OI-12`, related hosting questions)** | Client, TCU governance and team | Do not invent retention periods or hosting; implement under approved operational policy |
| **No unsupported analytics or auto-alert scope** | Area owner/reviewer | Keep MVP to the four documented ADM use cases; no automatic per-cancellation volunteer blast, marketing campaign features, real-time inventory, or abuse flagging inside ADM |

## Questions test (required before opening the PR)

In a **fresh agent session in plan mode**, provide only: the four ADM use cases, their cited business rules, architecture section 8, and **this** design. Ask:

> List every point you would still have to guess to implement `UC-ADM-view-dashboard`, `UC-ADM-manage-shifts`, and `UC-ADM-notify-volunteers`, and `UC-ADM-review-volunteer`. For each, say what you would guess and whether a wrong guess could violate a requirement. Do not write code.

For each finding, update this design **or** state why the guess cannot violate a requirement. Paste the resulting question/answer list into the PR description under **How it was verified**, inside a collapsed `<details><summary>Questions test</summary>…</details>` section. A teammate other than this document's author must review and approve the PR before anyone starts implementation from it.
