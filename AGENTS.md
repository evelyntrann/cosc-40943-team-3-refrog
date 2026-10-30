# AGENTS.md — ReFrog charter (Team 3)

Read this at the start of every session. Each rule is one line and cites where it is decided; follow the citation for the reasoning. If a rule here and the cited document disagree, the cited document wins — flag the mismatch instead of picking one.

## The project

ReFrog: a phone-first progressive web app for TCU's finals-week move-out donation event — donations, volunteer shifts, TCU-affiliated shopping, and an admin dashboard (`docs/requirements/vision-and-scope.md`).

## Where things live

- Requirements: `docs/requirements/` — use cases (`use-cases.md`), business rules (`business-rules.md`), specification (`software-requirements-specification.md`), glossary, open issues (`OPEN-ISSUES.md`).
- Architecture-of-record: `docs/design/architectural-design.md`. Crosscutting rules are its §8.2.
- Design-of-record, one per area: `docs/design/<area>.md` (`adm`, `vol`, `don`, `shp`, `ABU`, `IDV`, `INF`).

## How to work

- Build only from a merged design-of-record and a build-context issue that cites a `UC-*`; read the use case, its design, and everything they cite before writing code.
- Cite identifiers (`UC-*`, `BR-*`, `FR-*`, `OI-*`); never copy a use case or rule into code comments, issues, or docs.
- Never invent a rule, threshold, or term: if the repository does not say it, stop and list it as an assumption; client questions go in `OPEN-ISSUES.md`.
- Do not implement anything blocked on an open issue; build the stated default and leave it at that.
- Do not add scope the use case does not ask for (no extra queues, overrides, status fields, or "while we are here" features).

## The agent must not

- Add a dependency until a person confirms it exists and is the one we meant (course Agent Setup; `.claude/commands/implement.md`).
- Remove the `Co-Authored-By` trailer from a commit (`.claude/commands/implement.md`).
- Merge a pull request, force-push, or delete the repository (`.claude/settings.json` deny rules).
- Read secrets: `.env` files, `secrets/`, `*.pem`, `*.key` (`.claude/settings.json` deny rules).

## Parallel agents (course Agent Setup)

- One agent per sub-issue, each in its own worktree (`.claude/worktrees/`) and branch, ending in its own pull request.
- Fan out only after the shared code the leaves depend on is merged.
- Never split one use case's layers across agents.
- No more agents running than pull requests you will review that day.

## Project-wide constraints

- One deployable: PWA front end and application modules ship together, one database (architecture §9.2 `KD-deployment-shape`).
- Technology stack: Java / Spring Boot application, Vue.js PWA, PostgreSQL database (architecture §9.2 `KD-tech-stack`); do not add another language, framework, or database on your own.
- Hosting: **unresolved**, TCU IT vs. paid (`OI-15`); keep hosting specifics out of code.
- Sign-in: the app's own email-and-password accounts, email confirmed by a one-time code at sign-up; no Google or other external identity provider (architecture §9.2 `KD-own-accounts`). Sign-in proves identity only, never TCU affiliation (architecture §8.1).
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
- **Logging:** log enough to diagnose failures; never log secrets, verification codes, raw TCU IDs, or unnecessary personal data (§8.2.7).
- **Persistence:** persist only after validation and business rules pass; one transaction per smallest complete business operation (§8.2.8).
- **Auditing:** record admin sign-in attempts and admin changes only; no general-purpose audit subsystem (§8.2.9).
- **Testing:** cover both accepted and rejected requests with non-production data, including auth, validation, duplicates, time zones, and persistence failures (§8.2.10).

## Git (team contract §5)

- Branch per sub-issue: `feat/<issue-number>-<short-slug>`; never push to `main`; every change is a pull request.
- A pull request needs one approving review from someone who does not own the use case.
