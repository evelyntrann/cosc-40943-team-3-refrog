# Team Contract: ReFrog (Team 3)

**Project:** ReFrog
**Members:**
- Evelyn Tran — @evelyntrann
- Ananye Kejriwal - @a1kj
- Alexa Mercado — @amercado9
- Alfonso Rodriguez-Tomax — @Alfonsotomax
- Spencer Scherger — @SpencerScherger
- Ethan Wong - @remag3

**Repository:** https://github.com/evelyntrann/cosc-40943-team-3-refrog, owned by Evelyn Tran
**Signed:** September 4, 2026

## 1. Meeting time

We meet every **Wednesday** and **Friday**, **8:00am–10:00am**, in **the library**.
A member who cannot attend tells the team **24 hours ahead** and reads the minutes.

Our first client planning meeting is **9/12/2026 at 1:00pm**.

## 2. Communication

Primary channel: **Slack, #refrogteam3**. Client contact goes through **Wendy Macias (w.macias@tcu.edu)**.

We reply within **24** hours on weekdays. Anything urgent: **text the Slack chat**.

## 3. How we decide

Routine calls: whoever owns the use case decides.
Anything affecting the whole team: discussed at the weekly meeting, decided by majority vote.
A decision that survives the meeting is written down in our [weekly meeting minutes doc](https://docs.google.com/document/d/1e5lhVox60dBIgWSU1XA2-u0phgaDFue-6sH4He0c6po/edit?usp=sharing).

## 4. How work is claimed

Work is divided **by use case, not by layer**. One member owns a use case end to
end: front end, back end, tests, and the pipeline.
Claiming: assign yourself to the sub-issue on GitHub and move it to *In Progress* on the project board.
Nobody is the "front-end person" or the "tester".

## 5. Git workflow and review

Coding conventions (naming, formatting, layout) live in `AGENTS.md`, or `CLAUDE.md` not here.
This clause is about how work moves.

Branch per sub-issue, named **feat/<issue-number>-<short-slug>** (e.g. `feat/42-login-form`).
Never push to `main`. Every change arrives as a pull request.
A pull request needs at least **1** approving review from someone who does not own the use case.
A reviewer reads the issue before the diff. Blocking a merge: failing CI, an unresolved requested change, or missing tests for new behavior.

## 6. Working with AI

We use **Claude Code or Codex**. Our charter lives in `AGENTS.md`.
Every member can explain any line submitted under their name.
We do not merge agent output that nobody has read.
Additional limits we agree on: **none**.

## 7. When someone does not deliver

First: the use case owner raises it directly with the person within 48 hours of a missed commitment — privately, not in the group chat. We attack the problem, not the person: offer to help unblock, or agree on a smaller scope.
If it happens again: it's raised at the next team meeting, the remaining work is reassigned, and the meeting minutes note it.
Still unresolved: we escalate to our TA, then to the instructor. We escalate early rather than waiting until it threatens the deadline.

## Signatures

Each member adds their own line, in their own commit.

- Evelyn Tran, September 4, 2026
- Alexa Mercado, September 4, 2026
- Alfonso Rodriguez-Tomax 4, 2026
