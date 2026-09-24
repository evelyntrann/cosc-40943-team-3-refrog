# SRS Quality and Localization Decision Checklist

**Project:** ReFrog  
**Sections:** SRS Section 9 — Quality Attributes; Section 10 — Internationalization and Localization  
**Purpose:** Record the values and policy decisions that the team must approve before the proposed requirements are added to the Software Requirements Specification.

> This checklist contains proposed values and unresolved choices. Nothing in this document is an approved software requirement until the team records its decision.

## Meeting Priorities

The decisions most likely to affect the system architecture should be discussed first:

1. Authentication and TCU-affiliation verification
2. Application platform and external-service integrations
3. Availability, outage recovery, and backup expectations
4. Peak capacity and anticipated growth
5. Accessibility standard
6. Data and audit-log retention
7. Hosting and long-term maintenance ownership

## Numeric and Standards-Based Values

| Identifier | Value requiring approval | Proposed value | Team decision |
|---|---|---|---|
| `USE-mobile-responsive` | Minimum supported viewport width | 320 CSS pixels | |
| `USE-donation-completion` | Successful completion rate | 90% of representative first-time users | |
| `USE-donation-completion` | Maximum completion time | 60 seconds | |
| `USE-volunteer-signup-completion` | Successful completion rate | 90% of representative first-time volunteers | |
| `USE-volunteer-signup-completion` | Maximum completion time | 2 minutes | |
| `USE-accessibility` | Accessibility standard | WCAG 2.1 Level AA | |
| `PER-page-load` | Acceptable page-load time | 3 seconds at the 95th percentile | |
| `PER-page-load` | Test-network conditions | TBD — for example, campus Wi-Fi or a defined mobile connection | |
| `PER-form-submission` | Submission response time | 2 seconds at the 95th percentile | |
| `PER-dashboard-load` | Dashboard load time | 5 seconds | |
| `SEC-session-expiration` | Administrator inactivity timeout | TBD | |
| `AVL-uptime` | Availability target | 99% during recruitment and event periods | |
| `AVL-outage-recovery` | Maximum recovery time | 30 minutes | |
| `ROB-backup-recovery` | Backup frequency | TBD | |
| `ROB-backup-recovery` | Recovery-point objective | TBD — maximum acceptable data loss | |
| `ROB-backup-recovery` | Recovery-time objective | TBD — maximum restoration time | |
| `ROB-backup-recovery` | Backup retention period | TBD | |
| `SCA-event-capacity` | Active locations | 8 | |
| `SCA-event-capacity` | Volunteers per event | 200 | |
| `SCA-event-capacity` | Shoppers per event | 3,000 | |
| `SCA-event-capacity` | Item-activity records per event | 10,000 | |
| `SCA-growth-capacity` | Capacity margin above present demand | TBD | |
| `LOC-release-locale` | Initial locale | United States English (`en-US`) | |
| `LOC-event-time-zone` | Event time zone | `America/Chicago` | |
| `LOC-date-display` | Human-readable date format | Example: `May 8, 2027` | |
| `LOC-time-display` | Time format | 12-hour clock with a.m./p.m. | |
| `LOC-measurement-units` | Weight and volume units | TBD — likely pounds, U.S. tons, and cubic yards | |

## Policy and Scope Decisions

| Identifier | Decision needed | Team decision |
|---|---|---|
| `USE-donation-completion` | Define how many representative users must participate in the usability test. | |
| `USE-volunteer-signup-completion` | Define the volunteer test population and whether prior ReFrog experience is allowed. | |
| `PER-page-load` | Define the peak-load workload and network conditions used for testing. | |
| `PER-dashboard-load` | Define the maximum record count used in the dashboard test. | |
| `SEC-authentication-provider` | Choose TCU single sign-on, TCU email verification, another electronic method, or continued visual ID verification. | |
| `SEC-audit-log` | Decide which events are logged, who may review them, and how long the logs are retained. | |
| `SAF-not-applicable` | Confirm that the application is not safety-critical and will not provide or control physical safety procedures. | |
| `AVL-event-hours` | Define the critical availability periods: recruitment dates, finals-week dates, and daily operating hours. | |
| `AVL-maintenance-window` | Decide when planned maintenance is permitted. | |
| `AVL-fallback-information` | Choose the required fallback format: printable report, downloadable file, offline access, or another method. | |
| `ROB-connectivity-loss` | Decide whether entered data must survive only a temporary disconnect or also closing and reopening the application. | |
| `SCA-growth-capacity` | Decide whether to support only current scale or a growth target such as 25%, 50%, or twice current demand. | |
| `INT-data-export` | Confirm that CSV export is required and identify which datasets may be exported. | |
| `INT-existing-tools` | Decide whether SignUpGenius, Google Forms, Google Sheets, maps, email, and the ReFrog website will be integrated, replaced, or retained separately. | |
| `MNT-operational-handoff` | Identify the person or organization responsible for maintenance after the team graduates. | |
| `MNT-dependency-record` | Select or approve the supported technology stack and hosting environment. | |
| `MNT-operational-handoff` | Determine who owns service accounts and pays recurring hosting or service costs. | |
| `LOC-release-locale` | Confirm that English is the only language supported in the initial release. | |
| `LOC-currency-not-applicable` | Confirm that currency handling is unnecessary because all ReFrog shopping remains free. | |
| `LOC-future-languages` | Decide whether the system must support future translation or whether localization is entirely out of scope. | |

## Decisions Requiring Client or TCU Input

The team may not be able to approve the following items without external confirmation:

- `SEC-authentication-provider`: ReFrog and potentially TCU IT must approve the method used to establish TCU affiliation.
- `AVL-event-hours`: ReFrog organizers must confirm the recruitment period and event operating hours.
- `INT-existing-tools`: ReFrog must decide which existing tools it intends to retain or replace; integration may require service-account access.
- `MNT-operational-handoff`: ReFrog or TCU must identify the long-term maintenance owner, account owner, and hosting payer.
- `LOC-measurement-units`: ReFrog must confirm the units used in official impact reporting.

## Meeting Record

**Meeting date:** ____________________  
**Attendees:** ________________________________________________________________  
**Decisions recorded by:** ____________________

### Follow-up Items

| Item | Owner | Due date |
|---|---|---|
| | | |
| | | |
| | | |

## After Approval

After the meeting:

1. Replace each blank **Team decision** cell with the approved value or disposition.
2. Record unanswered client questions in `OPEN-ISSUES.md` using unique identifiers.
3. Insert approved requirements into Sections 9 and 10 of `software-requirements-specification.md`.
4. Ensure every inserted requirement has a measurable verification method.
5. Review the final wording with the client before treating the thresholds as commitments.
