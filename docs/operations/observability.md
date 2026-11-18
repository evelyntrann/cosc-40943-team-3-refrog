# Operational evidence for Checkpoint 3

Issue [#117](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/117) prepares the design in [architecture §8.2.11](../design/architectural-design.md#82-other-concepts). The endpoints and structured events described there are proposed and await implementation after design merge. This document is a provider-neutral evidence procedure, not a record of a completed deployment.

## Current evidence

| Item | Recorded state |
|---|---|
| Approved production host | EAS, TCU-managed infrastructure (`OI-15`) |
| Running demo URL and environment | Not deployed, confirmed by the team on 2026-11-18; an EAS-approved demo environment remains to be supplied |
| Deployed tag/commit and deployment record | Not supplied |
| Platform log-stream location and access | Not supplied |
| Application readiness and metrics | Designed in #117; implementation/verification pending |
| Deliberate failure found in platform logs | Not performed |
| `AVL-uptime` measured value | Not measured; no external observations recorded |

Update this table with actual links and UTC observation times when evidence exists. Do not copy credentials, cookie values, connection strings, verification codes, participant emails, or raw provider exception messages into evidence. These facts cannot be inferred from a successful local run or a green PR workflow.

## Preparation and assumptions

Record the demo environment, deployed URL, deployed tag and full commit, deployment evidence link, platform log-stream link, and the person who can access it. Confirm the settings in `OI-33`: covered dates, approved maintenance intervals, observer location, equal probe cadence, and request deadline. The design makes no numeric choice for those unset settings. Keep notification routing/thresholds under `OI-23` and retention under `OI-6`.

Use a non-production deployment and synthetic records for fault demonstrations. Agree with the environment owner on a reversible database interruption and application stop, then restore both. Do not expose a public failure-injection endpoint or change production records to produce evidence.

## Health, metrics, and platform logs

After the design is implemented and tested:

1. Request `GET /api/v1/health` from outside the application host. Record UTC time, HTTP status, expected healthy envelope, and server correlation ID. Confirm the response is uncached and contains no deployment or database details.
2. As an administrator, request `GET /api/v1/admin/operations/metrics`. Record its observation period and aggregate counts. Confirm an ordinary user and a signed-out browser cannot read it. These counters describe one process and reset on restart; they are not the `AVL-uptime` measurement source.
3. Make the agreed database readiness check fail. Record the external `503 HEALTH_UNAVAILABLE` response and correlation ID. Find the matching structured event in the hosting platform's log stream; record the stream link, UTC time, operation, status, safe failure code/type, and elapsed time. A terminal screenshot alone does not meet this check.
4. Restore the database and record a healthy observation. Stop the Application through the platform, confirm the external observer records failure even though the process cannot log it, then restart and record recovery.
5. Inspect the captured platform event and failure response for useful correlation/status/operation information and for the prohibited data above. Link the automated failure-path redaction test as separate evidence. Remove sensitive material before sharing a screenshot; do not paste an unreviewed platform export into the repository.

Keep the demonstration times, environment, deployed revision, request/result evidence, platform-log evidence, and recovery evidence together. This demonstrates a probe can fail; it does not prove long-term availability.

## Availability measurement

The measured requirement is [`AVL-uptime`](../requirements/software-requirements-specification.md#95-availability). Its window is the documented volunteer-recruitment period and finals-week event period, minus approved maintenance intervals; the target remains the requirement's 99 percent. Exact dates are pending `OI-33`.

Run the external observer at the confirmed equal cadence and deadline. It must validate the HTTP status and healthy JSON envelope, and treat connection errors, deadline expiry, other status codes, and malformed/unexpected responses as failed observations. Keep the source records outside process memory so an application restart cannot erase downtime evidence. Record UTC observation time, environment/deployed revision, good/bad result, safe failure class, and elapsed time. Do not retain response bodies containing unexpected sensitive data.

For a stated covered interval, report:

- **SLI:** `good observations / all attempted observations`; give both counts and the percentage.
- **Target:** 99 percent from `AVL-uptime`.
- **Error budget:** `0.01 × all attempted observations` bad observations in the same interval; report actual bad observations beside it.
- **Coverage:** actual start/end, cadence/deadline, excluded approved maintenance, missed observer coverage, source link, and deployed revision(s).
- **Limitations:** sampling misses outages between probes; observer failures can create gaps or false failures; readiness covers Application/database connectivity but not schema correctness, page rendering, every workflow, or email delivery.

With zero attempts, report **not measured**, never 100 percent. Do not hide missing observer coverage by treating it as healthy or claim the full requirement window from a shorter checkpoint demonstration. If the available record is shorter or outside the required periods, label it as a limited demonstration and state the gap. Do not remove failed observations unless their times fall within maintenance exclusions approved and recorded independently.

Current value remains **not measured** until timestamped deployed observations are available. Replace that statement with actual counts, interval, source link, and limitations; do not populate it with local tests or an invented percentage.
