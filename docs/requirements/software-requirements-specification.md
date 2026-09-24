# Software Requirements Specification

**Project:** _[Your project name]_
**Team:** _[Team NN]_
**Client:** _[Client name and organization]_
**Version:** 0.1

---

_**How to use this template.** Instructions appear in italic square brackets. Fill in underneath them and leave them in place until the document is stable._

_**What this document is, and what it is not.** The specification describes the external behavior of your system completely enough that a developer can build it and a tester can check it. What it is **not** is a container for everything you have written. Your glossary, vision and scope, use cases, and business rules are separate documents with their own identifiers, and this one **links to them rather than repeating them**._

_That makes the specification mostly a hub. Read that as a feature. One fact, one home: a business rule copied in here is a business rule that will disagree with `business-rules.md` by October, and nobody will notice which copy is right. The sections below that say "link to" are supposed to be short._

_What this document owns outright: the requirements that have no other home. Functional requirements that are not part of any use case, quality attributes, external interfaces, data requirements, operating environment, and constraints._

## Identifiers

_Every requirement in this document carries a name-based slug. Create only the spaces your project actually needs._

| Space | For | Example |
|---|---|---|
| `FR-<AREA>-<slug>` | Functional requirements outside any use case | `FR-SAVE-autosave-active` |
| `UI-<slug>` | User interface requirements | `UI-spa-views` |
| `SI-<slug>` | Software and system interfaces | `SI-llm-proxy-only` |
| `CI-<slug>` | Communications interfaces | `CI-email-notifications` |
| `DI-<slug>` | Data requirements | `DI-persist-graph` |
| `OE-<slug>` | Operating environment | `OE-supported-browsers` |
| `CO-<slug>` | Design and implementation constraints | `CO-single-application` |
| `AS-<slug>` / `DE-<slug>` | Assumptions and dependencies | `AS-supported-browser`, `DE-llm-service` |

_Quality attributes get one space per attribute, so the identifier says which kind of quality it is at the place it is cited: `USE-` usability, `PER-` performance, `SEC-` security, `SAF-` safety, `AVL-` availability, `ROB-` robustness, `SCA-` scalability, `INT-` interoperability, `MNT-` maintainability._

_Requirements cited from elsewhere keep their own identifiers: `UC-*` from [use-cases.md](use-cases.md), `BR-*` from [business-rules.md](business-rules.md), `BO-*`, `SM-*`, `FEAT-*` from [vision-and-scope.md](vision-and-scope.md)._

## Revision History

| Date | Version | Description | Author |
|---|---|---|---|
| _[YYYY-MM-DD]_ | 0.1 | Initial draft | _[Name]_ |

---

## 1. Introduction

### 1.1 The purpose of _[project name]_

_[What the system is for: who wants it, why, and who will use it. Even though the vision and scope answers this, restate it in a paragraph here, because people read this document without having read that one.]_

### 1.2 The purpose of this document

_[What this specification covers and for which release.]_

_Example: "This document describes the functional and nonfunctional requirements for release 1.0 of the Cafeteria Ordering System. It serves as the reference for the project's requirements, defining the scope, functionality, and constraints for stakeholders, developers, and testers."]_

### 1.3 Document conventions

_[Any typographical conventions, and the identifier formats above, so that someone adding a requirement later knows how to name it.]_

### 1.4 References

_[Every document this specification refers to, with a link. At minimum, the four other documents in this folder. Include external standards you must conform to.]_

- _[Project glossary](project-glossary.md)_
- _[Vision and scope](vision-and-scope.md)_
- _[Use cases](use-cases.md)_
- _[Business rules](business-rules.md)_
- _[Open issues](OPEN-ISSUES.md)_
- _[The Easy Approach to Requirements Syntax (EARS)](https://alistairmavin.com/ears/)_

---

## 2. Overall Description

### 2.1 Product perspective

_[How this system relates to other systems and to the user's environment. Self-contained, or one component of something larger? Link to the product perspective section of your vision and scope and to your architecture's context diagram rather than redrawing them.]_

### 2.2 User classes and characteristics

_[The kinds of user, and what distinguishes them: frequency of use, technical skill, privilege level, whether they are inside or outside the client's organization. Link to the stakeholder profiles in your vision and scope; what belongs here is what affects the software's behavior, especially permissions.]_

### 2.3 Operating environment

_[The environment the software runs in: hardware, operating systems and versions, browsers, where users and servers are located, and any other software it has to coexist with.]_

_Examples:_

- _`OE-supported-browsers`: The system shall operate correctly on the current and previous major versions of Chrome, Firefox, Safari, and Edge._
- _`OE-server-platform`: The system shall run on a server running the current corporate-approved version of Linux._
- _`OE-access-paths`: The system shall permit access from the corporate intranet, from a VPN connection, and from Android and iOS phones and tablets._

### 2.4 Design and implementation constraints

_[Anything that limits the developers' options: corporate or regulatory policy, hardware limits, required languages or databases, coding standards, interfaces to other applications.]_

_Examples:_

- _`CO-database-engine`: The system shall use the corporate standard database engine._
- _`CO-language-version`: The backend shall be written in Java 21._
- _`CO-coding-standard`: Design, code, and maintenance documentation shall conform to the client's development standard._

_The constraint students forget: **who maintains this after you graduate, and what do they already know how to run?** If the answer is one person who knows Python, a Spring Boot service is a constraint violation nobody wrote down._

### 2.5 Assumptions and dependencies

_[An assumption is a factor you believe true without proof, which would change these requirements if it turned out false. A dependency is something outside your control that the project relies on: an external API, a third-party library, a change someone else has to make.]_

_Examples:_

- _`AS-supported-browser`: Users access the system with a browser that supports the ECMAScript version the frontend targets._
- _`DE-payroll-integration`: Operation depends on changes being made in the Payroll System to accept payment requests for meals ordered through this system._

---

## 3. Project Glossary

_[Link only. The glossary is [project-glossary.md](project-glossary.md).]_

## 4. Vision and Scope

_[Link only. Business requirements, objectives, metrics, and scope live in [vision-and-scope.md](vision-and-scope.md).]_

---

## 5. Functional Requirements

### 5.1 Use cases

_[Link to [use-cases.md](use-cases.md). Most of your system's behavior is specified there, as use cases, and it does not get restated here.]_

### 5.2 Non-use-case functional requirements

_[Behavior that is real, testable, and belongs to no single use case: autosave, validation applied everywhere, notification, authorization, audit logging. If you find yourself writing the same step into six use cases, it belongs here instead._

_Group them under sub-headings by concern, and write each one using an [EARS](https://alistairmavin.com/ears/) shape so that it cannot be read two ways:_

- _**Ubiquitous:** The `<system>` shall `<response>`._
- _**Event driven:** When `<trigger>`, the `<system>` shall `<response>`._
- _**State driven:** While `<in a state>`, the `<system>` shall `<response>`._
- _**Optional:** Where `<feature is included>`, the `<system>` shall `<response>`._
- _**Unwanted behavior:** If `<precondition>`, then the `<system>` shall `<response>`._

_Example: `FR-SAVE-autosave-active`: While a student is editing a weekly activity report during an active week, the system shall persist the draft every 30 seconds._

_**Every requirement here needs an oracle.** If you cannot say how a tester would tell whether it holds, it is not a requirement yet.]_

---

## 6. Business Rules

_[Link only, to [business-rules.md](business-rules.md). Business rules are a rich source of requirements because they dictate properties the system must have in order to conform to them, but the rules themselves are properties of the client's business, not of your software, and they have their own document.]_

---

## 7. Data Requirements

### 7.1 Business domain model

_[The entities in the problem domain and how they relate, as a mermaid class diagram. Model the **business**, not your database schema: this is what the client would recognize, before any decision about tables or persistence.]_

    ```mermaid
    classDiagram
      class Team {
        +String name
      }
      class Student {
        +String email
      }
      Team "1" --> "*" Student : has
    ```

### 7.2 Data dictionary

_[Each entity's fields, with data type, allowed values, defaults, and validation rules. Where a use case already specifies a field's validation in its Associated Information, cite the use case instead of repeating it.]_

### 7.3 Reports

_[Any report the system generates: who reads it, what it contains, how often, and in what format. Reports are where clients discover late that a field they need was never captured, so specify them early.]_

### 7.4 Data acquisition, integrity, retention, and disposal

_[Where the data comes from, how it is kept correct, how long it is kept, and how it is destroyed. If your system holds anything about students or other identifiable people, this section is not optional, and its content is usually a business rule you should cite rather than invent.]_

---

## 8. External Interface Requirements

### 8.1 User interfaces

_[The user-facing surfaces, at requirement level: which views exist, standards they must conform to, accessibility requirements. Link to wireframes or prototypes rather than describing pixel layouts.]_

### 8.2 Hardware interfaces

_[Any hardware the system talks to, or "none".]_

### 8.3 Software interfaces

_[Other software systems yours connects to: what crosses the boundary, in which direction, in what format, and what happens when the other side is unavailable.]_

### 8.4 API document

_[Link to your API documentation. It is generated from the code, so link it rather than transcribing endpoints that will be stale within a week.]_

### 8.5 Communications interfaces

_[Email, notifications, messaging, and the protocols involved.]_

---

## 9. Quality Attributes

_[How well the system does what it does. **This is the section that decides whether your client is happy with software that meets every functional requirement**, so do not treat it as a formality.]_

_The rule for every entry: an adjective is not a requirement. "Fast", "easy", "secure", and "user-friendly" are the starting point of a conversation, not the end of one. Each entry needs a number and a way to measure it._

_Write one subsection per attribute your project actually has, and say "not applicable" with a reason for the ones it does not. An explicit "not applicable" is information; silence is not._

### 9.1 Usability

_Example: `USE-wcag-aa`: All user-facing views shall conform to WCAG 2.1 level AA._

ReFrog participants commonly interact with the system on personal smartphones at outdoor, physically distributed event locations. Donator and volunteer interactions must remain brief because users may be working in noisy, fast-moving conditions. The thresholds below are proposed for team and client review and are tracked in [srs-quality-localization-decisions.md](srs-quality-localization-decisions.md).

- `USE-mobile-responsive`: The system shall make all participant workflows usable without horizontal scrolling at a viewport width of 320 CSS pixels.
- `USE-donation-completion`: At least 90 percent of representative first-time users shall complete a donation submission without assistance within 60 seconds.
- `USE-volunteer-signup-completion`: At least 90 percent of representative first-time volunteers shall find and claim an available shift without assistance within two minutes.
- `USE-admin-no-code`: A ReFrog administrator shall be able to configure event dates, locations, shifts, and staffing requirements without changing application code.
- `USE-validation-feedback`: When a user provides invalid or incomplete input, the system shall identify each affected field, explain how to correct it, and preserve all valid input already entered.
- `USE-accessibility`: All user-facing views shall conform to WCAG 2.1 Level AA.

### 9.2 Performance

_Example: `PER-report-load`: A peer evaluation report for a section of 80 students shall render within 2 seconds at the 95th percentile._

The most recent documented event involved 193 volunteers, approximately 2,573 shoppers, and approximately 9,600 items taken across seven locations; eight locations are planned for the next event. Concurrent-user counts and representative network conditions still require confirmation.

- `PER-page-load`: Under the approved peak load and test-network conditions, 95 percent of participant-facing pages shall become usable within three seconds.
- `PER-form-submission`: Under the approved peak load, 95 percent of donation, shopping, and volunteer submissions shall receive a success or failure response within two seconds.
- `PER-dashboard-load`: The administrator dashboard shall display current event information within five seconds for an event containing the approved maximum number of records.
- `PER-measurement`: Performance verification shall use a documented workload representing peak finals-week activity rather than average annual activity.

### 9.3 Security

_Example: `SEC-authentication`: The system shall authenticate every request to a non-public endpoint, and shall reject unauthenticated requests without disclosing whether the requested resource exists._

- `SEC-authenticated-administration`: The system shall authenticate every request that creates, modifies, deletes, exports, or displays nonpublic administrative information.
- `SEC-role-based-access`: The system shall authorize protected operations according to the authenticated user's assigned role.
- `SEC-least-privilege`: The system shall prevent donators, shoppers, and volunteers from accessing administrative dashboards or other participants' records.
- `SEC-affiliation-data`: The system shall disclose TCU-affiliation information only to authorized users and only when required to perform an approved ReFrog workflow.
- `SEC-administrator-review`: The system shall restrict access to suspected shopping-abuse information to authorized ReFrog administrators, consistent with `BR-shopping-abuse-review`.
- `SEC-transport-encryption`: The system shall encrypt network communication containing authentication credentials, affiliation information, or participant records by using HTTPS.
- `SEC-audit-log`: The system shall record successful and unsuccessful administrator authentication attempts and administrator changes to event, schedule, and participant records.
- `SEC-authentication-provider`: The system shall use the authentication and TCU-affiliation method approved by ReFrog and TCU. The choice among TCU single sign-on, TCU email verification, another electronic method, or continued in-person ID verification remains unresolved under `AS-authentication-approach` and `AS-affiliation-verification`.
- `SEC-session-expiration`: The system shall expire an administrator session after the team-approved period of inactivity.

### 9.4 Safety

_[Conditions under which the system could contribute to harm, and what prevents it. For most projects in this course the honest answer is `SAF-not-applicable`, with a sentence saying why.]_

- `SAF-not-applicable`: The system does not control the physical receipt, sorting, storage, transportation, or disposal of donated items and is not intended to make safety-critical decisions. Physical event-safety procedures remain outside the software's scope. This determination must be reconsidered if the system is later expected to provide emergency instructions or control safety-critical staffing decisions.

### 9.5 Availability

_Example: `AVL-uptime`: The system shall be available 99% of the time during the academic term, excluding announced maintenance windows._

- `AVL-event-hours`: The system shall be available during the recruitment period and all published ReFrog event operating hours, subject to the approved availability target.
- `AVL-uptime`: The system shall maintain at least 99 percent availability during the volunteer-recruitment period and finals-week event period, excluding approved maintenance windows.
- `AVL-maintenance-window`: The system shall not schedule routine maintenance during published event operating hours.
- `AVL-fallback-information`: Before each event, the system shall allow administrators to obtain an accessible backup of schedules, location information, and attendance procedures for use during an outage.
- `AVL-outage-recovery`: The system shall restore essential scheduling and event-information functions within 30 minutes of a service interruption.

### 9.6 Robustness

_Example: `ROB-edit-loss-bound`: On an unexpected client disconnect, the system shall lose no more than 30 seconds of a student's in-progress edits._

- `ROB-submission-status`: If a submission cannot be completed, the system shall clearly indicate that it was not recorded and preserve the user's entered information for another attempt.
- `ROB-duplicate-submission`: When a user retries a donation, shopping, or volunteer submission after an interrupted response, the system shall prevent the retry from creating an unintended duplicate record.
- `ROB-invalid-input`: If submitted data violates a validation rule, the system shall reject the invalid values without altering previously stored valid records.
- `ROB-connectivity-loss`: If network connectivity is lost during data entry, the system shall preserve entered values until the user restores connectivity, cancels the operation, or closes the interface. Whether the values must survive closing and reopening the interface remains a team decision.
- `ROB-external-service-failure`: If an external authentication, notification, or mapping service is unavailable, the system shall identify the affected function and shall not present the operation as successful.
- `ROB-backup-recovery`: The system shall back up persistent records and support restoration within team-approved recovery-point, recovery-time, and retention limits.

### 9.7 Scalability

_[Add the ones that apply, with `SCA-`, `INT-`, and `MNT-` identifiers. Maintainability is the one this course cares about most, because someone inherits your code in January.]_

- `SCA-event-capacity`: The system shall support at least eight active locations, 200 volunteers, 3,000 shoppers, and 10,000 item-activity records in one event without violating the performance requirements in section 9.2.
- `SCA-growth-capacity`: The system shall support the team-approved growth margin above the event capacity without requiring redesign of its data model or deployment architecture.

### 9.8 Interoperability

- `INT-data-export`: The system shall allow an authorized administrator to export volunteer, donation, shopping, and summary data in CSV format for use in spreadsheet software.
- `INT-export-documentation`: Each export shall contain documented column names, date formats, units, and record identifiers.
- `INT-existing-tools`: For each approved integration with SignUpGenius, Google Forms, Google Sheets, the ReFrog website, a mapping service, email, or a TCU identity service, the interface specification shall identify the exchanged data, direction, format, and failure behavior.
- `INT-integration-scope`: Each existing service that is not selected for integration shall be documented as replaced, retained as a manual process, or outside the release scope rather than assumed to be connected.

### 9.9 Maintainability

- `MNT-event-configuration`: The system shall allow authorized administrators to change event dates, operating hours, locations, shifts, and staffing requirements without modifying or redeploying application code.
- `MNT-automated-tests`: The project shall include automated tests for authentication, authorization, volunteer scheduling, validation, and data-calculation rules.
- `MNT-deployment-documentation`: The project shall provide instructions for installing, configuring, deploying, backing up, restoring, and updating the application.
- `MNT-administrator-documentation`: The project shall provide instructions for routine administrator tasks and event preparation.
- `MNT-diagnostics`: The system shall record sufficient diagnostic information to identify failed requests and external-service failures without recording authentication secrets or unnecessary sensitive information.
- `MNT-dependency-record`: The project shall document its runtime, external services, required service accounts, and third-party dependencies.
- `MNT-operational-handoff`: Before production handoff, the project shall provide the designated maintenance owner with the source code, deployment instructions, administrator instructions, service-account ownership information, and recurring-cost information. The maintenance owner, hosting payer, service-account owner, and supported technology stack remain unresolved under `AS-operational-ownership`.

---

## 10. Internationalization and Localization

_[Languages, character sets, time zones, date and currency formats. If the answer is a single locale, say so and say why, because that is a real constraint on who can use the system.]_

The initial release is intended for ReFrog's TCU-based operation in Fort Worth, Texas. The following requirements define a single-locale release while preventing avoidable loss or ambiguity in stored data. Additional language support remains outside the initial scope unless the team and client decide otherwise.

- `LOC-release-locale`: The system shall provide the initial-release interface and documentation in United States English (`en-US`).
- `LOC-unicode`: The system shall store, process, export, and display user-entered text using Unicode without removing valid characters from names or other text fields.
- `LOC-event-time-zone`: The system shall associate ReFrog event dates and times with the `America/Chicago` time zone and shall account for daylight-saving-time changes.
- `LOC-time-storage`: The system shall store timestamps in a consistent machine-readable form and convert them to the event's configured time zone for display.
- `LOC-date-display`: The system shall display human-readable dates with the month written or abbreviated, such as `May 8, 2027`, when a numeric date could be ambiguous.
- `LOC-time-display`: The system shall display user-facing event times using a 12-hour clock with `a.m.` or `p.m.` and the event's configured time zone.
- `LOC-export-format`: Machine-readable exports shall use an unambiguous date and time representation documented with the export.
- `LOC-measurement-units`: Reports containing weight or volume shall identify their units explicitly and shall use the measurement units approved by ReFrog.
- `LOC-currency-not-applicable`: Currency localization is not applicable to the initial release because ReFrog does not charge shoppers for donated items, consistent with `BR-free-shopping`.
- `LOC-future-languages`: Translation and runtime locale switching are outside the initial release unless later added to scope. The system shall not embed interface text in stored business records where doing so would prevent future localization.

---

## 11. Other Requirements

_[Anything real that fits nowhere above: legal, licensing, installation, training, documentation. Delete this section if it is empty rather than leaving it as a placeholder.]_

---

## Working this document with your agent

_[Delegate: converting prose requirements into EARS shapes; checking that every `UC-*`, `BR-*`, and `FEAT-*` cited here exists in the document that owns it; finding functional requirements that appear in several use cases and should be lifted into section 5.2; drafting an oracle for a quality attribute you have stated only as an adjective._

_Keep human: the numbers. Every threshold in section 9 is a commitment somebody has to live with, and an agent will supply a plausible one (99.9% uptime, 200ms response) that nobody asked for and no one can meet. A number in this document either came from your client, from a measurement, or from a decision your team made deliberately and can defend._

_**The specific failure to watch for: invented precision.** A generated specification reads as authoritative at exactly the points where it is guessing. Check every number, every browser version, every retention period against something real, and put the ones you cannot verify in [OPEN-ISSUES.md](OPEN-ISSUES.md) instead of leaving a confident guess in the document your team will build from.]_
