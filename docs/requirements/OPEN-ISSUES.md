# Open Issues

**Project:** _[Your project name]_
**Team:** _[Team NN]_

---

_**What this file is.** Every question about the project that you cannot answer yet, in one place, with the name of the person who can answer it. It is the shortest document in `docs/requirements/` and the one your client meetings run on._

_**Why it exists.** A draft specification with confident guesses in the gaps is more dangerous than one with holes in it, because nobody can tell the guesses from the facts. Writing "we do not know" is not an admission of failure in week 3, it is the correct state. What fails is knowing and not writing it down._

_**Where entries come from.** Three places, and all three are routine:_

- _Drafting a section of [vision-and-scope.md](vision-and-scope.md) and hitting something the client brief does not say._
- _Your agent's list. When you ask it to draft a section, ask it to list every question it could not answer from the material you gave it. Its list is longer than yours and it is not embarrassed to ask obvious things._
- _The meeting itself. Your client says something that contradicts your notes, or answers a question with "I would have to check"._

_**How they leave.** Answered in a client meeting, in Slack, or by reading a document. Record the answer and the date, mark it resolved, and put the substance where it belongs (an objective, a term in the [glossary](project-glossary.md), a business rule). This file is a queue, not a home: an answer that stays here has not been filed._

_**Identifiers here are numbers**, `OI-1` upward, and that is deliberate. Numbers are fine for a list that only ever grows at the bottom and gets cited lightly. The slug convention in [vision-and-scope.md](vision-and-scope.md) exists for identifiers that get **reordered** or **cited often**, which is not this list._

## Before a client meeting

_[Sort the open list by what it costs you to stay wrong, not by what is easy to ask. You will get through fewer questions than you plan to. Take the ones where a wrong guess sends the whole team down the wrong path for a month, and leave the ones you can settle by reading a document or trying the client's current tool yourself.]_

_[Send the shortlist to your client the day before. A client who has seen the questions arrives with answers instead of promises.]_

## Open

| ID | Question | Why it matters | Who can answer | Raised |
|---|---|---|---|---|
| OI-2 | What business benefit should this project improve, expressed as a measurable quantity? | Without a quantified business objective, we cannot complete the Business Objectives section or prioritize requirements in [vision-and-scope.md](vision-and-scope.md). | Client: Wendy Macias | 2026-09-11 |
| OI-1 | _[ is refrog a web application or mobile application?]_ | _[ Blocks deciding what tech stack to use.]_ | _[Client]_ | _[2026-09-11]_
| OI-2 | _[ Do you want the volunteers or shoppers to track the items tracked?]_ | _[ Blocks deciding feature for shopping.]_ | _[Client]_ | _[2026-09-18]_
| OI-3 | _[ What capabilities should donation partners have within the app?]_ | _[ Blocks deciding feature for donation partners.]_ | _[Client]_ | _[2026-09-18]_
| OI-4 | Does logging a donation require the donor to be signed in, or can it stay anonymous like today's QR-code form? | Blocks `UC-DON-log-donation`'s preconditions and whether donors need accounts at all. | Client: Wendy Macias | 2026-09-23 |
| OI-7 | When a shift becomes understaffed: is the primary response to broadcast the opening to other volunteers so they can self-serve claim it (closest to what Wendy described — "message out to people... have somebody be able to sign up... to fill that spot"), or to alert an administrator directly? If administrators are alerted (as a fallback or otherwise), who exactly is contacted, how soon before the shift, and what happens if nobody responds in time? | Blocks the design of `UC-VOL-cancel-shift`'s extension 6b — currently written as admin-notification-only, which may not match what the client actually asked for. Also touches who counts as an "administrator" for alerting purposes, which the team has provisionally scoped to the three founders in `UC-ADM-view-dashboard`. | Client: Wendy Macias / ReFrog committee | 2026-09-23 |
| OI-9 | Does creating and configuring volunteer shifts happen inside the new app, or does the client intend to keep using an external tool (e.g., SignUpGenius) for that? | Blocks `UC-ADM-manage-shifts`, which every `UC-VOL-*` use case depends on. | Client: Wendy Macias | 2026-09-23 |
| OI-10 | What TCU affiliation-verification method is permitted and practical without requiring TCU single sign-on? | Blocks `UC-IDV-verify-affiliation`'s core mechanism. Originally raised as `OI-authentication` in `client-interview-2026-09-11.md`'s open questions but never filed here until now. | Client: Wendy Macias / TCU IT | 2026-09-11 |
| OI-11 | What rules should identify or limit excessive shopping and potential resale activity? | Blocks `UC-ABU-review-alert` entirely — the use case cannot be finalized without this. Originally raised as `OI-shopping-limits` in `client-interview-2026-09-11.md`'s open questions but never filed here until now. | Client: Wendy Macias / ReFrog committee | 2026-09-11 |
|

## Resolved

| ID | Question | Answer | Answered by | Date | Filed in |
|---|---|---|---|---|---|
