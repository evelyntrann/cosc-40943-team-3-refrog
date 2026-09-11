# Napkin — Round 0

**Project:** ReFrog  
**Team:** Team 3  
**Author:** Ethan Wong (team lead)  
**Date:** 2026-09-11  
**Time-boxed to:** 20 minutes before the first client planning meeting

> [!NOTE]
> This is a rough, pre-meeting judgment written deliberately before detailed requirements exist.
> Its purpose is to make our sizing assumptions visible so the team can correct them after the
> 9/12 meeting, not to commit to anything. Every answer here is a bet, not a decision.

---

## 1. Shape

**One sentence:** ReFrog is a CRUD web application — volunteers register for events and sign up for item slots, organizers track donated inventory, and a coordinator sees it all in a dashboard.

```
┌─────────────┐     ┌───────────────┐     ┌─────────────────┐     ┌──────────────┐
│  Volunteer  │────▶│  Sign-up /    │────▶│  Inventory /    │────▶│  Organizer   │
│  (public)   │     │  Auth layer   │     │  Donation       │     │  Dashboard   │
└─────────────┘     └───────────────┘     │  Tracker        │     └──────────────┘
                                          └─────────────────┘
```

Five boxes: **Public sign-up form → Auth/identity layer → Volunteer roster → Inventory/donation tracker → Organizer dashboard.**  
No real-time feed, no ML, no complex pipeline. This is forms and tables with a thin reporting layer on top.

---

## 2. The Hard Part

**Inventory tracking across categories is the one thing that makes this not a weekend project.**

Taking a volunteer sign-up form is a weekend project. The hard part is the constraint model underneath: each donated item type (food, clothing, hygiene supplies, etc.) has its own rules — quantity caps, whether duplicates are allowed, how "claimed" differs from "arrived" — and the coordinator needs to know the live running total *before* the event to avoid over- or under-commitment.

If we model inventory wrong in week 4, every use case that touches it has to be rewritten in week 10.

---

## 3. Bottleneck

**Where it breaks: under a five-person team, not under load or scale.**

Event sign-up volume is modest (likely tens to low hundreds of volunteers per event). The database will never sweat.

The real bottleneck is team coordination: **work is divided by use case, but inventory and volunteer sign-up are tightly coupled**. The member who owns "claim an item slot" and the member who owns "organizer views inventory" are building against the same data model. If they define it independently, they collide. The bottleneck is the data model review that has to happen before either branch opens, not concurrency or scale.

---

## 4. Stack

**Boring default: React + FastAPI (Python) + PostgreSQL, deployed on Railway or Render.**

Why: the team already uses Claude Code / Codex as the AI teammate, which has strong coverage for this stack. Python is the language the client's post-graduation maintainer is most likely to know (TCU CS context). PostgreSQL handles relational inventory constraints without drama. Railway/Render removes DevOps as a weekly time sink for a six-person team with a December deadline.

The only reason to deviate is if the first client meeting reveals an existing system (a spreadsheet-backed Google Form, an Airtable base) we have to write to or export from — in which case we evaluate the API surface of whatever that is before picking an integration layer.

---

## 5. Kill Risks

1. **Inventory state drift:** A volunteer claims a slot, the form submits, the backend write fails silently, and the coordinator sees a count that is one higher than reality. At event day, a category is over-promised. Mechanism: no transaction boundary around "claim slot + decrement available count." Fix: single DB transaction, not two separate API calls.

2. **Scope creep via friendly client:** Wendy Macias is the co-founder, which means every feature she mentions in a meeting carries the weight of a product decision. The team contract says we commit to nothing in the room. If we do not enforce "let me bring that back to the team" in the 9/12 meeting, our feature list doubles before the vision-and-scope is stable. Mechanism: team lead does not have commit authority in client meetings.

3. **Maintainability cliff:** This system has to survive after we graduate. If the stack or deployment depends on a CLI tool or a cloud service only one teammate understands, the client is left with dead software in January. Mechanism: we pick nothing we cannot document in under one page; we ask who maintains it in the 9/12 meeting and let the answer constrain the stack.

---

## 6. Verdict

**Feasible: yes** — for a December MVP, conditional on the scope staying narrow.

A volunteer sign-up + inventory dashboard is a workable semester project for six people working in use-case-owned slices. The data model is not trivial but it is knowable.

**What we cut first:** real-time updates (use a refresh button, not a WebSocket), email notifications (defer to the next cycle or use a simple mailto link), and any analytics beyond a running count per category. The core loop is: volunteer claims a slot → coordinator sees it → inventory count goes down. Everything else is a feature request for round 2.

**What we validate at the 9/12 meeting:**
- How many categories of donated items? Are quantities capped per event or globally?
- Who is the post-graduation maintainer and what do they run today?
- Is there an existing sign-up tool (Google Forms, Airtable) we inherit, or are we greenfield?
- What does "submitted" vs. "confirmed" vs. "arrived" mean for a donation? (Inventory state machine.)
