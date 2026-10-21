# U03/U05 — English-only instructor academic portal and server RPC publishing

**21 October 2026. Source and mocked headless CI only. No real browser, user, or Unity run. U05/G-U1 Open.**

## What changed
- Authenticated `staffOverview` reads first 100 tenant-filtered course, section and published assignment rows via existing PostgreSQL RLS, fresh membership lookup and JWT. Validates canonical IDs, same-tenant course→section→assignment relationships; fail closed on duplicates/foreign records/denials; role string is UI hint not permission.
- `staffPublicationOptions` asks server for RLS-visible published immutable lesson and rubric versions on a selected section course; instructor/admin only.
- `staffPublishAssignment` validates canonical IDs, published option pins, exact request id, title and future due date, then calls existing deployed audited `public.university_publish_assignment` security-definer RPC with authenticated user JWT. The server makes actual decisions on institution/section instructor, published content, idempotency/replay and audit. No direct POST to `assignments` table, no grades or science.
- `portal/src/view.mjs` presents inert academic text, explicit section selection and published-version form; `portal/app.mjs` retains in-memory JWT and same-draft request ID on retries, prevents stale async responses across institution switching/signout, and displays confirmed assignment UUID only when RPC returns a canonical UUID. No real portal deployment.

## Red/green and exact-SHA evidence
- Staff API RED `9ec95f8` [Native PR 39570428945](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39570428945) failed four expected missing methods; GREEN `370cb0d`.
- Staff view RED `9b82e1f` [Native PR 39570768080](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39570768080) missing export; GREEN `341e97f`; browser `bfbfb00` full Native + disposable Supabase PR SUCCESS.
- Pinned lesson/rubric version RED `3dc7cea` [Native PR 39571560460](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39571560460), GREEN `b1ff036`, Native + PostgreSQL PR SUCCESS.
- Existing server RPC RED `a2b350b` [Native PR 39571891203](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39571891203), GREEN `101431a`, Native + PostgreSQL PR SUCCESS.
- Publishing form RED `902eb2b` [Native PR 39572252263](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39572252263), GREEN `0fd1d8c`; integrated `9b60e97` [Native PR 39572756948](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39572756948) **34/34** mocked Node portal assertions + browser syntax green, [disposable Supabase PR 39572756980](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39572756980) SUCCESS. OS Native latest exact SHA outcomes checked separately.
- UI scope clarification SHA `2356a9c` and evidence SHA follow. No new migration and no live owner project action.

## Remaining
Real confirmed human Auth and two-institution browser pilot, positive database-backed exact browser-to-RPC publication and student receipt, course/section/enrollment creation UI, lesson/rubric authoring UI, submission and grade/feedback UI, pagination, complete accessibility and mobile, and final Unity 6000.3.25f1 Editor/Player/XR/physical headset tests. Academic staging stays quarantined; no trusted Native origin attestation, signed cloud ack or graded completion. PR #1 open/unmerged, P06.02 still formal next of 66 original tasks (15 VerifiedLocal/51 Open), English-only permanent.

## Admin onboarding of academic structures (source-only)
- Admin course/section RPC RED `e88881c` [Native PR 39573784334](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39573784334), GREEN `4648668289252d3b1b29b26017d5d976a1c138b2` [Native PR 39574013216](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39574013216) SUCCESS and disposable PostgreSQL PR 39574013238 SUCCESS. Existing audited SQL RPC only; no direct inserts. UI empty-university course + existing-course section form RED `834293e`, GREEN `619f688c`, browser `051206d6`.
- Verified existing-student enrollment RPC RED `7e0f3d2`, GREEN `c953cba1f3f99dfa24e028174aacecc0d406913e`: [Native PR 39574989674](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39574989674) and disposable [Supabase PR 39574989715](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39574989715) SUCCESS. Enroll form RED `f34dbee`, GREEN `cce02df`, browser source `f9f2d3e`. Requires separately-created confirmed existing student active membership; never creates user or grants membership.
- Full browser and owner Auth pilot NOT_RUN; U05/G-U1 Open; original 66 roadmap still 15 VerifiedLocal/51 Open. No production DB mutation, no Unity Cloud build or merge.
