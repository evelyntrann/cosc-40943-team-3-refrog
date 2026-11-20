# Calibration

Checkpoint 3 in-person guidance on 2026-11-20 permits the team to use estimated actual hours. This table therefore labels them **Estimated actual** and calculates the corresponding ratios. One row belongs to each estimate; ratios are estimated actual divided by the estimate midpoint.

## Estimation method

The team-supplied **Use Case Work Allocation** planning guide assigns relative effort points:

| Points | Guide meaning |
|---:|---|
| 8 | High-risk security, concurrency, reporting, or cross-module work |
| 5 | Normal full-stack workflow with business rules and persistence |
| 3–4 | Smaller workflow reusing established infrastructure |
| 2 | Primarily informational UI or documentation-driven content |

Two use cases have both guide points and recorded estimate midpoints:

- `UC-INF-view-event-info`: 2 points and a 5.5-hour refined midpoint.
- `UC-VOL-signup`: 8 points and a 64-hour coarse midpoint.

The estimated-hour model is the straight line between those anchors:

`estimated hours = 5.5 + 9.75 × (points - 2)`

| Points | Estimated hours |
|---:|---:|
| 2 | 5.50 |
| 3 | 15.25 |
| 4 | 25.00 |
| 5 | 34.75 |
| 7 | 54.25 |
| 8 | 64.00 |

`UC-AUTH-register-account` is not pointed in the guide because it was already underway. It is assigned 8 points for calibration because it is a high-risk security and cross-module workflow under the guide's definition.

## Completed and reported-complete use cases

| Use case | Owner | Estimate source | Estimate midpoint (h) | Estimated actual (h) | Ratio | Largest observed miss |
|---|---|---|---:|---:|---:|---|
| `UC-AUTH-register-account` | Shared AUTH work | 8-point model | 64.00 | 64.00 | 1.0 | Concurrency, resend timing, typed-email handling, and delivery failure required review corrections beyond the initial implementation. |
| `UC-AUTH-sign-in` | Alexa | 5-point model from allocation guide | 34.75 | 34.75 | 1.0 | Server-side unavailable-state, sliding-renewal, and complete protected-session assertions were not all covered by the first verification claim. |
| `UC-AUTH-recover-account` | Alexa | 8-point model from allocation guide | 64.00 | 64.00 | 1.0 | The accepted-email side effect can precede failed history persistence; the missing correction and regression are tracked in #143. |
| `UC-ADM-view-dashboard` | Evelyn | 7-point model from allocation guide | 54.25 | 54.25 | 1.0 | The work depended on the still-open shift-management branch and remained outside the audited `main` revision. |
| `UC-INF-view-event-info` | Evelyn | 2026-11-13 refined estimate ([#99](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/99)) | 5.50 | 5.50 | 1.0 | Rendered page output and approved off-season wording were not fully covered by the first verification claim. |
| `UC-VOL-signup` | Ethan | 2026-10-23 coarse estimate ([#59](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/59)) | 64.00 | 64.00 | 1.0 | Antiforgery, unknown-outcome reconciliation, concurrency, and failure-path assertions remained after the initial passing suite. |
| **Team total** | | | **286.50** | **286.50** | **1.0** | The point model supplies four baselines; the two recorded issue estimates independently land on the same model anchors. |

The allocation guide reports dashboard work as done; repository evidence qualifies it as pending outside the audited `main` revision. It remains in the estimated-hours table because this table records the team's work-allocation status, while traceability remains authoritative for Build and Verify status.

## Existing estimates for unfinished work

| Use case | Owner | Estimate | Made/source | Points | Range (h) | Midpoint (h) | Status |
|---|---|---|---|---:|---:|---:|---|
| `UC-IDV-verify-affiliation` | Alfonso | Coarse | 2026-10-23 ([#60](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/60)) | 7 | 29–50 | 39.5 | Not closed |
| `UC-IDV-verify-affiliation` | Alfonso | Refined | Missing | 7 | — | — | Not closed |
| `UC-VOL-view-schedule` | Ethan | Coarse | 2026-10-26 ([#62](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/62)) | 4 | 9–16 | 12.5 | Not closed |
| `UC-ADM-manage-shifts` | Evelyn | Refined first increment only | 2026-11-16 ([#111](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/111)) | 8 | 13–18 | 15.5 | Full use case not closed |
| `UC-VOL-request-access` | Spencer | Point model | Package C allocation | 3 | — | 15.25 | Not closed |
| `UC-ADM-review-volunteer-request` | Spencer | Point model | Package C allocation | 5 | — | 34.75 | Not closed |
| `UC-ADM-grant-volunteer-access` | Spencer | Point model | Package C allocation | 4 | — | 25.00 | Not closed |
| `UC-ADM-remove-volunteer-access` | Spencer | Point model | Package C allocation | 4 | — | 25.00 | Not closed |
| **Spencer assigned total** | **Spencer** | | | **16** | | **100.00** | **Assigned, unfinished** |

The known issue ranges comprise:

- **#60:** clarify and design 3–5, build and review 20–35, verify 6–10.
- **#59:** specify 6–10, build and review 30–50, verify 12–20.
- **#62:** clarify and design 1–2, build and review 6–10, verify 2–4.
- **#99:** build and review 3–5, verify 1–2.
- **#111:** build and review 10–14, verify 3–4 for the first shift-management increment.

## Ratios by person and team

| Person or team | Completed midpoint (h) | Estimated actual (h) | Ratio | Completed work | Assigned unfinished forecast (h) |
|---|---:|---:|---:|---|---:|
| Alexa | 98.75 | 98.75 | 1.0 | Sign-in and recovery | 11.00 |
| Evelyn | 59.75 | 59.75 | 1.0 | Dashboard and event information | 64.00 |
| Spencer | 0.00 | 0.00 | — | None closed | 100.00 |
| Alfonso | 0.00 | 0.00 | — | None closed | 118.25 |
| Ethan | 64.00 | 64.00 | 1.0 | Volunteer sign-up | 50.00 |
| Shared AUTH work | 64.00 | 64.00 | 1.0 | Registration | 0.00 |
| **Team** | **286.50** | **286.50** | **1.0** | All completed rows | **343.25** |

## MVP forecast

The allocation guide excludes Package F from the MVP. After removing the use cases it reports complete, Packages A–E contain 51 remaining points:

| Remaining package work | Owner | Use cases | Points | Estimated midpoint (h) | Calibrated forecast at team ratio 1.0 (h) |
|---|---|---|---:|---:|---:|
| A | Alexa | `UC-INF-view-install-guide`, `UC-INF-view-role-guides` | 4 | 11.00 | 11.00 |
| B | Evelyn | `UC-ADM-manage-shifts` | 8 | 64.00 | 64.00 |
| C | Spencer | `UC-VOL-request-access`, `UC-ADM-review-volunteer-request`, `UC-ADM-grant-volunteer-access`, `UC-ADM-remove-volunteer-access` | 16 | 100.00 | 100.00 |
| D | Alfonso | `UC-IDV-verify-affiliation`, `UC-SHP-log-item-taken` | 15 | 118.25 | 118.25 |
| E | Ethan | `UC-VOL-cancel-shift`, `UC-VOL-view-schedule` | 8 | 50.00 | 50.00 |
| **Total** | | | **51** | **343.25** | **343.25** |

The course workload expectation is 6–9 hours per member per week. With six team members and two weeks from Checkpoint 3 to Team 3's December 4 MVP demonstration, estimated capacity is:

- Lower capacity: `6 members × 2 weeks × 6 hours = 72 hours`.
- Upper capacity: `6 members × 2 weeks × 9 hours = 108 hours`.
- Forecast gap: `343.25 - 108 = 235.25 hours` at the optimistic capacity, or `343.25 - 72 = 271.25 hours` at the lower capacity.

The forecast does not fit the available capacity. It also excludes the unpointed legacy `UC-ADM-review-volunteer` work and counts the full eight points for shift management even though its first increment is merged. The team must reconcile these estimates with current progress and move enough rows out of `v1.0.0`, or explicitly revise their scope with the client, before treating the release plan as achievable.
