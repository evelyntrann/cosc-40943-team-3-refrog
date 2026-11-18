# Calibration

One row per **estimate**, so a finished use case usually has a coarse row and a refined row (course Implementation module §4.4). The table is graded on completeness and honesty, not on accuracy. Do not fill in Actual or Ratio before the use case closes. **Missing** means the source has not been supplied; it never means zero hours.

Checkpoint 3 evidence checked on **2026-11-18**, tracked in [#115](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/115). Sources are [traceability.md §4](traceability.md#4-use-case-to-design-code-and-tests), merged implementation pull requests, the estimate issues linked below, and the team planning guide supplied on 2026-11-18. The guide's completion claims are distinguished below from merged implementation evidence. Actual human effort and retrospective statements cannot be inferred from commit timestamps, issue duration, or story points.

## How to fill in a row

1. **Add the row when the estimate is made.** Copy the total range from the use case's issue (the sum of its parts), compute the midpoint, and record the date the estimate was posted along with the issue number.
   - **Coarse:** from planning poker, made after only reading the use case.
   - **Refined:** written by the owner on the issue, under the coarse one, before building.
2. **Actual, when the use case closes:** link the dated weekly activity report lines tagged with this use case's ID and add up only the hours **logged on or after the row's Made date**. Record the contributor on each line so person and team totals can be checked. A coarse row and a refined row for the same use case can therefore have different Actuals.
3. **Ratio:** Actual ÷ Midpoint, to one decimal place. A ratio of 1.5 means the work took half again as long as estimated. Keep coarse and refined ratios separate; do not count the same work twice in a team total.
4. **Biggest miss:** hold the five-minute retrospective **the day the use case closes**, before starting the next one. The owner answers three questions: which part missed most, what you did not know when you estimated, and what you will do differently next time. Write the answer as one sentence on the use case's coarse row that same day.

## Table

| Use case | Estimate | Made | Range (h) | Midpoint (h) | Actual (h) | Ratio | Biggest miss |
|---|---|---|---|---|---|---|---|
| `UC-IDV-verify-affiliation` | Coarse | 2026-10-23 ([#60](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/60)) | 29–50 | 39.5 | Not closed | Not calculated | Not closed |
| `UC-IDV-verify-affiliation` | Refined | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-VOL-signup` | Coarse | 2026-10-23 ([#59](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/59)) | 48–80 | 64 | Not closed | Not calculated | Not closed |
| `UC-VOL-view-schedule` | Coarse | 2026-10-26 ([#62](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/62)) | 9–16 | 12.5 | Not closed | Not calculated | Not closed |
| `UC-INF-view-event-info` | Coarse | Missing original estimate | Missing | Missing | Missing report lines | Not calculated | Missing owner retrospective |
| `UC-INF-view-event-info` | Refined, build and verify | 2026-11-13 ([#99](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/99)) | 4–7 | 5.5 | Missing report lines | Not calculated | See coarse row |
| `UC-AUTH-register-account` | Missing original estimate | Missing | Missing | Missing | Pending use-case close and report lines | Not calculated | Pending use-case close |
| `UC-AUTH-sign-in` | Missing original estimate | Missing | Missing | Missing | Missing report lines | Not calculated | Missing owner retrospective |
| `UC-ADM-manage-shifts` | Refined, first build increment only | 2026-11-16 ([#111](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/111)) | 13–18 | 15.5 | Not closed | Not calculated | Not closed |
| `UC-AUTH-recover-account` | Missing original estimate | Missing | Missing | Missing | Missing report lines; team reports done | Not calculated | Missing owner retrospective |
| `UC-VOL-request-access` | Missing coarse estimate ([#82](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/82)) | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-ADM-review-volunteer-request` | Missing coarse estimate ([#83](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/83)) | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-ADM-grant-volunteer-access` | Missing coarse estimate ([#84](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/84)) | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-ADM-remove-volunteer-access` | Missing coarse estimate ([#85](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/85)) | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-ADM-view-dashboard` | Missing original estimate | Missing | Missing | Missing | Missing report lines; team reports done | Not calculated | Missing owner retrospective |
| `UC-ADM-notify-volunteers` | Missing coarse estimate | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-INF-view-locations` | Missing coarse estimate | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-INF-view-role-guides` | Missing coarse estimate | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-INF-view-install-guide` | Missing coarse estimate | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-SHP-log-item-taken` | Missing coarse estimate | Missing | Missing | Missing | Not closed | Not calculated | Not closed |
| `UC-VOL-cancel-shift` | Missing coarse estimate | Missing | Missing | Missing | Not closed | Not calculated | Not closed |

How the ranges were added up from the issues:

- **#60:** clarify and design 3–5, build and review 20–35, verify 6–10.
- **#59:** specify 6–10, build and review 30–50, verify 12–20.
- **#62:** clarify and design 1–2, build and review 6–10, verify 2–4.
- **#99:** build and review 3–5, verify 1–2; total 4–7, midpoint `(4 + 7) / 2 = 5.5`. The issue records the owner's pre-build decisions on 2026-11-13 and was opened that day. This is a build estimate, not a replacement for a missing original coarse estimate.
- **#111:** build and review 10–14, verify 3–4; total 13–18, midpoint `(13 + 18) / 2 = 15.5`. This covers the first increment only; [PR #114](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/114) explicitly leaves extension 6a for a later build, so closing the issue does not close the whole use case.

Source gaps checked on 2026-11-18:

- [#79](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/79) has one shared AUTH design estimate and no per-use-case coarse-estimate comment. It cannot be split among the three use cases after the fact. [#107](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/107), the sign-in build context, has no estimate or assignee. [PR #109](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/109) implements sign-in while its traceability row still says Designed. The team reports it done; its actuals still need report sources.
- `UC-INF-view-event-info` is Built and Verified in traceability and shipped in [PR #102](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/102). Its [#99](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/99) assignee is `@evelyntrann`; no coarse estimate, UC-tagged report lines, or owner retrospective was linked there. Supply the original sources; a new estimate must retain its new date and cannot be presented as the original.
- #82–#85 still contain planning-poker placeholders and no estimate comments. The remaining missing MVP estimates are named in the forecast below.
- No weekly activity report links were found in these source issues or in the repository. Report links, dated UC-tagged hours, and the owners' biggest-miss sentences remain required inputs; no actuals have been reconstructed from Git history.

## Team planning guide supplied 2026-11-18

This guide supplies owners, relative story points, completion claims, and intended MVP scope. **Points are not hours** and do not supply an hour range, midpoint, actual, or calibration ratio. No points-to-hours conversion has been made.

| Owner from guide | Use case | Points | Team-reported status or scope |
|---|---|---|---|
| Alexa | `UC-AUTH-sign-in` | 5 | Done; merged implementation in PR #109 |
| Alexa | `UC-AUTH-recover-account` | 8 | Done; implementation/verification source still needed |
| Alexa | `UC-INF-view-install-guide` | 2 | MVP |
| Alexa | `UC-INF-view-role-guides` | 2 | MVP |
| Evelyn | `UC-ADM-manage-shifts` | 8 | Waiting on VOL; first increment merged in PR #114 |
| Evelyn | `UC-ADM-view-dashboard` | 7 | Done; implementation/verification source still needed |
| Evelyn | `UC-INF-view-event-info` | 2 | Done; merged implementation in PR #102 |
| Spencer | `UC-VOL-request-access` | 3 | MVP |
| Spencer | `UC-ADM-review-volunteer-request` | 5 | MVP |
| Spencer | `UC-ADM-grant-volunteer-access` | 4 | MVP |
| Spencer | `UC-ADM-remove-volunteer-access` | 4 | MVP |
| Alfonso | `UC-IDV-verify-affiliation` | 7 | MVP |
| Alfonso | `UC-SHP-log-item-taken` | 8 | MVP |
| Ethan | `UC-VOL-signup` | 8 | MVP |
| Ethan | `UC-VOL-cancel-shift` | 4 | MVP |
| Ethan | `UC-VOL-view-schedule` | 4 | MVP |
| Unassigned (Package F) | `UC-DON-log-donation` | 5 | Explicitly outside MVP |
| Unassigned (Package F) | `UC-ABU-review-alert` | 5 | Explicitly outside MVP |
| Unassigned (Package F) | `UC-ADM-notify-volunteers` | 3 | Explicitly outside MVP |
| Unassigned (Package F) | `UC-INF-view-locations` | 2 | Explicitly outside MVP |

`UC-AUTH-register-account` and the legacy `UC-ADM-review-volunteer` are reported underway; the guide supplies neither their owner nor an hour estimate. Confirm their scope alongside the guide's access-request use cases. The guide identifies four finished use cases to calibrate: sign-in, recovery, dashboard, and event info. Recovery and dashboard still require source links for their implementation and verification; their reported completion is not a claim that tests passed. Each of the four needs its original dated estimate, dated UC-tagged activity report lines, and owner-written biggest-miss sentence.

## Ratios by person and team

Members follow [team-contract.md](team-contract.md#team-contract-refrog-team-3). Source report lines must identify who did the work; an assignee is not evidence of hours. Calculate coarse and refined totals separately from sourced, finished-use-case rows. For shared work, keep each contributor's hours visible and include each estimate once in the team denominator.

| Person or team | Coarse ratio | Refined ratio | Missing evidence |
|---|---|---|---|
| `@evelyntrann` | Not calculated | Not calculated | Original estimates, UC-tagged reports, and retrospective; INF refined estimate is sourced above |
| `@a1kj` | Not calculated | Not calculated | Finished-use-case attribution, estimates, and UC-tagged reports |
| `@amercado9` | Not calculated | Not calculated | Original sign-in/recovery estimates, UC-tagged reports, and retrospectives |
| `@Alfonsotomax` | Not calculated | Not calculated | Finished-use-case attribution, estimates, and UC-tagged reports |
| `@SpencerScherger` | Not calculated | Not calculated | Finished-use-case attribution, estimates, and UC-tagged reports |
| `@remag3` | Not calculated | Not calculated | Finished-use-case attribution, estimates, and UC-tagged reports |
| Team | Not calculated | Not calculated | Complete finished-use-case estimates and dated, attributed UC-tagged report lines |

The team ratio is `sum(actual hours) / sum(estimate midpoints)` for the same completed estimate rows. The forecast uses the coarse ratio. A missing denominator or source is **not calculated**, not a ratio of 0 or an assumed ratio of 1. Person ratios also require the original estimate attributable to that person's work; do not divide a person's hours by an entire shared-use-case estimate.

## Forecast

Use every unfinished `v1.0.0` row in [traceability.md §4](traceability.md#4-use-case-to-design-code-and-tests). Multiply the sum of their estimate **midpoints** by the team's coarse ratio, then compare the result with the hours the team will really have before the MVP demo. Exclude finished work. Where an FR's work is already covered by a use-case estimate, link that coverage instead of counting the same hours again; uncovered FR work needs its own estimate.

The 2026-11-18 guide and the published release rows currently disagree. Package F's `UC-ADM-notify-volunteers` and `UC-INF-view-locations` still say `v1.0.0` in traceability although the guide excludes them from MVP. The guide includes IDV and shopping, whose rows say earlier releases or Spring, and reports recovery/dashboard done while their rows still say Designed/Not started. Reconcile the release and completion evidence before presenting a final sum; no later release date is assigned here.

| Unfinished v1.0.0 use case | Midpoint (h) | Source or missing input |
|---|---|---|
| `UC-VOL-view-schedule` | 12.5 | [#62](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/62) |
| `UC-AUTH-recover-account` | Missing | Guide reports done; completion source and original estimate needed for calibration rather than a remaining-work forecast |
| `UC-VOL-request-access` | Missing | #82 planning-poker range and date |
| `UC-ADM-review-volunteer-request` | Missing | #83 planning-poker range and date |
| `UC-ADM-grant-volunteer-access` | Missing | #84 planning-poker range and date |
| `UC-ADM-remove-volunteer-access` | Missing | #85 planning-poker range and date |
| `UC-ADM-view-dashboard` | Missing | Guide reports done; completion source and original estimate needed for calibration rather than a remaining-work forecast |
| `UC-ADM-notify-volunteers` | Missing | Package F: guide excludes from MVP; published release row needs reconciliation |
| `UC-INF-view-locations` | Missing | Package F: guide excludes from MVP; published release row needs reconciliation |
| `UC-INF-view-role-guides` | Missing | Dated estimate and source |
| `UC-INF-view-install-guide` | Missing | Dated estimate and source |

The six unfinished FR rows also need explicit estimate coverage: `FR-AUTH-volunteer-role`, `FR-AUTH-volunteer-lifecycle`, `FR-AUTH-password-recovery`, `FR-NOTIFY-in-app-inbox`, `FR-NOTIFY-preserve-business-state`, and `FR-DISPLAY-unavailable-data`. None is silently assumed to cost zero.

Other unfinished use cases in the guide also need release reconciliation and hour estimates: `UC-IDV-verify-affiliation` has a recorded coarse midpoint of 39.5 h, `UC-VOL-signup` 64 h, and `UC-SHP-log-item-taken` and `UC-VOL-cancel-shift` have no sourced hour midpoint in this table. `UC-ADM-manage-shifts` needs its remaining increment estimated separately from the already-built #111 increment. Registration and the legacy volunteer-review use case need their owners and remaining scope confirmed. Story points do not fill these gaps.

**Forecast (2026-11-18): not calculated.** The published unfinished `v1.0.0` rows have only one sourced midpoint, **12.5 h**; the guide changes that inventory and does not supply hour estimates. Original hour ranges for the remaining work, FR estimate coverage, the team's coarse ratio, the MVP demo date, and each member's realistic remaining hours are missing. Therefore predicted hours, available capacity, and whether scope fits cannot yet be stated. Once sourced and reconciled, report `sum(unfinished MVP midpoints) × team ratio = predicted hours` against `sum(member remaining hours) = capacity`; if the work exceeds capacity, agree with the client which rows move out of `v1.0.0` and update the release plan.
