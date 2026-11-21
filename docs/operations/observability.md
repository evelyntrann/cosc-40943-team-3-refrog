# Observability verification runbook

**Current status:** design only, [issue #117](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/117). Health, metrics and diagnostic changes in [the design](../design/observability.md) are not implemented. The developer confirmed on 2026-11-20 that the immediate target is a local demo and the app is not deployed. The [availability sample](availability-slo.md) is local, not production availability.

This procedure supports `UC-INF-view-event-info`, `UC-AUTH-register-account`, `MNT-diagnostics`, `MNT-error-notification` and `AVL-uptime`. The local path needs the merged design and its implementation; only the later deployed-platform exercise needs EAS access.

The [§4.3 stack adaptation](../design/observability.md#observability-43-adaptation-to-refrog) distinguishes logging, endpoint inspection, collection, dashboards and traces. This demo uses a local metrics snapshot and the existing SLI report; Prometheus, Grafana, Zipkin and production telemetry collection are not configured.

## Local demo first

After the design merges and O1–O8 are implemented:

1. Use [Running locally](../../README.md#running-locally) with a disposable SQL Server database and synthetic accounts. Start the app using the existing HTTPS launch profile, which selects Development; the proposed metrics snapshot is available only in that environment and still requires current administrator access. Keep the application console visible to show the actual structured output; label it **local application console**, not platform-stream evidence.
2. From a second terminal, make cookie-free checks of the proposed endpoints. Use the trusted local HTTPS certificate and the configured launch address:

   ```sh
   DEMO_BASE_URL=https://localhost:7257
   curl --include "$DEMO_BASE_URL/health"
   curl --include "$DEMO_BASE_URL/api/v1/operations/metrics"
   curl --include "$DEMO_BASE_URL/api/v1/info/event"
   ```

   Show health's `200` JSON, metrics' anonymous `401`, and event information's normal response. These health/metrics commands are future demo steps, not working endpoints at this PR's source revision.
3. Sign in as a synthetic ordinary account and show the metrics route's `403` refusal. Then sign in as a synthetic local administrator using the existing UI. Request metrics in that authenticated browser, capture its safe aggregate JSON, send another public event-information request, and show the count/duration change. Do not copy cookies or passwords into evidence.
4. Stop only the disposable local test database, keep the app running, and make one cookie-free event-information request. Record its actual `500 UNEXPECTED_ERROR` and `X-Trace-Id`, then find that ID in the local JSON diagnostic/completion records. Wait for an actual application response; a client-side timeout alone does not demonstrate the diagnostic. Restore SQL immediately, including if the check fails, and show a successful cookie-free retry. `/health` should still report process liveness while SQL is unavailable.
5. Inspect the actual emitted records against the design's [logging and privacy contract](../design/observability.md#logging-and-privacy-contract); show safe exception/code-location details without email, token, password, code or connection details. Run and record O1–O8 assertions, including positive diagnostic and negative privacy checks.
6. Show the existing local [availability report](availability-slo.md#measure-it-again) with its sample window and limitations. Keep deliberate-failure probes separate from the normal sample and identify any new run separately.

Today the repository's Compose setup runs Mailpit, not the ReFrog application, so Mailpit's Docker logs are not ReFrog request diagnostics. If an application container is provided later, its Docker Desktop Logs view can demonstrate local host collection; it must still be labelled a local container demonstration. The course's deployed-platform-stream requirement remains deferred until deployment.

## Where telemetry goes

| Signal | Proposed application source | Collection/view | Evidence still needed |
|---|---|---|---|
| Logs | `ILogger<T>` → structured JSON console output with UTC timestamp and request trace ID | EAS platform captures the process console stream; operator searches its log viewer | Actual host/service, stream location or command, access role, collection configuration and matching deployed entry |
| Health | Public `GET /health`, process liveness only | Host probe or external HTTP request; inspect JSON, not status alone | Deployed response and host probe configuration, if a host probe is enabled |
| Metrics | Development-only, current-administrator `GET /api/v1/operations/metrics`; no snapshot handler in other environments | Local administrator reads process snapshot; collector/history is not configured | Local before/after snapshot and anonymous `401`; non-Development `404`, including for administrators |
| Availability SLI | Existing `GET /api/v1/info/event` external probe | Existing CSV/report procedure in [availability-slo.md](availability-slo.md) | Deployed URL, probe outside the host, actual monitoring window and samples (`OI-33`) |
| Error notifications | Not implemented; requires approved policy (`OI-23`) | Destination/channel not selected | Recipient, threshold, channel, escalation and actual delivery proof |

Do not call a developer terminal capture a platform stream. On a service-managed host, EAS might expose the service's journal; on a container host, its collected console logs; on IIS, an EAS-approved application-log collector. These are examples, not claims about the selected host. Ask EAS for the actual application-log source rather than treating a web-server access log as the application's diagnostic event.

## Host setup and normal checks

1. Record the deployed commit, environment, instance identity, public base URL, and exact platform log-view location. Obtain operator access without sharing credentials in the issue or PR.
2. Enable collection of the application's structured console output. Confirm UTC timestamps, logger category, event ID/name and trace ID are searchable. Apply only EAS-approved retention (`OI-6`) and notification policy (`OI-23`). If not approved, record each as pending.
3. Request `/health` anonymously and save its status, content type and safe JSON body. It must return the designed envelope with `UP`, not the SPA document. It says nothing about SQL or email readiness.
4. For this later non-Development deployment, request `/api/v1/operations/metrics` both anonymously and with an administrator session: save the shared `404` refusals and confirm no snapshot is returned. The local Development inspection steps above separately prove `401`/`403` protection and authorized access. Do not paste session cookies, credentials or full browser network exports into evidence.
5. Identify any actual platform collector/dashboard separately. If none is configured, record that gap; this JSON snapshot does not collect or retain history. Explain the local metric population/reset behavior and that a 5xx count misses an operation that fails behind a successful HTTP acknowledgement.

## One deliberate failure from the deployed platform stream

Use a deployed test instance with synthetic data and an isolated database. This procedure needs control of that test database; it is not authorization to stop a shared or production database.

1. Open the **platform's application-log viewer** and confirm it is attached to the deployed instance. Record its location and start a narrow UTC time filter. Use a fresh anonymous HTTP client with no session cookies or Authorization header for all event-information requests below; an administrator cookie from the metrics exercise can otherwise fail SQL-backed cookie validation first and return AUTH's `503`.
2. Confirm event information succeeds with that cookie-free client. Make only the disposable test instance's database unavailable using the host's existing operator controls. Keep the application running; do not create an application route that throws on demand.
3. Send one anonymous `GET /api/v1/info/event` request. Record request time, status and response `X-Trace-Id`. The existing INF design failure path should return `500 UNEXPECTED_ERROR`. A transport timeout can demonstrate a bad availability probe, but does not prove the application logged a completed failed request.
4. Search the platform stream for that exact trace ID. Save the real diagnostic and completion entries showing timestamp, event/category, method, route template, status/duration, safe exception type/frame information and matching ID. An access-log status alone is insufficient. If no matching application entry appears, record failure and investigate collection/filtering; do not fabricate a sample line.
5. Restore database access immediately, including when the request or log search fails. Confirm a new cookie-free event-information request succeeds and record that recovery result.
6. Inspect the saved entries for emails, tokens, passwords, codes, connection strings and unnecessary personal information. Check structured fields/scopes and any exception output as well as the displayed message. Keep only the required safe excerpt; crop unrelated records, and do not export a whole log containing other users' data.

If there is no isolated deployed test instance or no platform-log access, leave this exercise pending and record the missing prerequisite. Existing local failure tests and CSV samples do not satisfy it.

## What the person debugging needs

For the deliberate failure, answer these from the actual saved entries:

| Question | Evidence |
|---|---|
| When and on which deployed instance/revision? | UTC timestamp plus deployment/instance record |
| Which request and operation failed? | Response `X-Trace-Id` matches diagnostic/completion IDs; method and route template |
| What did the caller receive and how long did it take? | Status code, stable response error code, elapsed duration and completion outcome |
| Where did it fail? | Logger category, event name, exception/cause type and code-location frames |
| Did recovery work? | Restored dependency and subsequent successful request |
| Did anything private escape? | Inspect actual formatted records and fields; automated tests also inject synthetic sensitive values |

A log with no private values but no cause or request context is insufficient. A raw exception stack/message containing a recipient or connection detail is also insufficient. Record useful, allowlisted code metadata and correlation together.

## Evidence record

Replace `Pending` only with observed values and reviewed, privacy-safe artifacts. This table is not a completed exercise.

| Evidence | Recorded value |
|---|---|
| Immediate demo environment | Local only, confirmed 2026-11-20; implementation pending |
| Local failure response ID, diagnostic excerpt and recovery | Pending implementation; never substitute for deployed evidence below |
| Deployed test URL, environment and commit | Pending |
| EAS host/service and instance | Pending |
| Platform application-log view location/access role | Pending |
| Console collection enabled and actual JSON entry visible | Pending |
| `/health` status/content type/body | Pending |
| Local Development metrics refusal and administrator before/after snapshot | Pending implementation |
| Non-Development metrics absence (`404`, anonymous and administrator) | Pending implementation/deployment |
| Deliberate failure request time, status and trace ID | Pending |
| Matching diagnostic/completion excerpt from platform stream | Pending |
| Private-data inspection and reviewer | Pending |
| Dependency restored; successful recovery request | Pending |
| External availability probe location/window/result | Local sample only; see [availability evidence](availability-slo.md) |
| Notification delivery and retention policy | Pending `OI-23` / `OI-6` |

The course exercise is complete only when the deployed failure, matching platform entries and privacy review are recorded. The design PR can close #117 without claiming those later implementation/deployment obligations are complete. `MNT-error-notification` and production `AVL-uptime` need their own actual evidence.
