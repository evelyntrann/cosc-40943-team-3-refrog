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
| OI-5 | Do you want a report at the end of the event, from every location, or something else? | Gives the client a report to show things are going smoothly. | Client: Wendy Macias | 2026-09-25 |
| OI-6 | How long must donation, shopping, volunteer, affiliation-check, and partner-pickup records be retained, and which records may contain an identifiable shopper or volunteer? | SRS section 7.4.3 cannot define retention, data minimization, or production storage behavior without approved periods and identity rules. **Enriched 2026-10-02, not yet a final number:** Wendy gave role-specific guidance rather than a single period — roughly 4 years for student participants (matching their TCU tenure), substantially longer for volunteers (many are long-tenured faculty/staff), annual data *exports* regardless of retention window, and she's open to an annual re-login/refresh as a lighter alternative to long-term storage. The team still owes her a final recommendation. | Client and TCU data-governance owner | 2026-09-25 |
| OI-11 | What rules should identify or limit excessive shopping and potential resale activity? | Blocks `UC-ABU-review-alert` entirely — the use case cannot be finalized without this. Originally raised as `OI-shopping-limits` in `client-interview-2026-09-11.md`'s open questions but never filed here until now. **Enriched 2026-10-02, still not a defined rule:** Wendy gave a qualitative hint when asked — the signal is a category-and-quantity combination, not a flat item count ("20 pieces of clothing, I have no problem; 20 mirrors, that would raise a red flag"). She was explicit she isn't ready to define a hard rule this early; the team agreed to keep this use case but not prioritize it for the first release. | Client: Wendy Macias / ReFrog committee | 2026-09-11 |
| OI-12 | Who will maintain the application after the current senior design team completes the project: ReFrog administrators, another senior design group, or another designated owner? | The maintenance owner determines the required handoff, documentation, account ownership, hosting responsibility, and technology choices. | Client: Wendy Macias | 2026-09-24 |
| OI-14 | What backup frequency, recovery point, and recovery time are required for the operational data used by the reports? | What is the backup integrity; the answer determines whether a lost or corrupted event record can be recovered. | Client and operational owner | 2026-09-25 |
| OI-15 | Which hosting path: TCU IT or a paid alternative, and what does each cost? | Wendy is open to either — she has funding sources available if a paid option is needed (SGA, Staff Assembly, and Housing have covered costs before) — but is waiting on the team to send pros/cons/costs and a decision deadline before she can choose. **This is an action item the team owes her**, not just an open question. | Client: Wendy Macias / Team | 2026-10-02 |
| OI-16 | How far before an assigned volunteer's own shift should the system remind them? | Blocks `FR-NOTIFY-shift-reminder`'s timing. Distinct from the (now resolved) `OI-7`, which was about prompting *other* volunteers to fill an opening, not reminding someone of a shift they already have. | Client: Wendy Macias | 2026-10-02 |
| OI-17 | What sign-in provider handles general authentication (volunteers, administrators)? | Blocks `UC-VOL-signup`'s sign-in precondition and the "Authentication" component in the architecture document. Distinct from the (now resolved) `OI-10`, which settled TCU-affiliation verification specifically (an email code, independent of whatever the user signed in with) but left the general sign-in provider itself undecided. | Client: Wendy Macias / Team | 2026-10-02 |

## Resolved

| ID | Question | Answer | Answered by | Date | Filed in |
|---|---|---|---|---|---|
| OI-1 | Is ReFrog a web application or mobile application? | A progressive web app (PWA): accessible via QR code/browser link with no install required, with installing to the home screen offered as optional. Wendy confirmed this directly as a good compromise for both "lives on their phone" and "quick one-time access" users. | Client: Wendy Macias | 2026-10-02 | `vision-and-scope.md` Vision Statement |
| OI-3 | What capabilities should donation partners have within the app? | None — donation partners get no app access. Pickup coordination stays outside the app, through the administrators, same as today. | Client: Wendy Macias | 2026-10-02 | `use-cases.md` (confirms the existing exclusion of `UC-PTR-*`) |
| OI-4 | Does logging a donation require the donor to be signed in, or can it stay anonymous like today's QR-code form? | No sign-in required. Wendy was explicit: "I'm happy for it to be anybody because sometimes parents might be there." A volunteer may also fill out the donation form on a donor's behalf. | Client: Wendy Macias | 2026-10-02 | `UC-DON-log-donation` |
| OI-7 | When a shift becomes understaffed, who should be contacted, by whom, and what happens if nobody responds? | Neither option originally framed — it's a manual, admin-triggered notification, not an automatic one. An administrator gets a control to send a notification (to volunteers who haven't signed up, or to everyone) whenever they decide it's needed, rather than the system firing one automatically per cancellation. Wendy was explicit about not wanting to over-notify volunteers; this was proposed by the team during the meeting and she approved it directly ("That sounds lovely"). | Client: Wendy Macias | 2026-10-02 | `UC-VOL-cancel-shift`, new `UC-ADM-notify-volunteers` |
| OI-9 | Does creating and configuring volunteer shifts happen inside the new app, or does the client intend to keep using an external tool (e.g., SignUpGenius) for that? | Yes, replace SignUpGenius — shift creation moves into the app. "That would make things more integrated and help a lot." | Client: Wendy Macias | 2026-10-02 | `UC-ADM-manage-shifts` |
| OI-10 | What TCU affiliation-verification method is permitted and practical without requiring TCU single sign-on? | An email verification code: the shopper enters their TCU email on a profile page, the system sends a code, and a verified confirmation screen is shown to a volunteer at the location. Given a direct choice between this and an in-app physical-ID check, Wendy chose the email code: "it makes perfect sense... since if somebody's associated with TCU, they'd have that... tcu.edu email address." This replaces the earlier assumption that affiliation could be derived from a Google Sign-In email domain, which depended on TCU's email running on Google Workspace — never confirmed, and not what was actually decided. | Client: Wendy Macias | 2026-10-02 | `UC-IDV-verify-affiliation` |
| OI-13 | Do you want the volunteers or shoppers to track the items tracked? | Shoppers self-report, the same as the existing design assumed. "Normally it'd be the shopper. So it'd be self-reported." | Client: Wendy Macias | 2026-10-02 | `UC-SHP-log-item-taken` |
