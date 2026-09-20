# Business Rules

**Project:** _[Refrog]_
**Team:** _[Team 3]_
**Client:** _[Wendy Macias, Texas Christian University]_
**Version:** 0.1

---

_**How to use this template.** Instructions appear in italic square brackets. Fill in underneath them and leave them in place until the document is stable._

_**What a business rule is.** A corporate policy, a government regulation, a law, an industry standard, or a computational formula. Business rules are a rich source of requirements, because they dictate properties your system must have in order to conform to them._

_**What a business rule is not: a software requirement.** This is the distinction students get wrong, so read it twice. A rule is a property of the **business**. It exists whether or not your software does, it was true before you arrived, and it will still be true if the project is cancelled. "A student may only submit a peer evaluation during an active week" is a rule the course had before anyone wrote code._

_What belongs to your software is the **enforcement** of that rule, and that is a functional requirement, written in the specification and cited back here. Keeping the two apart is what lets you answer the question that comes up every semester: "who decided this, and can we change it?" If it is a rule, the client's organization decides and you comply. If it is a requirement, your team decides and you can negotiate._

## How to hear one in a meeting

_[Rules almost never arrive announced. They surface in the middle of a story about something else, usually in one of these shapes:]_

- _"Must comply with..."_
- _"Only `<someone>` may `<do something>`"_
- _"If `<condition>`, then `<something happens>`"_
- _"Must be calculated according to..."_
- _"...unless it has been more than a year."_

_Examples of a client stating a rule without knowing it: "A new client must pay 30 percent of the estimated consulting fee and travel expenses in advance." "Time-off approvals must comply with the company's vacation policy."_

_When you hear one, write it down in the meeting. You will not reconstruct it afterward, and the exact wording matters because the rule is someone else's sentence, not yours._

## The five shapes a rule takes

_[Useful for recognizing rules, not for organizing this document. Sections below are grouped by topic, not by these categories.]_

| Shape | What it does | Example |
|---|---|---|
| **Fact** | States something always true about the business | Every senior design team belongs to exactly one course section. |
| **Constraint** | Restricts what may be done, or by whom | Only a course admin may create a course section. |
| **Action enabler** | Triggers an action when a condition holds | If a student has not completed safety training in 12 months, the request is refused. |
| **Inference** | Derives a new fact from known facts | A team with no submissions for two consecutive weeks is at risk. |
| **Computation** | Defines how a value is calculated | The peer evaluation score is the mean of all scores received that week. |

_Computations are the ones teams forget are rules. A formula the client uses today is a rule you must reproduce exactly, not a design decision you get to make. Ask for the spreadsheet._

## What a rule turns into

_[One rule usually propagates into several requirements of different kinds. This is why the document exists as its own artifact rather than being scattered through the specification.]_

| Requirement type | How the rule shows up | Example |
|---|---|---|
| Business requirement | A regulation drives a business objective | The system must enable compliance with all federal and state chemical reporting regulations within five months. |
| User requirement | A privacy policy dictates who may do what | Only laboratory managers may generate chemical exposure reports for anyone other than themselves. |
| Functional requirement | A company policy becomes system behavior | If an invoice is received from an unregistered vendor, the system shall email the vendor the supplier intake form and the W-9. |
| Quality attribute | A safety regulation becomes a checked property | The system must maintain safety training records and check them before a user can request a hazardous chemical. |

## Identifiers and traceability

_Each rule carries a stable `BR-<slug>` identifier, a name-based slug coined from the rule's gist: `BR-active-weeks`, `BR-section-admin-only`, `BR-artifact-key-unique`. Never renumber, rename, or repoint one. The thematic grouping into sections below is organizational only and does not affect a rule's identity, so moving a rule between sections is free and renaming it is not._

_**Cite rules, do not copy them.** When a use case is governed by a rule, its Business Rules field carries the identifier only, never the rule's text. One rule, one home. A rule copied into three use cases will be updated in one of them._

_A rule may cite another rule by identifier where one depends on another._

## Every rule needs a source

_[The column teams leave blank, and the one that matters most. For each rule, record where it comes from: a named policy document, a regulation, a page of the client's handbook, or the person who told you and the date.]_

_A rule you cannot attribute is usually not a rule. It is your team's design decision wearing a rule's clothes, and it belongs in the specification where it can be argued with. The test: if you asked your client to change it tomorrow, who would have to approve? If the answer is "you", it was never a rule._

_Where a rule is expected to change, say so and say when. Rules change on the business's schedule, not on yours._

## Where your AI teammate helps, and where it is dangerous

_[Delegate: turning your meeting notes into candidate rules, spotting sentences in a transcript that have the shape of a rule, and finding use cases in your specification that a given rule ought to govern but does not cite.]_

_**Do not let it invent rules.** This section is the single most dangerous place in your requirements for fabricated content, because an invented rule reads exactly like a real one. "Passwords must be at least 8 characters." "Records must be retained for 7 years." Both are plausible, both are common, and neither is your client's policy unless your client said so. A fabricated rule then propagates into functional requirements, tests that pass, and code that enforces something nobody asked for._

_The Source column is the defense. Every rule traces to a document or a person, or it does not go in the file. When your agent proposes a rule, the only question is: who told us this?_

## Revision History

| Date | Version | Description | Author |
|---|---|---|---|
| _[YYYY-MM-DD]_ | 0.1 | Initial rules from the client brief and first client meeting | _[Name]_ |

---

## 1. Introduction

### 1.1 Purpose

[One paragraph: this document collects the policies, regulations, standards, and formulas that govern the business your software operates in, so the specification can cite them rather than restate them.]

This document records the policies and operating practices that govern ReFrog's volunteer coordination, in-person donation and shopping event, TCU-affiliation checks, item tracking, and donation-partner pickups. The software requirements will cite these rules rather than restating them, while the ReFrog administrators remain the authority for confirming or changing the rules.

### 1.2 Scope

[Which parts of the client's business these rules cover, and which are out of scope. If your client's organization has rules that your system does not touch, say so here rather than silently omitting them.]

These rules cover the parts of ReFrog's operations that the proposed system may support: recruiting and assigning volunteers, managing shifts and cancellations, staffing physical donation locations, recording donation and shopping activity by location, checking whether shoppers are affiliated with TCU, reviewing possible shopping abuse, and coordinating the distribution of remaining usable items to donation partners. They also cover the event's documented operating context, including its finals-week schedule, physical locations, and partner pickup process.

These rules do not cover the physical handling, sorting, storage, or transportation of items; the internal policies of TCU or the donation partners; the technical choice of application platform or authentication provider; or exact definitions and penalties for shopping abuse that the ReFrog committee has not yet approved. Those matters may create requirements or dependencies, but they are not treated as business rules here until an authoritative source is provided.

---

## 2. Rules

_[Group rules under topic headings that fit your project. The Project Pulse headings are one example, not a required set: Course Administration, Teams and Assignment, Access and Ownership, Identity and Uniqueness, Editing and Locking, Deletion Integrity, Review and Submission._

_Format each rule as a bold identifier, the rule in one sentence, then its source. Worked examples:]_

### 2.1 _[Topic]_

- **`BR-active-weeks`:** A student may submit or edit a weekly activity report only during a week that the course section has marked active.
  **Source:** course policy, confirmed by the instructor 2026-09-10.
- **`BR-section-admin-only`:** Only a course admin may create or edit a course section, configure its active-weeks window (see `BR-active-weeks`), or assign a rubric to it.
  **Source:** department policy on grade-bearing records.
- **`BR-artifact-key-unique`:** Every artifact key is unique within a team and remains stable across edits to the artifact's content.
  **Source:** team decision, 2026-09-10. **Candidate for the specification instead of this file**, since the team, not the client, would approve a change.

**Eligibility and Shopping:**
- **`BR-shopper-verification`:** A person must demonstrate TCU affiliation before shopping at ReFrog locations. **Source:** Current-process description in vision-and-scope.md §1.2; current method is visual inspection of a physical or phone ID. Electronic verification is unresolved.
- **`BR-free-shopping`:** ReFrog shoppers receive usable donated items without charge. 
  **Source:** Client brief and client meeting, recorded in vision-and-scope.md §1.1.
- **`BR-shopping-activity-recorded`:** Each shopping visit records the event date, location, and number of items taken. 
  **Source:** Existing ReFrog Shopping Form, obtained from Wendy Macias, September 11, 2026.
- **`BR-shopping-abuse-review`:** Suspected excessive or otherwise prohibited shopping must be reviewed by a ReFrog administrator before any action is taken.
  **Source:** vision-and-scope.md §2.6 and AS-client-policy-decisions. The definition of “excessive” or “prohibited” behavior remains unresolved.
- **`BR-shopper-tcu-affiliation`:** ReFrog shopping is available to TCU students, faculty, staff, and volunteers.     
  **Source:** Client meeting with Wendy Macias, September 11, 2026, recorded in vision-and-scope.md §1.1.

**Event Operations:**
- **`BR-event-in-person`:** Donations and shopping take place at staffed physical ReFrog locations.
  **Source:** Client meeting and vision-and-scope.md §2.5.
- **`BR-location-staffing`:** Each operating donation location must have a volunteer presence while the location is open.
  **Source:** vision-and-scope.md §2.5 and §3.2. The required number of volunteers per location still needs confirmation.
- **`BR-event-schedule`:** The ReFrog event operates during finals week, generally from Monday through Saturday and beginning around 2:00 p.m.
  **Source:** Current-process description in vision-and-scope.md §1.2 and §3.2. Exact dates and hours must be confirmed for each event.
- **`BR-volunteer-assignment`:** A volunteer may work only the location and shift to which they are assigned or have claimed.
  **Source:** Proposed process flow and AS-event-configuration. Cancellation and reassignment procedures still need client confirmation.

**Donations and Item Handoffs:**
- **`BR-donation-location-recorded`:** Each donation must be associated with the ReFrog location where it was received.
  **Source:** Existing ReFrog Donation Form, obtained from Wendy Macias, September 11, 2026.
- **`BR-donation-count-recorded`:** Each donation submission records the number of items donated.
  **Source:** Existing ReFrog Donation Form, obtained from Wendy Macias, September 11, 2026.
- **`BR-partner-pickup-priority`:** Donation partners receive eligible remaining items according to the event’s agreed pickup order and item priorities.
  **Source:** Current-process description in vision-and-scope.md §1.2. The exact priority order should be confirmed with Wellman Project, Trio, Archway, and ReFrog organizers.
- **`BR-remaining-items-distributed`:** Usable items remaining after shopping are made available to ReFrog’s donation partners for pickup.
  **Source:** vision-and-scope.md §1.2 and §2.5.


_[That third entry is deliberate. Flag rules you are not sure about rather than dropping them; deciding whether something is a rule or a requirement is a conversation to have with your client, and it is worth having.]_

_**Checklist:** Does every rule have a source? Could your client change it without asking you? Is it stated as one sentence about the business, rather than as a sentence about your software? Does any use case cite it, and if none does, is that correct?_
