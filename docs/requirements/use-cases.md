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
| `INF` | Event & location information | `FEAT-event-info` (formerly `FEAT-location-info`) |
| `ADM` | Administration | `FEAT-adminstration` |
| `ABU` | Shopping-abuse monitoring | `FEAT-shopping-monitoring` |
| `SHP` | Shopping | `FEAT-shopping` |
| `DON` | Donation | `FEAT-donation` |

`FEAT-donation-partners` has no use cases yet. Confirmed 2026-10-02 (`OI-3`, resolved): donation partners are not app users — pickup coordination stays with administrators, outside the app, same as today.

## Revision History

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-09-23 | 0.1 | Initial use cases derived from the vision and scope feature list and the first client meeting | Evelyn Tran |
| 2026-10-16 | 0.2 | `UC-SHP-log-item-taken`: category is now required, one per item taken (was optional extension 2a); added extension 3b | Alfonsotomax |

---

## 1. Introduction

### 1.1 Purpose

This document specifies the goals a donor, shopper, volunteer, or administrator can accomplish with the ReFrog application, in enough detail that a developer knows what to build and a tester knows what to check. It covers volunteer shift scheduling, TCU-affiliation verification, viewing location and event information, logging donations and shopping activity, the administrator dashboard, and reviewing potential shopping abuse.

### 1.2 Scope

This document covers the feature areas listed above: `FEAT-volunteer-scheduling`, `FEAT-volunteer-notification`, `FEAT-identity-verification`, `FEAT-event-info` (formerly `FEAT-location-info`), `FEAT-adminstration`, `FEAT-shopping-monitoring`, `FEAT-shopping`, and `FEAT-donation`.

`FEAT-donation-partners` is explicitly **not** covered here — confirmed 2026-10-02 (`OI-3`, resolved) that donation partners get no app access at all; pickups stay coordinated through administrators.

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
| `VOL` | Volunteer scheduling, from `FEAT-volunteer-scheduling` | `UC-VOL-signup`, `UC-VOL-cancel-shift`, `UC-VOL-view-schedule`, `UC-VOL-request-access` |
| `IDV` | Identity verification, from `FEAT-identity-verification` | `UC-IDV-verify-affiliation` |
| `LOC` | Location info, from `FEAT-location-info` | `UC-LOC-view-locations` |
| `ADM` | Administration, from `FEAT-adminstration` | `UC-ADM-view-dashboard`, `UC-ADM-manage-shifts`, `UC-ADM-notify-volunteers`, `UC-ADM-review-volunteer` |
| `ABU` | Shopping-abuse monitoring, from `FEAT-shopping-monitoring` | `UC-ABU-review-alert` |
| `SHP` | Shopping, from `FEAT-shopping` | `UC-SHP-log-item-taken` |
| `DON` | Donation, from `FEAT-donation` | `UC-DON-log-donation` |

A note on `UC-VOL-claim-open-shift`: an earlier draft of this list had it as a separate use case from `UC-VOL-signup`. It was folded in below — claiming a shift that opened from a cancellation and signing up for a shift nobody has taken yet are the same actor/system dialogue; only the reason the shift is open differs, and the system does not need to treat them differently.

---

**Volunteer role decision (2026-10-16):** Team decision with Alexa Mercado: administrators approve volunteer requests or grant access manually by verified sign-in email. Approval creates the volunteer role; pending requests do not. A signed-in role holder may shop without TCU email verification. This is separate from shift assignment; whether role approval is required before shift signup remains `OI-18`.

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

- POST-1. The volunteer is added to the shift's assigned list, as pending approval if an administrator has not yet approved them (5c).
- POST-2. If the shift's approved and pending sign-ups now reach its required volunteer count, it no longer appears as open to other volunteers.

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
- **4c. An administrator denied this volunteer's earlier sign-up for this same shift (`UC-ADM-review-volunteer` 3a):**
    - 4c1. The system informs the volunteer they cannot sign up for this shift. Their other shifts are not affected.
    - 4c2. The volunteer returns to step 2.
- **5a. The assignment cannot be saved (e.g., a network or server failure):**
    - 5a1. The system informs the volunteer the sign-up did not complete and that no assignment was made.
    - 5a2. The volunteer may retry from step 3.
- **5b. The volunteer retries after step 5a, or submits twice in quick succession:**
    - 5b1. The system recognizes the shift is already assigned to this volunteer from the prior attempt and does not create a second assignment or occupy a second slot.
    - 5b2. The system confirms the existing sign-up. Use case ends.
- **5c. An administrator has not yet approved this person as a volunteer (their first sign-up, or any sign-up before approval):**
    - 5c1. The system records the assignment as pending approval. A pending assignment holds the slot exactly like an approved one.
    - 5c2. The system confirms the sign-up and tells the volunteer it is awaiting administrator approval (`UC-ADM-review-volunteer`). Use case ends.

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
**Assumptions:** Sign-in is via Google Sign-In — the team's working decision (resolves `OI-17`), not yet confirmed with the client. Distinct from `OI-10` (resolved), which settled TCU-affiliation verification specifically, not general sign-in. Everyone signs in, volunteers included; a signed-in account becomes a volunteer only when an administrator approves it (team discussion, 2026-10-16). The volunteer claims first and is approved after, so a first-time volunteer can still claim a shift without waiting (`USE-volunteer-signup-completion`).
**Open Issues:** none.

---

### UC-VOL-cancel-shift: Cancel a signed-up shift

**UC ID and Name:** `UC-VOL-cancel-shift`: Cancel a signed-up shift
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Volunteer
**Secondary Actors:** none
**Trigger:** The volunteer indicates they can no longer work a shift they are assigned to.
**Description:** A volunteer who cannot make a shift cancels it so the resulting gap becomes visible for another volunteer to claim, rather than requiring an administrator to notice and cover it by hand, which is how this is handled today. An administrator may separately choose to notify volunteers about the opening (`UC-ADM-notify-volunteers`), but that is a distinct, deliberate action, not a consequence of cancelling.

**Preconditions:**

- PRE-1. The volunteer is signed in.
- PRE-2. The volunteer is currently assigned to the shift being cancelled, approved or pending approval.

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

**Priority:** High — directly targets the client's stated pain point of manually covering last-minute cancellations.
**Frequency of Use:** Occasional relative to sign-ups, but concentrated in the days immediately before each event.
**Business Rules:** `BR-volunteer-assignment`

**Associated Information:** Reuses the shift and volunteer fields defined in `UC-VOL-signup`; no new fields. A failed removal leaves the volunteer's assignment unchanged. Retrying a cancellation that already succeeded (e.g., a duplicate submission) must not error or have any further effect beyond confirming the volunteer is no longer assigned.

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift; `UC-ADM-notify-volunteers`: Notify volunteers of open shifts
**Assumptions:** none beyond `UC-VOL-signup`'s.
**Open Issues:** none — `OI-7` is resolved. This use case no longer automatically notifies anyone on cancellation; it just reopens the shift. Confirmed 2026-10-02: notifying volunteers about open shifts is a separate, administrator-triggered action (`UC-ADM-notify-volunteers`), not an automatic consequence of a cancellation. Wendy was explicit she doesn't want volunteers over-notified.

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
3. The system displays the volunteer's upcoming shifts (location, date, time), marking any still awaiting administrator approval, and their hours: finished hours from approved shifts that have ended, and planned hours from approved shifts still to come.
4. Use case ends.

**Extensions:**

- **2a. The volunteer has no assigned shifts:**
    - 2a1. The system displays an empty schedule and points to open shifts (`UC-VOL-signup`).
    - 2a2. Use case ends.

**Priority:** Medium
**Frequency of Use:** Frequent during event week; occasional during recruitment.
**Business Rules:** none directly; displays data governed by `BR-volunteer-assignment`.

**Associated Information:** Reuses the shift fields defined in `UC-VOL-signup`. "Hours" are derived from approved shift assignments, and a shift awaiting approval counts toward neither number; see Open Issues.

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift; `UC-VOL-cancel-shift`: Cancel a signed-up shift
**Assumptions:** Hours are calculated from scheduled shift length rather than a separate check-in/check-out action — a team decision, deprioritized as not blocking the current design, not yet confirmed with the client.
**Open Issues:** none

---

### UC-VOL-request-access: Request volunteer access

**UC ID and Name:** `UC-VOL-request-access`: Request volunteer access
**Created By:** Alexa Mercado
**Date Created:** 2026-10-16
**Primary Actor:** Signed-in user
**Secondary Actors:** Administrator (reviews separately)
**Trigger:** The user selects **Become a volunteer**.
**Description:** Request the administrator-granted volunteer role. Source: team decision with Alexa Mercado, not a recorded client approval.

**Preconditions:**
- PRE-1. The user is signed in.

**Postconditions:**
- POST-1. A pending request is recorded for the authenticated account; no volunteer role is granted by requesting.

**Main Success Scenario:**
1. The user selects Become a volunteer.
2. The system checks the account's role and existing request.
3. The system records a pending request for that account.
4. The system displays Volunteer access pending approval.
5. Use case ends.

**Extensions:**
- **2a. The account already has the volunteer role:** Display existing volunteer access; use case ends.
- **2b. A pending request already exists:** Return its status without a duplicate; continue at step 4.
- **2c. A prior request was declined for the current annual event:** Do not create another request for the same event or grant permissions. Explain that the user may request again for the next annual event, or Wendy may manually grant access through `UC-ADM-grant-volunteer-access`.
- **2d. A prior request was declined for a previous annual event:** Allow a new request for the next annual event; continue at step 3. The new request remains pending until administrator approval.
- **2e. Access was removed or expired:** Do not restore privileges through request submission alone; use the administrator renewal workflow.
- **3a. Saving fails:** Report that the request was not confirmed, preserve the previous state, and allow retry. A retry after an actual successful write returns the existing request.

**Priority:** High for the approved-volunteer access workflow.
**Frequency of Use:** Once per prospective volunteer, with possible retries.
**Business Rules:** `BR-volunteer-role-approval`
**Associated Information:** Account identity comes from sign-in, never a user-selected target account. Store request status and timestamps; only the requester and administrators may view the request. Failed writes do not grant access.
**Related Use Cases:** `UC-ADM-review-volunteer-request`, `UC-IDV-verify-affiliation`
**Assumptions:** Approval grants a role, not a shift assignment.
**Policy:** Declined requests may be resubmitted for the next annual event; Wendy may manually approve earlier. See `BR-volunteer-request-resubmission`. Shift approval belongs to the scheduling owner.

---
## Area: Identity Verification (`IDV`)

### UC-IDV-verify-affiliation: Verify a shopper's TCU affiliation before shopping

**UC ID and Name:** `UC-IDV-verify-affiliation`: Verify a shopper's TCU affiliation before shopping
**Created By:** Evelyn Tran
**Date Created:** 2026-09-23
**Primary Actor:** Shopper
**Secondary Actors:** Volunteer (may observe or fall back to a manual check at the location)
**Trigger:** A shopper at a ReFrog location wants to begin shopping.
**Description:** A shopper verifies TCU affiliation by email code so they can be admitted to shop, per `BR-shopper-tcu-affiliation`. Confirmed with the client 2026-10-02 (resolves `OI-10`): the shopper enters their TCU email on a profile page, the system sends a one-time code to that email, and the shopper enters the code to verify. This is a deliberate choice over an in-app physical-ID check — Wendy wanted the ID step out of the app specifically so the app could focus on shoppers self-logging what they take (`UC-SHP-log-item-taken`). The email check happens once per shopper (not once per visit); after that, showing the verified screen to a volunteer at the location is the practical, physical confirmation, similar to today's process.

**Preconditions:**

- PRE-1. The shopper is signed in.

**Postconditions:**

- POST-1. After the current account-restriction check succeeds, the signed-in shopper is eligible through verified TCU email or an active administrator-granted volunteer role, enabling `UC-SHP-log-item-taken`. Role-based eligibility does not mark TCU email as verified; restrictions take precedence under `FR-AUTH-volunteer-lifecycle`.

**Main Success Scenario:**

1. The shopper indicates they want to shop.
2. The system checks current trusted account restrictions first, then determines eligibility from saved TCU email verification or an active administrator-granted volunteer role. Either qualifying path suffices; see `FR-AUTH-shopping-eligibility`.
3. The shopper enters their TCU email address on their profile.
4. The system sends a one-time code to that email address.
5. The shopper enters the code.
6. The system verifies the code and marks the shopper as TCU-affiliated.
7. The system displays “TCU email verified”, which the shopper can show a volunteer.
8. The shopper shows the confirmation to the volunteer at the location for a quick visual check, consistent with `BR-shopper-verification`.
9. Use case ends.

**Extensions:**

- **2a. The shopper has already verified on a previous visit:**
    - 2a1. The system skips steps 3–6 and displays the existing verified confirmation.
    - 2a2. Use case continues at step 8.
- **2b. The signed-in account has an administrator-granted volunteer role:**
    - 2b1. The account displays its current role as “Volunteer” and the system skips steps 3-6, whether or not the volunteer has a TCU email. If TCU email is also verified, its verification status is displayed separately.
    - 2b2. Use case continues at step 8; email-verification state is unchanged.
- **2c. The account has revoked or expired volunteer access:**
    - 2c1. The account view shows “Volunteer access inactive—administrator review required”; the system denies shopping access despite retained TCU email verification.
    - 2c2. Use case ends without restoring participation permissions.
- **2d. Required current restriction or eligibility state is unavailable:**
    - 2d1. The system reports “Unable to verify access—try again”, preserves saved verification and offers retry without granting shopping access.
    - 2d2. Use case resumes at step 2 on retry.
- **3a. The entered email is not a TCU email address:**
    - 3a1. The system rejects the entry and asks the shopper to enter a TCU email.
    - 3a2. Use case resumes at step 3.
- **5a. The entered code is incorrect or expired:**
    - 5a1. The system rejects the code and offers to resend it.
    - 5a2. Use case resumes at step 4 or step 5.
- **5b. The shopper has no approved volunteer role and cannot access a TCU email:**
    - 5b1. The volunteer falls back to the current visual ID check (`BR-shopper-verification`) rather than the app.
    - 5b2. Use case ends.

**Priority:** High — named by the client alongside volunteer sign-up as a top priority.
**Frequency of Use:** The email-code check happens once per shopper; the confirmation display happens every shopping visit, thousands per event.
**Business Rules:** `BR-shopper-tcu-affiliation`, `BR-shopper-verification`, `BR-volunteer-role-approval`, `BR-volunteer-role-lifecycle`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| TCU email address | String | Required; must end in the TCU email domain | Visible only to the shopper and administrators | TCU ID |
| verification code | String | Required; time-limited, single-use | Never logged in plain form after use | — |

**Related Use Cases:** `UC-SHP-log-item-taken`: Record items taken while shopping; `UC-VOL-request-access`; `UC-ADM-review-volunteer-request`; `UC-ADM-grant-volunteer-access`; `UC-ADM-remove-volunteer-access`
**Assumptions:** Verification persists across visits once completed, rather than being repeated every time — not explicitly confirmed with the client, but implied by "that would help a lot" language around keeping the app simple for repeat shoppers.
**Open Issues:** `OI-10` is resolved. `OI-19` remains open: the existing visual-ID fallback does not yet specify how a successful physical check grants digital shopping access. Do not infer a digital override.

---

## Area: Event & Location Information (`INF`)

### UC-INF-view-event-info: View overall event dates and general information

**UC ID and Name:** `UC-INF-view-event-info`: View overall event dates and general information
**Created By:** Evelyn Tran (updated by Team 03)
**Date Created:** 2026-10-16
**Primary Actor:** App user (visitor, donor, shopper, or volunteer)
**Secondary Actors:** none
**Trigger:** A user navigates to the app landing or overview page.
**Description:** A user views high-level event dates, overall operational windows (finals week schedule), and a concise summary of what ReFrog is and how it diverts move-out waste. Prioritizes active event timing over lengthy historical narrative.

**Preconditions:** none — public information, accessible without signing in.

**Postconditions:**

- POST-1. The active event schedule and high-level event overview are displayed.

**Main Success Scenario:**

1. The user indicates they want to view event overview information.
2. The system retrieves active move-out dates, overall operating hours, and general event description.
3. The system displays the event dates, campus-wide announcement notes, and a high-level overview.
4. Use case ends.

**Extensions:**

- **2a. No active event is configured:**
    - 2a1. The system displays general off-season text explaining ReFrog's purpose and notes that dates for the upcoming move-out are pending.
    - 2a2. Use case ends.

**Priority:** High
**Frequency of Use:** Very frequent; default landing view for new or unauthenticated visitors.
**Business Rules:** `BR-event-schedule`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| event dates | Date range | Required | Public | ReFrogEvent |
| daily operating hours | Time range / Text | Required | Public | — |
| overview summary | Rich text | Required | Public | — |

**Related Use Cases:** `UC-INF-view-locations`, `UC-INF-view-role-guides`
**Assumptions:** Viewing general event info does not require sign-in.
**Open Issues:** none

---

### UC-INF-view-locations: View site locations, hours, map pins, and infographics

**UC ID and Name:** `UC-INF-view-locations`: View site locations, hours, map pins, and infographics
**Created By:** Evelyn Tran (updated by Team 03)
**Date Created:** 2026-09-23
**Primary Actor:** App user (donor, shopper, or volunteer)
**Secondary Actors:** none
**Trigger:** A user wants to find where ReFrog pods are located, check individual site hours, view external map pins, or consult visual guide infographics.
**Description:** A user views ReFrog's active physical sites for the current move-out event. Because location configurations change year-to-year and have varying daily hours (e.g., King Family Commons open Mon–Sat vs Greek Village Mon–Tue), this screen presents per-site details, interactive map navigation links (Apple/Google Maps pins), and optional infographic/site map assets uploaded by organizers.

**Preconditions:** none — public information, accessible without signing in.

**Postconditions:**

- POST-1. Active locations, operational hours per site, map pin links, and available site infographics are displayed.

**Main Success Scenario:**

1. The user indicates they want to view ReFrog locations and site details.
2. The system retrieves the list of active locations for the current event, including site name, address/description, operating days, daily hours, map pin coordinates/links, and any associated infographic image URLs.
3. The system displays the locations in a responsive list or card view, offering:
    - Direct links to open coordinates in the user's native map application (Google Maps / Apple Maps).
    - An expandable visual site map / infographic (if provided).
4. The user may select an individual location to see specific pod notes, access instructions, and accepted item categories.
5. Use case ends.

**Extensions:**

- **2a. No locations are active or configured:**
    - 2a1. The system displays a message stating that location assignments for the current event are being finalized.
    - 2a2. Use case ends.
- **3a. The user opens the external map pin:**
    - 3a1. The system launches the map URL using a standard universal map link (`https://maps.google.com/?q=lat,lng`).
    - 3a2. Use case continues.

**Priority:** High — essential for physical logistics during move-out week.
**Frequency of Use:** Very frequent; referenced repeatedly by participants navigating campus pods.
**Business Rules:** `BR-event-schedule`, `BR-event-in-person`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| location name | String | Required | Public | Donation Location |
| address / description | String | Required | Public | — |
| operating days & hours | String / Structured | Required (varies by site) | Public | — |
| map pin link / coordinates | Geo URL / lat-lng | Optional | Public | — |
| infographic / map image URL | URI | Optional | Public | — |

**Related Use Cases:** `UC-INF-view-event-info`, `UC-INF-view-role-guides`, `UC-DON-log-donation`, `UC-SHP-log-item-taken`, `UC-VOL-signup`
**Assumptions:** Accessible without sign-in on both mobile and desktop browsers.
**Open Issues:** none

---

### UC-INF-view-role-guides: View participation guidelines for donors, shoppers, and volunteers

**UC ID and Name:** `UC-INF-view-role-guides`: View participation guidelines for donors, shoppers, and volunteers
**Created By:** Team 03
**Date Created:** 2026-10-16
**Primary Actor:** App user (donor, shopper, or volunteer)
**Secondary Actors:** none
**Trigger:** A user wants specific instructions on how to participate in a given role (donating items, shopping, or volunteering).
**Description:** A user selects and views concise, role-specific guidelines:
- **Donor Guide:** What items are accepted, drop-off expectations, anonymous/no-login drop-off flow, and pod etiquette.
- **Shopper Guide:** Free shopping policy, TCU affiliation requirements (email code verification), shopping etiquette, and item logging.
- **Volunteer Guide:** Shift check-in expectations, basic on-site duties, and communication protocols.

**Preconditions:** none — public information, accessible without signing in.

**Postconditions:**

- POST-1. The selected role-specific guidance is displayed clearly to the user.

**Main Success Scenario:**

1. The user indicates they want to view participation guidelines.
2. The system presents available role tabs: "For Donors", "For Shoppers", and "For Volunteers".
3. The user selects a role tab (or defaults to Donor).
4. The system retrieves and displays the concise guidance steps, rules, and FAQs for that specific role.
5. Use case ends.

**Extensions:**

- **4a. The user switches to a different role tab:**
    - 4a1. The system displays the guidelines for the newly selected role without reloading the page.
    - 4a2. Use case continues at step 4.

**Priority:** High — prevents participant confusion and sets clear expectations before users arrive at physical pods.
**Frequency of Use:** Frequent; read by students, parents, and volunteers leading up to and during finals week.
**Business Rules:** `BR-shopper-tcu-affiliation`, `BR-free-shopping`, `BR-donation-ease`, `BR-location-staffing`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| role identifier | Enum | One of `DONOR`, `SHOPPER`, `VOLUNTEER` | Public | Donator, Shopper, Volunteer |
| instructional steps | List of Strings | Required | Public | — |
| role business rules summary | Rich text | Required | Public | — |

**Related Use Cases:** `UC-INF-view-event-info`, `UC-INF-view-locations`, `UC-DON-log-donation`, `UC-SHP-log-item-taken`, `UC-VOL-signup`
**Assumptions:** Accessible publicly without authentication.
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
**Description:** An administrator defines each shift's location, date, time, and required number of volunteers, before volunteer sign-up opens. Every other `UC-VOL-*` use case assumes shifts already exist; this is the use case that creates them. `AS-event-configuration` establishes that organizers confirm dates, hours, locations, shift lengths, and staffing needs before registration opens; confirmed 2026-10-02 that this now happens inside the app rather than SignUpGenius (resolves `OI-9`): "that would make things more integrated and help a lot."

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
| required volunteer count | Integer | Required; set per location — confirmed 2026-10-02 this varies (e.g., King Family Commons needs 3, most locations need 2), not a single fixed minimum. The administrator sets each location's count once when setting up the event; every shift created at that location starts with it, and the administrator may change it on a single shift (team discussion, 2026-10-16). It is the shift's maximum: approved and pending sign-ups together never exceed it | Editable only by administrators | — |
| operating days | Set of weekdays | Required; set per location — confirmed 2026-10-02 this also varies (some locations open Monday–Tuesday only, others Monday–Saturday) | Editable only by administrators | — |

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift; `UC-ADM-view-dashboard`; `UC-ADM-notify-volunteers`: Notify volunteers of open shifts
**Assumptions:** Shift creation happens inside this app rather than a separate tool — confirmed 2026-10-02 (resolves `OI-9`).
**Open Issues:** none beyond editing/removing a shift with assigned volunteers (extension 6a), which is still unspecified.

---

### UC-ADM-notify-volunteers: Notify volunteers of open shifts

**UC ID and Name:** `UC-ADM-notify-volunteers`: Notify volunteers of open shifts
**Created By:** Evelyn Tran
**Date Created:** 2026-10-02
**Primary Actor:** Administrator
**Secondary Actors:** Volunteer (receives the notification)
**Trigger:** An administrator decides shifts need more coverage and wants to prompt volunteers to sign up.
**Description:** An administrator manually sends a notification to volunteers about open shifts, rather than the system notifying automatically on every cancellation. This resolves `OI-7`: Wendy asked for a way to prompt volunteers when slots are open (to reduce the founders personally covering gaps), but was explicit she doesn't want volunteers over-notified — "I don't want to mistreat our volunteers... I wouldn't want them to be getting too many notifications." The team proposed, and she approved, putting the decision of when to send one in the administrator's hands rather than automating it.

**Preconditions:**

- PRE-1. The administrator is signed in with administrator privileges.

**Postconditions:**

- POST-1. A notification has been sent to the selected recipients.

**Main Success Scenario:**

1. The administrator indicates they want to notify volunteers about open shifts.
2. The system asks whether to notify everyone, or only volunteers who have not signed up for any shift.
3. The administrator selects the audience and confirms.
4. The system sends the notification.
5. The system confirms the notification was sent.
6. Use case ends.

**Extensions:**

- **4a. The notification cannot be sent (e.g., the delivery channel is unavailable):**
    - 4a1. The system informs the administrator the notification did not go out.
    - 4a2. The administrator may retry from step 3.

**Priority:** High — directly addresses the client's stated pain point of covering shifts themselves, without the over-notification risk she flagged.
**Frequency of Use:** At the administrator's discretion — the client described a cadence of roughly once a week or every few days in the weeks before an event, not per-cancellation.
**Business Rules:** none identified yet.

**Associated Information:** No new data fields beyond the shift and volunteer data already captured in `UC-VOL-signup` and `UC-ADM-manage-shifts`. The notification goes by email and into each recipient's in-app inbox (`FR-NOTIFY-in-app-inbox`); only the email provider is still to be chosen.

**Related Use Cases:** `UC-VOL-cancel-shift`: Cancel a signed-up shift; `UC-VOL-signup`: Sign up for a volunteer shift
**Assumptions:** none.
**Open Issues:** none — `OI-7` is resolved. The channel is settled (email plus in-app inbox, team decision 2026-10-16); only the email provider remains open, tracked in the architecture document.

---

### UC-ADM-review-volunteer: Approve or deny a new volunteer

**UC ID and Name:** `UC-ADM-review-volunteer`: Approve or deny a new volunteer
**Created By:** Evelyn Tran
**Date Created:** 2026-10-16
**Primary Actor:** Administrator
**Secondary Actors:** Volunteer (receives a notification of each decision)
**Trigger:** A signed-in person who is not yet an approved volunteer has signed up for a shift (`UC-VOL-signup` 5c).
**Description:** An administrator reviews a new volunteer's pending sign-ups. **Approval is once per person:** it gives the person the Volunteer role and confirms all their pending sign-ups, and from then on their sign-ups are confirmed without review. **Denial is per sign-up:** it denies one pending sign-up, so its slot reopens and the person cannot sign up for that shift again. Their other sign-ups, in this event or later ones, are not affected. The person is notified of each decision. This comes from a team discussion on 2026-10-16.

**Preconditions:**

- PRE-1. The administrator is signed in with administrator privileges.

**Postconditions:**

- POST-1. Approved: the person holds the Volunteer role, and every sign-up they had pending is confirmed. It still occupies the same slot.
- POST-2. Denied: that one sign-up is recorded as denied, its slot reappears as open, and the person cannot sign up for that shift again. Their other sign-ups and their volunteer status are unchanged.
- POST-3. The person has been notified of the decision, or the administrator can see that the notification failed.

**Main Success Scenario:**

1. The administrator indicates they want to review new volunteers.
2. The system displays each person awaiting approval with their name, email, and pending shifts.
3. The administrator approves a person.
4. The system gives the person the Volunteer role and confirms their pending sign-ups, together, as one change.
5. The system notifies the person that they are approved.
6. The system confirms the approval to the administrator.
7. Use case ends.

**Extensions:**

- **2a. Nobody is awaiting approval:**
    - 2a1. The system displays that there is nobody to review.
    - 2a2. Use case ends.
- **3a. The administrator denies one of the person's pending sign-ups:**
    - 3a1. The system records that sign-up as denied, and its slot reopens.
    - 3a2. The system notifies the person that their sign-up for that shift was denied.
    - 3a3. The system confirms the denial to the administrator. If the person has other pending sign-ups, they are still awaiting approval; otherwise the use case ends.
- **3b. The person cancelled all their pending sign-ups before the administrator decided:**
    - 3b1. The administrator may still approve the person; the approval is recorded and there are no sign-ups to change.
- **4a. The change cannot be saved (e.g., a network or server failure):**
    - 4a1. The system informs the administrator the decision was not recorded; the person and their sign-ups are unchanged.
    - 4a2. The administrator may retry from step 3.
- **4b. The same decision is submitted twice (duplicate submission):**
    - 4b1. The system records it once, notifies the person once, and confirms it again (`FR-SAVE-duplicate-submission`).
- **5a. The notification cannot be sent (e.g., the delivery channel is unavailable):**
    - 5a1. The decision stays recorded, and the system shows the administrator that the notification did not go out (`FR-NOTIFY-preserve-business-state`).
    - 5a2. Use case resumes at step 6.

**Priority:** High — once this exists, no new volunteer's sign-up is confirmed until an administrator acts.
**Frequency of Use:** Once per new volunteer, concentrated in the recruitment window (`AS-seasonal-readiness`).
**Business Rules:** `BR-volunteer-assignment`

**Associated Information:**

| Property name | Data type | Validation rule | Security or access concerns | Glossary reference |
|---|---|---|---|---|
| volunteer status | Enum | Not yet approved, or approved; set only by this use case | Visible to the person and administrators; editable only by administrators | Volunteer |
| sign-up decision | Enum | Pending, approved, or denied, per sign-up; a denied sign-up is final for that shift | Visible to the person and administrators; editable only by administrators | — |

**Related Use Cases:** `UC-VOL-signup`: Sign up for a volunteer shift; `UC-VOL-view-schedule`: View assigned shifts and hours; `UC-ADM-notify-volunteers`: Notify volunteers of open shifts
**Assumptions:** The decision notification goes only to the person decided on, once per decision, so it does not add to the over-notification Wendy wants to avoid (`OI-7`). It goes by email and into the person's in-app inbox (`FR-NOTIFY-in-app-inbox`).
**Open Issues:** none.

---

### UC-ADM-review-volunteer-request: Approve or decline volunteer access

**UC ID and Name:** `UC-ADM-review-volunteer-request`: Approve or decline volunteer access
**Created By:** Alexa Mercado with Codex assistance
**Date Created:** 2026-10-16
**Primary Actor:** Administrator
**Secondary Actors:** Requesting user
**Trigger:** An administrator opens volunteer access requests.
**Description:** Decide a pending request; approval grants the volunteer role. Source: team decision with Alexa Mercado, not a recorded client approval.
**Preconditions:**
- PRE-1. The administrator is signed in with administrator privileges.
**Postconditions:**
- POST-1. The decision is stored; an approval grants the role and a decline does not.
- POST-2. The administrator, time, and action are recorded for auditing.
**Main Success Scenario:**
1. The administrator opens pending requests.
2. The system checks privileges and displays the requests.
3. The administrator selects a request.
4. The system displays its account and current status.
5. The administrator approves or declines.
6. The system saves the decision, any role grant, and the audit event as one complete operation.
7. The system confirms the resulting request and role status.
**Extensions:**
- **2a. User is not an administrator:** Deny access without returning requests; use case ends.
- **2b. No pending requests:** Display an empty list; use case ends.
- **4a. Request is missing or no longer pending:** Display the current result or not-found response; do not overwrite a prior decision.
- **5a. Administrator cancels:** Make no changes; use case ends.
- **6a. Persistence fails:** Report failure and leave request and role state unchanged; permit retry.
- **6b. Another administrator decided first or the request is submitted twice:** Return the saved decision, refuse a conflicting change, and do not duplicate the grant.
**Priority:** High for volunteer access approval.
**Frequency of Use:** During volunteer recruitment.
**Business Rules:** `BR-volunteer-role-approval`, `BR-shopper-verification`
**Associated Information:** Only administrators can decide requests. Pending and declined requests convey no role. Approval does not assign a shift or set email-verification state.
**Related Use Cases:** `UC-VOL-request-access`, `UC-ADM-remove-volunteer-access`, `UC-IDV-verify-affiliation`
**Assumptions:** No approval notification channel is added by this decision; users can see request status in their account.
**Open Issues:** `OI-18`.

---

### UC-ADM-grant-volunteer-access: Add a volunteer by email

**UC ID and Name:** `UC-ADM-grant-volunteer-access`: Add a volunteer by email
**Created By:** Alexa Mercado
**Date Created:** 2026-10-16
**Primary Actor:** Administrator, including Wendy
**Secondary Actors:** Volunteer; sign-in provider
**Trigger:** An administrator chooses to add a volunteer directly.
**Description:** Grant access without a user request by matching a verified sign-in email. Source: team decision with Alexa Mercado, not a recorded client approval.
**Preconditions:**
- PRE-1. The administrator is signed in with administrator privileges.
**Postconditions:**
- POST-1. The matching account has the volunteer role, or an email approval is pending verified account sign-in.
- POST-2. The administrator action is recorded for auditing.
**Main Success Scenario:**
1. The administrator selects Add volunteer by email.
2. The system verifies administrator privileges and asks for an email.
3. The administrator enters the email and confirms.
4. The system validates it and looks for an account with the matching verified sign-in email.
5. The system grants the role to the matching account and records the approval and audit event.
6. The system confirms volunteer access was granted.
**Extensions:**
- **2a. User lacks privileges:** Deny access; use case ends.
- **3a. Administrator cancels:** Make no changes; use case ends.
- **4a. Email is invalid:** Request correction; resume step 3.
- **4b. No matching account exists:** Store a pending email approval and audit event; display that access awaits sign-in. On later sign-in, apply the unrevoked approval only when the provider verifies the matching email; store the role and claimed approval atomically. A different or unverified email cannot claim it.
- **4c. More than one account matches:** Do not grant access; report that account resolution is required.
- **5a. Role or pending approval already exists:** Return the existing state without creating a duplicate.
- **5b. Saving fails, including during later approval claim:** Report failure; do not confirm a grant or claim that did not persist; allow safe retry.
**Priority:** High for administrator-directed volunteer access.
**Frequency of Use:** During recruitment and volunteer onboarding.
**Business Rules:** `BR-volunteer-role-approval`
**Associated Information:** A typed profile email or TCU affiliation email is insufficient to claim a manual grant; match provider-verified sign-in email. Pending approval grants no access. No account credentials, invitation message, or shift assignment are created by this use case.
**Related Use Cases:** `UC-ADM-remove-volunteer-access`, `UC-IDV-verify-affiliation`
**Assumptions:** General sign-in remains the existing authentication flow.
**Role lifecycle:** At the configured annual event end, volunteer privileges expire. Each new annual event requires administrator review and approval before renewal; access does not renew automatically. Email changes on the same account preserve an active role. See `BR-volunteer-role-lifecycle`.
**Open Issues:** Remaining policies in partially resolved `OI-18`; shift approval and assignments are outside this update.

---

### UC-ADM-remove-volunteer-access: Remove volunteer access

**UC ID and Name:** `UC-ADM-remove-volunteer-access`: Remove volunteer access
**Created By:** Alexa Mercado
**Date Created:** 2026-10-16
**Primary Actor:** Administrator
**Secondary Actors:** Affected user
**Trigger:** An administrator decides a role or pending email approval should be removed.
**Description:** Revoke volunteer access and restrict the account to general information; retained TCU email verification does not permit participation. Source: team decision with Alexa Mercado, not a recorded client approval.
**Preconditions:**
- PRE-1. The administrator is signed in with administrator privileges.
**Postconditions:**
- POST-1. The role or pending approval is revoked and older pending grants cannot restore it on sign-in.
- POST-2. The administrator action is audited; the account has general-information access only even if TCU email verification is retained.
**Main Success Scenario:**
1. The administrator selects an account or pending email approval.
2. The system verifies privileges and displays the current access state.
3. The administrator confirms removal.
4. The system removes the role or revokes the pending approval, invalidates older pending grants and requests, records the account restriction where an account exists, clears active assignments and releases occupied capacity, and records the audit event atomically.
5. The system confirms removal. Subsequent protected checks enforce general-information-only access, including for existing sessions.
**Extensions:**
- **2a. Caller lacks privileges:** Deny access; use case ends.
- **2b. Target does not exist:** Report not found; make no changes.
- **3a. Administrator cancels:** Make no changes; use case ends.
- **4a. Access was already removed:** Confirm the existing state without further effect.
- **4b. Persistence fails:** Preserve prior state and report failure; permit retry.
**Priority:** High for role integrity.
**Frequency of Use:** Occasional corrections or volunteer status changes.
**Business Rules:** `BR-volunteer-role-approval`, `BR-shopper-verification`
**Associated Information:** Role removal places the account in general-information-only access, even if it retains previously verified TCU email; administrator renewal is required to restore volunteer privileges. Shift cancellation alone never removes the role. Role removal clears active assignments and their occupied capacity; historical records follow retention policy. Shift approval remains with the scheduling owner.
**Related Use Cases:** `UC-ADM-review-volunteer-request`, `UC-ADM-grant-volunteer-access`, `UC-IDV-verify-affiliation`
**Assumptions:** Any later role grant requires a new explicit administrator decision.
**Open Issues:** `OI-18`.

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

- PRE-1. The shopper is signed in and has either persisted TCU email verification or an administrator-granted volunteer role, as checked by `UC-IDV-verify-affiliation`.

**Postconditions:**

- POST-1. The shopping visit (location, date, item count, and the category of each item) is recorded.

**Main Success Scenario:**

1. The shopper indicates they are finished selecting items.
2. The system asks the shopper to enter the number of items taken and to select a category for each item, from the same fixed category set used for donations (confirmed 2026-10-02, matching `UC-DON-log-donation`'s extension 2a).
3. The shopper enters the count, selects a category for each item, and confirms.
4. The system records the shopping visit.
5. The system confirms the entry to the shopper.
6. Use case ends.

**Extensions:**

- **3a. The shopper enters an invalid count (negative, non-numeric, or zero):**
    - 3a1. The system rejects the entry and asks the shopper to correct it.
    - 3a2. Use case resumes at step 2.
- **3b. The shopper does not select a category for every item:**
    - 3b1. The system rejects the entry, indicates which items are missing a category, and keeps the values already entered.
    - 3b2. Use case resumes at step 3.
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
| category | Enum, same fixed set as `UC-DON-log-donation` | Required; one category per item taken, so the number of categories equals the item count (repeats allowed) | Visible to administrators | Item |

A failed save leaves no visit recorded. A retry of the same submission, whether user-initiated or a duplicate, must not record the same visit twice.

**Related Use Cases:** `UC-IDV-verify-affiliation`, `UC-ADM-view-dashboard`, `UC-ABU-review-alert`
**Assumptions:** Items are counted, not described individually: the shopper gives a count and a category for each item, with no item names or descriptions. Category became required by team decision on 2026-10-16 (previously optional extension 2a). Confirmed 2026-10-02 that the shopper self-reports this (not a volunteer on their behalf), resolving `OI-13`.
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
**Description:** A donor records what they donated, replacing the current QR-code Google Form, so ReFrog can track donation activity by location. A volunteer may also complete this on a donor's behalf.

**Preconditions:** none. Confirmed 2026-10-02 (resolves `OI-4`): donation logging does not require sign-in. Wendy was explicit she's "happy for it to be anybody," including parents dropping items off for a student.

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
- **2a. The donor selects a category for the donation:**
    - 2a1. The system offers a small, fixed set of categories (confirmed 2026-10-02: 5–6 click-box options, e.g., mattress toppers, headboards, large furniture) rather than free text, consistent with the client's condition that categorization must not make donating harder.
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
| category | Enum, one of a fixed set (e.g., mattress toppers, headboards, large furniture — final list TBD) | Optional | Visible to administrators | Item |

A failed save leaves no donation recorded. A retry of the same submission, whether donor-initiated or a duplicate, must not record the same donation twice.

**Related Use Cases:** `UC-INF-view-locations`, `UC-INF-view-role-guides`, `UC-ADM-view-dashboard`
**Assumptions:** none.
**Open Issues:** none — `OI-4` is resolved. The exact final category list is not yet finalized with the client, but the shape (5–6 fixed options) is confirmed.

---

## Working these with your agent

_[Delegate: drafting the main success scenario once you have the trigger and the goal; proposing extensions you have not thought of, which it is genuinely good at; turning a filled-in use case into a first set of test cases; checking that every `BR-*` you cite exists in [business-rules.md](business-rules.md).]_

_Keep human: whether this is one use case or three, what the priority is, and whether an extension the agent proposed is a real path in your client's business or a generic one it has seen elsewhere. "The system handles concurrent edits" is a real requirement for some projects and invented complexity for others, and only you have met the client._

_The verification that catches the most: read the main success scenario aloud to someone who has not read the document, and stop wherever they ask a question. Every question is a missing step or a missing extension._

_**Checklist for each use case:** Does the name start with a verb? Can the system test every precondition? Does every step alternate actor and system? Is there at least one extension per step that can fail? Does every business rule appear as an identifier only? Could a tester write test cases from this without asking you anything?_
