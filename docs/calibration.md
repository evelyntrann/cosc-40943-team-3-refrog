# Calibration

One row per **finished** use case: its estimates, the actual hours from the weekly activity reports, a ratio per estimate, and one sentence on the biggest miss (course Implementation module §4.4). This is graded on completeness and honesty, never on accuracy.

## How to fill in a row

1. **Estimate:** copy the range from the use case's issue. Use the refined estimate if the owner wrote one before building; otherwise use the coarse estimate from planning poker.
2. **Actual:** add up the hours logged against the use case in the weekly activity reports. Each report line starts with the use case ID and uses one of these categories:
   - Clarify: REQUIREMENTS, COMMUNICATION
   - Design: DESIGN
   - Build and review: DEVELOPMENT, REVIEW
   - Verify: TESTING, BUGFIX
3. **Ratio:** actual hours ÷ the estimate's midpoint. For example, an estimate of 4–8 h has a midpoint of 6 h, so 9 h actual gives a ratio of 1.5.
4. **Biggest miss:** hold a five-minute retrospective the day the use case closes and write one sentence about what took longer, or shorter, than expected, and why.

## Table

| Use case | Part | Estimate (h) | Midpoint (h) | Actual (h) | Ratio |
|---|---|---|---|---|---|
| `UC-IDV-verify-affiliation` (#60) | Clarify and design | 3–5 | 4 | _at close_ | _at close_ |
| | Build and review | 20–35 | 27.5 | _at close_ | _at close_ |
| | Verify | 6–10 | 8 | _at close_ | _at close_ |
| | **Total** | 29–50 | 39.5 | _at close_ | _at close_ |

**Biggest miss, `UC-IDV-verify-affiliation`:** _written at the retrospective when the slice closes._

The #60 estimate combines clarify and design into one part. Later use cases use the four separate parts from planning poker.

## Forecast

_Written once the first ratio exists (course Traceability module §4.7). Format: "Forecast: N rows left in v1.0.0, X to Y h at ratio R; about Z h available."_
