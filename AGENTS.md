# AGENTS.md — ReFrog charter (Team 3)

Read this at the start of every session. Each rule is one line and cites where it is decided; follow the citation for the reasoning. If a rule here and the cited document disagree, the cited document wins — flag the mismatch instead of picking one.

## The project

ReFrog: a phone-first progressive web app for TCU's finals-week move-out donation event — donations, volunteer shifts, TCU-affiliated shopping, and an admin dashboard (`docs/requirements/vision-and-scope.md`).

## Where things live

- Requirements: `docs/requirements/` — use cases (`use-cases.md`), business rules (`business-rules.md`), specification (`software-requirements-specification.md`), glossary, open issues (`OPEN-ISSUES.md`).
- Architecture-of-record: `docs/design/architectural-design.md`. Crosscutting rules are its §8.2.
- Design-of-record, one per area: `docs/design/<area>.md` (`ADM`, `VOL`, `DON`, `SHP`, `ABU`, `IDV`, `INF`).

## How to work

- Build only from a merged design-of-record and a build-context issue that cites a `UC-*`; read the use case, its design, and everything they cite before writing code.
- Before marking a use case `✅ Verified`, map its main flow, every extension, and the design's failure-path test list to actual passing assertions, and record missing evidence as `🟡 Partial`; requests for agent code reviews must name the `UC-*` and its design test list ([VOL code review](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/130#issuecomment-7023772178), [AUTH code review](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/133#issuecomment-7023784031); architecture §8.2.10).
- Cite identifiers (`UC-*`, `BR-*`, `FR-*`, `OI-*`); never copy a use case or rule into code comments, issues, or docs.
- Never invent a rule, threshold, or term: if the repository does not say it, stop and list it as an assumption; client questions go in `OPEN-ISSUES.md`.
- Do not implement anything blocked on an open issue; build the stated default and leave it at that.
- Do not add scope the use case does not ask for (no extra queues, overrides, status fields, or "while we are here" features).

## The agent must not

- Add a dependency until a person confirms it exists and is the one we meant (course Agent Setup; `.claude/commands/implement.md`).
- Remove the `Co-Authored-By` trailer from a commit (`.claude/commands/implement.md`).
- Merge a pull request, force-push, or delete the repository (`.claude/settings.json` deny rules).
- Read secrets: `.env` files, `secrets/`, `*.pem`, `*.key`, `*.pfx`, `*.p12`, .NET user-secrets (`secrets.json`), `appsettings.Production.json` (`.claude/settings.json` deny rules).

## Parallel agents (course Agent Setup)

- One agent per sub-issue, each in its own worktree (`.claude/worktrees/`) and branch, ending in its own pull request.
- Fan out only after the shared code the leaves depend on is merged.
- Never split one use case's layers across agents.
- No more agents running than pull requests you will review that day.

## Project-wide constraints

- One deployable: PWA front end and application modules ship together, one database (architecture §9.2 `KD-deployment-shape`).
- Technology stack: C# / ASP.NET Core application, Blazor PWA, Microsoft SQL Server database (architecture §9.2 `KD-tcu-it-stack`); do not add another language, framework, or database on your own.
- Hosting: EAS will host the ASP.NET Core application and Microsoft SQL Server database on TCU-managed infrastructure (`OI-15`, resolved); Azure is not required. Keep provider-specific hosting assumptions out of code unless EAS approves them.
- Sign-in: the app's own email-and-password accounts, email confirmed by a one-time code at registration; no Google or other external identity provider (architecture §9.2 `KD-own-accounts`). A successfully code-verified exact `tcu.edu` registration email also establishes persisted TCU email verification; other sign-in emails prove identity only (architecture §8.1, `FR-AUTH-account-signup`).
- TCU affiliation is proven only by the email one-time code (`UC-IDV-verify-affiliation`, `OI-10`).
- Donors do not sign in (`OI-4`); donation partners get no app access (`OI-3`).
- Email goes through one shared delivery adapter; the provider is not chosen — code against the adapter (architecture §3, §5.1).

## Crosscutting rules (architecture §8.2)

- **Error handling:** distinguish validation, authorization, unavailable, and unexpected failures in one consistent response; when authorization state can't be established, deny as unavailable with retry, never erase saved state, and never report a write as saved unless it was (§8.2.1).
- **Time:** store timestamps in one machine-readable form, display in `America/Chicago` on a 12-hour clock with a.m./p.m., and read "now" from an injectable clock so tests can fix it (§8.2.2).
- **API:** consistent resource names, one shared success/error envelope, documented status codes; no per-area envelopes (§8.2.3).
- **Code:** one formatter, linter, and test layout across the repo; do not duplicate business rules between client and server (§8.2.4).
- **Validation:** the server check is the one that counts; name the bad field and keep the user's valid input (§8.2.5).
- **Config and secrets:** settings come from deployment configuration; secrets never appear in source, docs, logs, exports, or error responses (§8.2.6).
- **Logging:** use one `ILogger<T>` per class, structured message templates, audience-based levels, correlated safe exception diagnostics for handled failures, and identifiers rather than personal content; centralized collection and operational error notification follow §8.2.7 and `MNT-error-notification`; never log secrets, verification codes, raw TCU IDs, or unnecessary personal data.
- **Persistence:** persist only after validation and business rules pass; one transaction per smallest complete business operation (§8.2.8).
- **Auditing:** record admin sign-in attempts and admin changes only; no general-purpose audit subsystem (§8.2.9).
- **Testing:** cover both accepted and rejected requests with non-production data, including auth, validation, duplicates, time zones, and persistence failures (§8.2.10).

## Git (team contract §5)

- Branch per sub-issue: `feat/<issue-number>-<short-slug>`; never push to `main`; every change is a pull request.
- A pull request needs one approving review from someone who does not own the use case.
