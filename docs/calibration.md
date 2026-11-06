# Calibration

One row per **estimate**, so a finished use case usually has a coarse row and a refined row (course Implementation module §4.4). The table is graded on completeness and honesty, not on accuracy. Do not fill in Actual or Ratio before the use case closes.

## How to fill in a row

1. **Add the row when the estimate is made.** Copy the total range from the use case's issue (the sum of its parts), compute the midpoint, and record the date the estimate was posted along with the issue number.
   - **Coarse:** from planning poker, made after only reading the use case.
   - **Refined:** written by the owner on the issue, under the coarse one, before building.
2. **Actual, when the use case closes:** add up the hours on Project Pulse weekly activity report lines that start with this use case's ID and were **logged on or after the row's Made date**. A coarse row and a refined row for the same use case therefore have different Actuals.
3. **Ratio:** Actual ÷ Midpoint, to one decimal place. A ratio of 1.5 means the work took half again as long as estimated. Multiply both ends of your next estimate by it.
4. **Biggest miss:** hold the five-minute retrospective **the day the use case closes**, before starting the next one. The owner answers three questions: which part missed most, what you did not know when you estimated, and what you will do differently next time. Write the answer as one sentence on the use case's coarse row that same day.

## Table

| Use case | Estimate | Made | Range (h) | Midpoint (h) | Actual (h) | Ratio | Biggest miss |
|---|---|---|---|---|---|---|---|
| `UC-AUTH-register-account` | Coarse | — | — | — | — | — | — |
| `UC-AUTH-register-account` | Refined | — | — | — | — | — | — |
| `UC-IDV-verify-affiliation` | Coarse | 2026-10-23 (#60) | 29–50 | 39.5 | | | |
| `UC-IDV-verify-affiliation` | Refined | _not yet written on #60_ | | | | | |
| `UC-VOL-signup` | Coarse | 2026-10-23 (#59) | 48–80 | 64 | | | |
| `UC-VOL-view-schedule` | Coarse | 2026-10-26 (#62) | 9–16 | 12.5 | | | |
| `UC-AUTH-sign-in` | — | — | — | — | — | — | — |
| `UC-AUTH-recover-account` | — | — | — | — | — | — | — |
| `UC-VOL-request-access` | — | — | — | — | — | — | — |
| `UC-ADM-review-volunteer-request` | — | — | — | — | — | — | — |
| `UC-ADM-grant-volunteer-access` | — | — | — | — | — | — | — |
| `UC-ADM-remove-volunteer-access` | — | — | — | — | — | — | — |

How the ranges were added up from the issues:

- **#60:** clarify and design 3–5, build and review 20–35, verify 6–10.
- **#59:** specify 6–10, build and review 30–50, verify 12–20.
- **#62:** clarify and design 1–2, build and review 6–10, verify 2–4.

Rows marked — are waiting for an estimate. Fill them in once the range is on the issue:

- The AUTH use cases go on #79. The Estimate section in #79's body covers the design work for all three AUTH use cases together, not each use case, so it is not used here.
- `UC-VOL-request-access` is on #82, `UC-ADM-review-volunteer-request` on #83, `UC-ADM-grant-volunteer-access` on #84, and `UC-ADM-remove-volunteer-access` on #85.

## Status at Checkpoint 2 (2026-11-06)

- **No use case has finished,** so no row has an Actual, a Ratio, or a Biggest miss yet.
- **The slice, `UC-AUTH-register-account`, is being built in PR #92** without a coarse or refined estimate. When it closes, its Actual will still be recorded, and the retrospective should say that it had no estimate to compare against.
- **The slice changed from `UC-IDV-verify-affiliation` to `UC-AUTH-register-account`** ([traceability.md §4](traceability.md#4-use-case-to-design-code-and-tests)). The IDV rows stay because their estimates were made and will be calibrated when IDV closes.

## Ratios by person and team

_Filled in once rows have a Ratio. List each owner's coarse and refined ratios, taking the owner from the assignees on the use case's issue. The refined ratio shows how well we estimate a build. The coarse ratio shows how well we estimate a use case we have only read, and the release forecast uses it._

## Forecast

_Written once the first coarse ratio exists: multiply both ends of each remaining coarse estimate by the ratio and compare the total with the hours left before each release in [traceability.md §4](traceability.md#4-use-case-to-design-code-and-tests). If the forecast says a promised date will slip, tell the client at the next meeting._
