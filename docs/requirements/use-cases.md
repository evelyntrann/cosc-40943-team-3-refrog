# Use Cases

**Project:** _[Refrog]_
**Team:** _[Team 3]_
**Client:** _[Wendy Macias, Texas Christian University]_
**Version:** 0.1

---

_**How to use this template.** Instructions appear in italic square brackets. Fill in underneath them and leave them in place until the document is stable._

_**What a use case is.** One goal a user can accomplish with your system, written as the dialogue between the actor and the system, including what happens when it goes wrong. It is the unit of work in this course: one use case becomes one issue, one branch, one pull request, and one set of tests._

_**Why the use case and not the user story.** You will meet user stories in industry, and they are a good planning tool: "As a student, I want to submit my report so that I get credit." A story is deliberately under-specified, because it is a **placeholder for a conversation** that happens later, between people. That is exactly the wrong property when the thing building your code is an agent that will implement precisely what the specification says and never ask what you meant. Use stories to plan and prioritize. Build against use cases._

_The difference that matters is the parts a story does not have: preconditions, the step-by-step flow, and above all the **extensions**, which is where the failure paths live. Most defects your team ships this semester will be in a path nobody wrote down._

## Identifiers

_Use cases are identified as `UC-<AREA>-<slug>`, where the area code groups related functionality and the slug is coined from the goal: `UC-RUB-create-rubric`, `UC-WAR-manage-activities`, `UC-STU-invite-students`._

_Pick your own area codes from your project's feature areas, three or four letters each, and list them at the top of the Use Case List. Areas correspond to the `FEAT-*` entries in your [vision and scope](vision-and-scope.md), which is where use cases come from._

_**Never renumber, rename, or repoint an identifier.** Moving a use case between areas would change its identifier, so put it in the right area the first time, and if you get it wrong, leave it. An identifier is an address, not a description._

_Within one use case, `PRE-1`, `POST-1`, and the step numbers are local and may be renumbered freely, because nothing outside the use case cites them._

**Area codes for this project:**

| Area | Feature area | From |
|---|---|---|
| `VOL` | Volunteer scheduling | `FEAT-volunteer-scheduling`, `FEAT-volunteer-notification` |
| `IDV` | Identity verification | `FEAT-identity-verification` |
| `LOC` | Location info | `FEAT-location-info` |
| `ADM` | Administration | `FEAT-adminstration` |
| `ABU` | Shopping-abuse monitoring | `FEAT-shopping-monitoring` |
| `SHP` | Shopping | `FEAT-shopping` |
| `DON` | Donation | `FEAT-donation` |

`FEAT-donation-partners` has no use cases yet. Whether donation partners are meant to be app users at all is unresolved (`OI-3`); this document covers only the seven areas above until that is answered.

## Revision History

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-09-23 | 0.1 | Initial use cases derived from the vision and scope feature list and the first client meeting | Evelyn Tran |

---

## 1. Introduction

### 1.1 Purpose

This document specifies the goals a donor, shopper, volunteer, or administrator can accomplish with the ReFrog application, in enough detail that a developer knows what to build and a tester knows what to check. It covers volunteer shift scheduling, TCU-affiliation verification, viewing location and event information, logging donations and shopping activity, the administrator dashboard, and reviewing potential shopping abuse.

### 1.2 Scope

This document covers the feature areas listed above: `FEAT-volunteer-scheduling`, `FEAT-volunteer-notification`, `FEAT-identity-verification`, `FEAT-location-info`, `FEAT-adminstration`, `FEAT-shopping-monitoring`, `FEAT-shopping`, and `FEAT-donation`.

`FEAT-donation-partners` is explicitly **not** covered here — no use cases exist for it, pending `OI-3` (what capabilities, if any, donation partners should have within the app).

`FEAT-volunteer-notification` is deliberately **not** represented as its own use case either, and that is worth stating rather than leaving silent. A shift reminder is system-initiated on a schedule, with no actor-driven goal behind it — it belongs in the specification's non-use-case functional requirements (an event-driven EARS requirement: "When a shift begins in `<N>` hours, the system shall notify the assigned volunteer"), not here. What *is* missing and should not be: an administrator-side use case for creating the shifts that `UC-VOL-signup` assumes already exist. That gap is fixed below with `UC-ADM-manage-shifts`.

---

## 2. Use Case Template

_[The field definitions. Every use case below uses exactly these fields, in this order.]_

**UC ID and Name.** _The identifier plus a concise name stating the value this use case provides to a user. Begin with an action verb, followed by an object: "Create a rubric", not "Rubric creation" and not "Rubric management", which is a feature, not a goal._

**Created By** and **Date Created.** _Who wrote it, and when._

**Primary and Secondary Actors.** _An actor is a person or other entity outside the system that interacts with it. The primary actor initiates this use case; secondary actors participate in completing it. Actors usually correspond to the user classes you identified in the vision and scope._

**Trigger.** _The business event, system event, or user action that starts the use case. The trigger tells the system to begin testing the preconditions._

**Description.** _A brief statement of the reason for and the outcome of this use case._

**Preconditions.** _What must already be true before this use case can start. **The system must be able to test each precondition**, which is what separates a precondition from a hope. Label them `PRE-1`, `PRE-2`. Example: PRE-1. The user's identity has been authenticated._

**Postconditions.** _The state of the system at successful conclusion. Label them `POST-1`, `POST-2`. Example: POST-1. The price of the item in the database has been updated with the new value._

**Main Success Scenario.** _The actor's actions and the system's responses under normal, expected conditions, as a numbered list that alternates between the two and ends by accomplishing the goal in the name. Write "The system validates..." not "The system will validate..."; use cases are written in the present tense._

**Extensions.** _Where the real work is. Two kinds, both numbered relative to the step they branch from:_

- _**Alternative flows**, other ways the use case can still succeed. Number them `4a`, `4b` for branches from step 4, with their own sub-steps `4a1`, `4a2`. Say where the flow branches off and, if it does, where it rejoins._
- _**Exceptions**, anticipated error conditions and how the system responds. Numbered the same way._

_**A use case with no extensions is not finished.** For every step, ask: what if the input is invalid, the thing is not found, the user cancels, the user is not allowed, or the external system is down? An agent building from a flow with no failure paths will invent the error handling, and you will not find out until a demo._

**Priority.** _Relative priority of implementing this. Use the same scheme across all your use cases._

**Frequency of Use.** _Roughly how often this is performed, per an appropriate unit of time. An early indicator of load, concurrency, and transaction volume, and it is the field that tells your architecture which use cases matter._

**Business Rules.** _The `BR-*` identifiers that govern this use case. **Identifiers only, never the rule's text**, so the rule has one home in [business-rules.md](business-rules.md) and cannot go stale here._

**Associated Information.** _Everything a developer needs that is not a step: the data fields and their validation rules, quality attributes that apply, display and sort strategies, and what happens if execution fails for a systemic reason such as a network timeout. If the use case makes a durable change, say whether a failure rolls it back, completes it, or leaves it partially done._

_Data fields are specified as a table:_

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| _[field]_ | _[type]_ | _[required, format, range]_ | _[who may see or set it]_ | _[term]_ |

**Related Use Cases.** _Other use cases this one invokes or is invoked by, by identifier and name._

**Assumptions.** _Anything assumed about this use case or how it executes._

**Open Issues.** _What you do not know yet. Mirror it into [OPEN-ISSUES.md](OPEN-ISSUES.md) so it is visible in one place._

---

## 3. Use Case List

| Area code | Feature area | Use cases |
|---|---|---|
| `VOL` | Volunteer scheduling, from `FEAT-volunteer-scheduling` | `UC-VOL-signup`, `UC-VOL-cancel-shift`, `UC-VOL-view-schedule` |
| `IDV` | Identity verification, from `FEAT-identity-verification` | `UC-IDV-verify-affiliation` |
| `LOC` | Location info, from `FEAT-location-info` | `UC-LOC-view-locations` |
| `ADM` | Administration, from `FEAT-adminstration` | `UC-ADM-view-dashboard`, `UC-ADM-manage-shifts` |
| `ABU` | Shopping-abuse monitoring, from `FEAT-shopping-monitoring` | `UC-ABU-review-alert` |
| `SHP` | Shopping, from `FEAT-shopping` | `UC-SHP-log-item-taken` |
| `DON` | Donation, from `FEAT-donation` | `UC-DON-log-donation` |

A note on `UC-VOL-claim-open-shift`: an earlier draft of this list had it as a separate use case from `UC-VOL-signup`. It was folded in below — claiming a shift that opened from a cancellation and signing up for a shift nobody has taken yet are the same actor/system dialogue; only the reason the shift is open differs, and the system does not need to treat them differently.

---

## 4. Use Cases

## Area: Volunteer Scheduling (`VOL`)

### UC-VOL-signup: Sign up for a volunteer shift

**UC ID and Name:** `UC-VOL-signup`: Sign up for a volunteer shift
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Volunteer
**Secondary Actors:** none
**Trigger:** The volunteer indicates they want to sign up for an open shift.
**Description:** A volunteer claims an open shift — whether it has never been filled or opened up because someone else cancelled — so the location has the staffing `BR-location-staffing` requires during the event.

**Preconditions:**

- PRE-1. The volunteer is signed in.

**Postconditions:**

- POST-1. The volunteer is added to the shift's assigned list.
- POST-2. If the shift has now reached its required volunteer count, it no longer appears as open to other volunteers.

**Main Success Scenario:**

1. The volunteer indicates they want to view open shifts.
2. The system displays shifts that still need a volunteer, showing location, date, and time. An empty list is a valid result, not a precondition failure.
3. The volunteer selects a shift.
4. The system confirms the shift still has an unfilled slot.
5. The system assigns the volunteer to the shift and updates its remaining open slots.
6. The system confirms the sign-up to the volunteer.
7. Use case ends.

**Extensions:**

- **2a. No shifts are currently open:**
    - 2a1. The system displays that no shifts are open.
    - 2a2. Use case ends.
- **4a. The shift filled between steps 2 and 4 (another volunteer signed up first):**
    - 4a1. The system informs the volunteer the shift is no longer available.
    - 4a2. The volunteer returns to step 2.
- **4b. The volunteer is already assigned to this shift:**
    - 4b1. The system informs the volunteer they are already signed up.
    - 4b2. Use case ends.
- **5a. The assignment cannot be saved (e.g., a network or server failure):**
    - 5a1. The system informs the volunteer the sign-up did not complete and that no assignment was made.
    - 5a2. The volunteer may retry from step 3.
- **5b. The volunteer retries after step 5a, or submits twice in quick succession:**
    - 5b1. The system recognizes the shift is already assigned to this volunteer from the prior attempt and does not create a second assignment or occupy a second slot.
    - 5b2. The system confirms the existing sign-up. Use case ends.

**Priority:** High — the client named volunteer sign-up as the first feature needed, since recruitment starts before shopping or donation activity does.
**Frequency of Use:** High during the roughly one-to-one-and-a-half-month recruitment window before each event (`AS-seasonal-readiness`); low otherwise.
**Business Rules:** `BR-volunteer-assignment`, `BR-location-staffing`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| shift location | Reference | Required; must reference an active donation location | Visible to any signed-in volunteer | Donation Location |
| shift date/time | Date/time | Required | Visible to any signed-in volunteer | — |
| volunteer identifier | Reference | Required; must reference the signed-in volunteer | Assignment list visible only to the volunteer and administrators | Volunteer |

A failed save leaves no assignment recorded (fails cleanly, no partial state). A retry of the same sign-up, whether user-initiated or a duplicate submission, must not create two assignments for the same volunteer on the same shift or double-count against the shift's capacity.

**Related Use Cases:** `UC-VOL-cancel-shift`: Cancel a signed-up shift; `UC-VOL-view-schedule`: View assigned shifts and hours; `UC-ADM-manage-shifts`: Create and configure volunteer shifts
**Assumptions:** none beyond the sign-in mechanism itself, which is unresolved (see Open Issues).
**Open Issues:** `OI-10` — the sign-in/identification mechanism for volunteers is not yet finalized.

---

### UC-VOL-cancel-shift: Cancel a signed-up shift

**UC ID and Name:** `UC-VOL-cancel-shift`: Cancel a signed-up shift
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Volunteer
**Secondary Actors:** Administrator (notified in one extension)
**Trigger:** The volunteer indicates they can no longer work a shift they are assigned to.
**Description:** A volunteer who cannot make a shift cancels it so the resulting gap becomes visible for another volunteer to claim, rather than requiring an administrator to notice and cover it by hand, which is how this is handled today.

**Preconditions:**

- PRE-1. The volunteer is signed in.
- PRE-2. The volunteer is currently assigned to the shift being cancelled.

**Postconditions:**

- POST-1. The volunteer is removed from the shift's assigned list.
- POST-2. The shift reappears as open, or its open-slot count increases.

**Main Success Scenario:**

1. The volunteer indicates they want to view their assigned shifts.
2. The system displays the volunteer's upcoming shifts.
3. The volunteer selects a shift and indicates they want to cancel it.
4. The system asks the volunteer to confirm.
5. The volunteer confirms.
6. The system removes the volunteer from the shift and marks it as needing a volunteer.
7. The system confirms the cancellation to the volunteer.
8. Use case ends.

**Extensions:**

- **5a. The volunteer does not confirm:**
    - 5a1. The system takes no action.
    - 5a2. Use case ends.
- **6a. The removal cannot be saved (e.g., a network or server failure):**
    - 6a1. The system informs the volunteer the cancellation did not complete and that they remain assigned to the shift.
    - 6a2. The volunteer may retry from step 3.
- **6b. The cancellation leaves the shift understaffed close enough to the event that it may not be claimed in time:**
    - 6b1. The system notifies administrators that the shift is open and unfilled.
    - 6b2. Use case continues at step 7.

**Priority:** High — directly targets the client's stated pain point of manually covering last-minute cancellations.
**Frequency of Use:** Occasional relative to sign-ups, but concentrated in the days immediately before each event.
**Business Rules:** `BR-volunteer-assignment`

**Associated Information:** Reuses the shift and volunteer fields defined in `UC-VOL-signup`; no new fields. A failed removal leaves the volunteer's assignment unchanged. Retrying a cancellation that already succeeded (e.g., a duplicate submission) must not error or have any further effect beyond confirming the volunteer is no longer assigned.

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift
**Assumptions:** none beyond `UC-VOL-signup`'s.
**Open Issues:** How soon before a shift should administrators be alerted about an unfilled cancellation (extension 6b)? Not defined by the client. See `OI-7`.

---

### UC-VOL-view-schedule: View assigned shifts and hours

**UC ID and Name:** `UC-VOL-view-schedule`: View assigned shifts and hours
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Volunteer
**Secondary Actors:** none
**Trigger:** The volunteer wants to see their upcoming shifts or accumulated hours.
**Description:** A volunteer views their assigned shifts and hours so they know where and when to show up and can track their contribution.

**Preconditions:**

- PRE-1. The volunteer is signed in.

**Postconditions:**

- POST-1. The volunteer's assigned shifts and hours are displayed. The list may be empty.

**Main Success Scenario:**

1. The volunteer indicates they want to view their schedule.
2. The system retrieves the shifts assigned to the volunteer.
3. The system displays the volunteer's upcoming shifts (location, date, time) and total hours.
4. Use case ends.

**Extensions:**

- **2a. The volunteer has no assigned shifts:**
    - 2a1. The system displays an empty schedule and points to open shifts (`UC-VOL-signup`).
    - 2a2. Use case ends.

**Priority:** Medium
**Frequency of Use:** Frequent during event week; occasional during recruitment.
**Business Rules:** none directly; displays data governed by `BR-volunteer-assignment`.

**Associated Information:** Reuses the shift fields defined in `UC-VOL-signup`. "Hours" are derived from shift assignments; see Open Issues.

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift; `UC-VOL-cancel-shift`: Cancel a signed-up shift
**Assumptions:** Hours are calculated from scheduled shift length rather than a separate check-in/check-out action — a team decision, deprioritized as not blocking the current design, not yet confirmed with the client.
**Open Issues:** none

---

## Area: Identity Verification (`IDV`)

### UC-IDV-verify-affiliation: Verify a shopper's TCU affiliation before shopping

**UC ID and Name:** `UC-IDV-verify-affiliation`: Verify a shopper's TCU affiliation before shopping
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Shopper
**Secondary Actors:** Volunteer (may observe or fall back to a manual check at the location)
**Trigger:** A shopper at a ReFrog location wants to begin shopping.
**Description:** A shopper demonstrates TCU affiliation so they can be admitted to shop, per `BR-shopper-tcu-affiliation`. Per the team's resolution of `OI-8`, signing in with a TCU credential (e.g., a tcu.edu email address) is itself the affiliation check — there is no separate "signed in but not verified" state. What happens at the physical location is a practical, visual confirmation (showing the signed-in app, or a TCU ID/email) to a volunteer, similar to today's process, not a second electronic check.

**Preconditions:**

- PRE-1. The shopper is signed in with a TCU credential.

**Postconditions:**

- POST-1. The shopper is permitted to shop, enabling `UC-SHP-log-item-taken`.

**Main Success Scenario:**

1. The shopper indicates they want to shop.
2. The system confirms the shopper is signed in with a TCU credential.
3. The system displays a confirmation the shopper can show a volunteer (e.g., on their phone).
4. The shopper shows the confirmation, or a physical TCU ID or TCU email, to the volunteer at the location for a quick visual check, consistent with `BR-shopper-verification`.
5. Use case ends.

**Extensions:**

- **2a. The shopper is not signed in, or is not signed in with a TCU credential:**
    - 2a1. The system informs the shopper they cannot be verified electronically this way.
    - 2a2. The volunteer falls back to the current visual ID check (`BR-shopper-verification`) rather than the app.
    - 2a3. Use case ends.

**Priority:** High — named by the client alongside volunteer sign-up as a top priority.
**Frequency of Use:** Every shopping visit; thousands per event.
**Business Rules:** `BR-shopper-tcu-affiliation`, `BR-shopper-verification`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| TCU credential | String/token | Required; exact provider and format still unresolved (`OI-10`) | Should reveal affiliation status only, not other personal data | TCU ID |

**Related Use Cases:** `UC-SHP-log-item-taken`: Record items taken while shopping
**Assumptions:** Signing in with a TCU credential is sufficient affiliation proof on its own, with no separate override/failure path beyond "not signed in" — a team decision, not yet confirmed with the client.
**Open Issues:** `OI-10` — the client suggested a TCU email address or a phone-displayed indicator rather than single sign-on, but the exact sign-in provider and credential format have not been chosen.

---

## Area: Location Info (`LOC`)

### UC-LOC-view-locations: View donation site locations, hours, and event info

**UC ID and Name:** `UC-LOC-view-locations`: View donation site locations, hours, and event info
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** App user (donor, shopper, or volunteer)
**Secondary Actors:** none
**Trigger:** A user wants to know where ReFrog locations are, when they're open, or other event details.
**Description:** A user views ReFrog's active locations and hours so they know where and when to go. This is written as one use case for all three actor types rather than three near-identical ones, since the system's behavior does not depend on which of the three is asking.

**Preconditions:** none — this information is public today (the ReFrog website), and nothing in the client meeting indicated it should require sign-in.

**Postconditions:**

- POST-1. The current locations, hours, and event info are displayed.

**Main Success Scenario:**

1. The user indicates they want to view ReFrog locations.
2. The system retrieves the active locations and hours for the current event.
3. The system displays each location's address or map, hours, and any relevant notes.
4. Use case ends.

**Extensions:**

- **2a. No event is currently active or scheduled:**
    - 2a1. The system informs the user that no ReFrog event is currently scheduled.
    - 2a2. Use case ends.

**Priority:** High — named directly among the client's top priorities.
**Frequency of Use:** Very frequent; likely checked by nearly every participant.
**Business Rules:** `BR-event-schedule`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| location name/address | String | Required | Public | Donation Location |
| hours | Time range | Required | Public | — |

**Related Use Cases:** `UC-DON-log-donation`, `UC-SHP-log-item-taken`, `UC-VOL-signup` — each references a location shown here.
**Assumptions:** Viewing location info does not require signing in.
**Open Issues:** none

---

## Area: Administration (`ADM`)

### UC-ADM-view-dashboard: View the event dashboard

**UC ID and Name:** `UC-ADM-view-dashboard`: View the event dashboard
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Administrator
**Secondary Actors:** none
**Trigger:** An administrator wants to check ReFrog's current status across locations.
**Description:** An administrator views donation, shopping, and volunteer-coverage activity in one place, so they can spot problems such as understaffed locations without manually reconciling separate Google Sheets.

**Preconditions:**

- PRE-1. The administrator is signed in with administrator privileges.

**Postconditions:**

- POST-1. Current donation, shopping, and volunteer-coverage data is displayed.

**Main Success Scenario:**

1. The administrator indicates they want to view the dashboard.
2. The system verifies the administrator's privileges.
3. The system retrieves current donation counts, shopping counts, and volunteer coverage by location.
4. The system displays the data, highlighting locations with unfilled shifts.
5. Use case ends.

**Extensions:**

- **2a. The user does not have administrator privileges:**
    - 2a1. The system denies access and displays an error.
    - 2a2. Use case ends.
- **3a. One or more locations have no recorded activity yet:**
    - 3a1. The system displays those locations as having no activity rather than omitting them.
    - 3a2. Use case continues at step 4.

**Priority:** High — named among the client's top priorities, though secondary to volunteer sign-up itself per `AS-volunteer-first`.
**Frequency of Use:** Frequent during event week; the client described wanting to review data overnight.
**Business Rules:** none identified yet governing who may access the dashboard; see Open Issues.

**Associated Information:** Aggregates data captured by `UC-DON-log-donation`, `UC-SHP-log-item-taken`, and `UC-VOL-view-schedule`. No new raw data fields.

**Related Use Cases:** `UC-DON-log-donation`, `UC-SHP-log-item-taken`, `UC-VOL-view-schedule`, `UC-ABU-review-alert`
**Assumptions:** "Administrator" refers only to the three named ReFrog organizers (Wendy, Eric, Courtney) — a team decision, not yet confirmed with the client.
**Open Issues:** none

---

### UC-ADM-manage-shifts: Create and configure volunteer shifts

**UC ID and Name:** `UC-ADM-manage-shifts`: Create and configure volunteer shifts
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Administrator
**Secondary Actors:** none
**Trigger:** An administrator wants to set up or adjust the shifts volunteers can sign up for an upcoming event.
**Description:** An administrator defines each shift's location, date, time, and required number of volunteers, before volunteer sign-up opens. Every other `UC-VOL-*` use case assumes shifts already exist; this is the use case that creates them. `AS-event-configuration` establishes that organizers confirm dates, hours, locations, shift lengths, and staffing needs before registration opens, but does not say whether that happens inside this app or through some other process — this use case assumes it moves into the app, which should be confirmed with the client.

**Preconditions:**

- PRE-1. The administrator is signed in with administrator privileges.

**Postconditions:**

- POST-1. The shift (location, date, time, required volunteer count) exists and is visible to volunteers via `UC-VOL-signup`.

**Main Success Scenario:**

1. The administrator indicates they want to create a shift.
2. The system asks for the shift's location, date, time, and required number of volunteers.
3. The administrator enters the details and confirms.
4. The system validates the details and creates the shift.
5. The system confirms the shift is now open for sign-up.
6. Use case ends.

**Extensions:**

- **2a. The user does not have administrator privileges:**
    - 2a1. The system denies access and displays an error.
    - 2a2. Use case ends.
- **4a. The entered details are invalid (e.g., a required-volunteer count of zero, a location that does not exist, or a date outside the event window):**
    - 4a1. The system rejects the entry and asks the administrator to correct it.
    - 4a2. Use case resumes at step 3.
- **4b. The shift cannot be saved (e.g., a network or server failure):**
    - 4b1. The system informs the administrator the shift was not created.
    - 4b2. The administrator may retry from step 3.
- **6a. The administrator wants to edit or remove a shift that already has volunteers assigned:**
    - 6a1. The system displays the affected volunteers before the change is confirmed.
    - 6a2. **Proposed, not confirmed by the client:** how a shift change should notify already-assigned volunteers has not been discussed and needs client input.

**Priority:** High — nothing in `UC-VOL-signup` can function without this existing first.
**Frequency of Use:** Concentrated in the planning phase, roughly one to one-and-a-half months before each event, per `AS-seasonal-readiness`; occasional adjustments during event week.
**Business Rules:** `BR-location-staffing`, `BR-event-schedule`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| location | Reference | Required; must reference an active ReFrog location | Visible to any signed-in volunteer once created | Donation Location |
| date/time | Date/time | Required; within the event's operating window | Visible to any signed-in volunteer once created | — |
| required volunteer count | Integer | Required; per `BR-location-staffing`, ReFrog's stated practice is a minimum of two | Editable only by administrators | — |

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift; `UC-ADM-view-dashboard`
**Assumptions:** Shift creation happens inside this app rather than a separate tool; not confirmed with the client.
**Open Issues:** `OI-9` — does shift creation belong in this app, or does the client intend to keep using an external tool for it? Editing/removing a shift with assigned volunteers (extension 6a) is also unspecified.

---

## Area: Shopping-Abuse Monitoring (`ABU`)

### UC-ABU-review-alert: Review and act on a potential shopping-abuse alert

**UC ID and Name:** `UC-ABU-review-alert`: Review and act on a potential shopping-abuse alert
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Administrator
**Secondary Actors:** none
**Trigger:** The system flags shopping activity as potentially excessive, or the administrator notices a concern while reviewing the dashboard.
**Description:** An administrator reviews flagged shopping activity and records a decision, consistent with the client's wish to review before any action is taken rather than have the system enforce automatically. **This use case is a skeleton.** What counts as "excessive" or "prohibited" shopping has not been defined by the client (`OI-11`), so the trigger condition and data fields below cannot be finalized yet.

**Preconditions:**

- PRE-1. The administrator is signed in with administrator privileges.

**Postconditions:**

- POST-1. The administrator has reviewed the flagged activity and recorded a decision.

**Main Success Scenario:**

1. The administrator indicates they want to review flagged shopping activity.
2. The system displays flagged activity with the reason each was flagged. An empty list is a valid result, not a precondition failure.
3. The administrator selects an item to review.
4. The system displays the details behind the flag.
5. The administrator records a decision.
6. The system saves the decision.
7. Use case ends.

**Extensions:**

- **2a. No activity is currently flagged:**
    - 2a1. The system displays that there are no current flags.
    - 2a2. Use case ends.
- **5a. The administrator determines the flag was a false positive:**
    - 5a1. The administrator marks the flag resolved with no action, and the record is corrected.
    - 5a2. Use case continues at step 6.
    - **Note:** the vision and scope's `RI-incorrect-shopping-penalties` raises one specific example — a volunteer's own legitimate activity being misclassified as abuse. That is the team's risk analysis, not something the client raised directly in the meeting; it is recorded here as the reason administrator review happens before any penalty, not as a confirmed client concern.

**Priority:** Low-Medium — the client was explicit that this follows volunteer features, not alongside them, and the governing rule does not exist yet.
**Frequency of Use:** Unknown; depends on a definition that does not yet exist.
**Business Rules:** `BR-shopping-abuse-review`

**Associated Information:** Cannot be specified beyond what's above until `OI-11` is resolved.

**Related Use Cases:** `UC-ADM-view-dashboard`, `UC-SHP-log-item-taken`
**Assumptions:** none — this entire use case is provisional.
**Open Issues:** `OI-11` — this use case's flagging condition and data fields depend entirely on its answer.

---

## Area: Shopping (`SHP`)

### UC-SHP-log-item-taken: Record items taken while shopping

**UC ID and Name:** `UC-SHP-log-item-taken`: Record items taken while shopping
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Shopper
**Secondary Actors:** Volunteer (may assist)
**Trigger:** A shopper has selected items and is ready to complete their visit.
**Description:** A shopper records what they took during a visit, replacing the current separate Google Form, so ReFrog can track shopping activity by location.

**Preconditions:**

- PRE-1. The shopper's TCU affiliation has been verified for this visit (`UC-IDV-verify-affiliation`).

**Postconditions:**

- POST-1. The shopping visit (location, date, item count) is recorded.

**Main Success Scenario:**

1. The shopper indicates they are finished selecting items.
2. The system asks the shopper to enter the number of items taken.
3. The shopper enters the count and confirms.
4. The system records the shopping visit.
5. The system confirms the entry to the shopper.
6. Use case ends.

**Extensions:**

- **3a. The shopper enters an invalid count (negative, non-numeric, or zero):**
    - 3a1. The system rejects the entry and asks the shopper to correct it.
    - 3a2. Use case resumes at step 2.
- **4a. The entry cannot be saved (for example, a connectivity issue at an outdoor location):**
    - 4a1. The system informs the shopper the entry could not be saved and offers to retry.
    - 4a2. Use case resumes at step 3, or the shopper reports the count to a volunteer as a fallback.
- **4b. The shopper retries after step 4a, or submits twice in quick succession, and the original entry actually did save:**
    - 4b1. The system recognizes the visit was already recorded and does not create a second entry.
    - 4b2. The system confirms the existing entry. Use case ends.

**Priority:** Medium — part of the "broader donation and shopping capabilities" the client said require separate prioritization from the volunteer-first release (`AS-volunteer-first`).
**Frequency of Use:** Every shopping visit; thousands per event.
**Business Rules:** `BR-shopping-activity-recorded`, `BR-free-shopping`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| location | Reference | Required; must reference an active ReFrog location | Visible to administrators | Donation Location |
| item count | Integer | Required; positive whole number | Visible to administrators | Item |

A failed save leaves no visit recorded. A retry of the same submission, whether user-initiated or a duplicate, must not record the same visit twice.

**Related Use Cases:** `UC-IDV-verify-affiliation`, `UC-ADM-view-dashboard`, `UC-ABU-review-alert`
**Assumptions:** Items are counted in aggregate, not itemized individually, consistent with current practice.
**Open Issues:** none beyond `OI-11`, already tracked under `UC-ABU-review-alert`.

---

## Area: Donation (`DON`)

### UC-DON-log-donation: Record items donated at a location

**UC ID and Name:** `UC-DON-log-donation`: Record items donated at a location
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Donor
**Secondary Actors:** none
**Trigger:** A donor has dropped off items at a ReFrog location and wants to record the donation.
**Description:** A donor records what they donated, replacing the current QR-code Google Form, so ReFrog can track donation activity by location.

**Preconditions:** none confirmed. Whether logging a donation requires the donor to be signed in is unresolved (see Open Issues); this use case does not assume either answer.

**Postconditions:**

- POST-1. The donation (location, date, item count) is recorded.

**Main Success Scenario:**

1. The donor indicates they want to log a donation.
2. The system asks the donor to select the donation location and enter the number of items donated.
3. The donor enters the information and confirms.
4. The system records the donation.
5. The system confirms the entry to the donor.
6. Use case ends.

**Extensions:**

- **3a. The donor enters an invalid item count:**
    - 3a1. The system rejects the entry and asks the donor to correct it.
    - 3a2. Use case resumes at step 2.
- **2a. Optional item categorization is enabled:**
    - 2a1. The system offers a fast, low-friction category selection, consistent with the client's condition that categorization must not make donating harder.
    - 2a2. Use case continues at step 3.
- **4a. The entry cannot be saved (for example, a connectivity issue at an outdoor location):**
    - 4a1. The system informs the donor the entry could not be saved and offers to retry.
    - 4a2. Use case resumes at step 3.
- **4b. The donor retries after step 4a, or submits twice in quick succession, and the original entry actually did save:**
    - 4b1. The system recognizes the donation was already recorded and does not create a second entry.
    - 4b2. The system confirms the existing entry. Use case ends.

**Priority:** Medium — same deferred tier as `UC-SHP-log-item-taken`, per `AS-volunteer-first`.
**Frequency of Use:** Every donation drop-off; high volume during event week.
**Business Rules:** `BR-donation-location-recorded`, `BR-donation-count-recorded`, `BR-donation-ease`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| location | Reference | Required; must reference an active ReFrog location | Visible to administrators | Donation Location |
| item count | Integer | Required; positive whole number | Visible to administrators | Item |

A failed save leaves no donation recorded. A retry of the same submission, whether donor-initiated or a duplicate, must not record the same donation twice.

**Related Use Cases:** `UC-LOC-view-locations`, `UC-ADM-view-dashboard`
**Assumptions:** none beyond the identity question below.
**Open Issues:** `OI-4` — does logging a donation require the donor to be signed in, or can it stay anonymous like today's QR-code form?

---

## Working these with your agent

_[Delegate: drafting the main success scenario once you have the trigger and the goal; proposing extensions you have not thought of, which it is genuinely good at; turning a filled-in use case into a first set of test cases; checking that every `BR-*` you cite exists in [business-rules.md](business-rules.md).]_

_Keep human: whether this is one use case or three, what the priority is, and whether an extension the agent proposed is a real path in your client's business or a generic one it has seen elsewhere. "The system handles concurrent edits" is a real requirement for some projects and invented complexity for others, and only you have met the client._

_The verification that catches the most: read the main success scenario aloud to someone who has not read the document, and stop wherever they ask a question. Every question is a missing step or a missing extension._

_**Checklist for each use case:** Does the name start with a verb? Can the system test every precondition? Does every step alternate actor and system? Is there at least one extension per step that can fail? Does every business rule appear as an identifier only? Could a tester write test cases from this without asking you anything?_
