# Availability SLO for `AVL-uptime`

**Current value: 100 percent (20 of 20 probes), measured locally. This is not production availability, because ReFrog is not deployed.**

The objective itself is written under [`AVL-uptime`](../requirements/software-requirements-specification.md#95-availability). This page records how it was measured.

| Part | Value |
|---|---|
| SLI | Good probes ÷ all attempted probes of `GET /api/v1/info/event`, one per minute |
| Good probe | `200`, `application/json`, a known event status, and no error, within three seconds |
| Window | The volunteer-recruitment and finals-week periods, excluding approved maintenance. Exact dates are open under `OI-33` |
| Target | At least 99 percent |
| Error budget | 1 percent of probes, about 14 minutes per day of the window |

The one-minute cadence and three-second deadline are team assumptions, recorded in `OI-33`.

## Current measurement

| Item | Recorded |
|---|---|
| Sample | 9:12 a.m. to 9:31 a.m., November 18, 2026 (America/Chicago) |
| Probes | 20 attempted of 20 scheduled |
| Good / bad | 20 / 0 |
| SLI | 100.00 percent |
| Error budget for this sample | 0.2 probes allowed, 0 spent. One bad probe would have missed the target |
| System measured | `v0.2.0` in the Production environment, on one development machine, with a disposable SQL Server 2022 database |
| Raw data | [availability-probes.csv](slo-evidence/availability-probes.csv) |

## The probe can fail

Before the sample, we broke the system on purpose and sent three probes per case. The raw results are in [probe-can-fail.csv](slo-evidence/probe-can-fail.csv). These probes are not counted in the SLI.

| Situation | Result | Reason recorded |
|---|---|---|
| Healthy | 3 good | `ok` |
| Database stopped | 3 bad | `deadline-exceeded` |
| Database restored | 3 good | `ok` |
| Application stopped | 3 bad | `no-response` |
| Application restored | 3 good | `ok` |
| Wrong page that answers `200` with HTML | 3 bad | `not-json` |
| Unknown API path | 3 bad | `status-404` |

The wrong-page case matters most. A probe that checks only the status code would have counted that page as up.

## What this number misses

- **Production:** the probe and the application ran on the same machine, so it cannot see network, DNS, TLS, or hosting failures.
- **The required window:** the sample is 20 minutes outside the recruitment and finals-week periods, so it does not show that `AVL-uptime` is met.
- **Short outages:** an outage between two probes is not seen.
- **Other workflows:** the probe reads event information only. It does not cover sign-in, registration, shift creation, email, or page rendering.
- **Real data and load:** the database had no active event, so every good probe was the off-season answer, with no real users or peak load.

## Measure it again

Start the application, then run this from the repository root. Point `PROBE_URL` at the deployed site once one exists.

```sh
export PROBE_URL=http://127.0.0.1:18080/api/v1/info/event
export PROBE_OUTPUT=docs/operations/slo-evidence/availability-probes.csv
PROBE_COUNT=20 scripts/availability-slo.sh probe
scripts/availability-slo.sh report
```
