# U05 English-only browser portal: first code-first slice (20 October 2026)

**Scope: read-only source; NOT deployed; NOT a completed student/instructor/admin portal; not Unity.**

## Implemented
- `portal/index.html`, `styles.css`, `config.mjs`, `app.mjs` form a static responsive English-only student page. Configuration is blank at source; only HTTPS `*.supabase.co` and `sb_publishable_*` may be configured, with no private keys.
- `portal/src/api.mjs` performs password sign-in using Supabase GoTrue, checks `/auth/v1/user` to confirm a real Auth identity and verified email, reads active memberships via the existing RLS-protected table and reads published assignment metadata only for a current student institution. Each call uses a bearer JWT and public key; server RLS is the authority. Tokens remain in JS module memory only; no local/session storage.
- `portal/src/view.mjs` uses DOM `createElement`/`textContent`; all untrusted assignment titles are text, never HTML. It distinguishes real empty lists, loading, service unavailable and absent student context, and never assigns grades/material/Native receipt status.
- Browser local session ending clears memory only and currently lacks server-side Auth logout/revocation. No browser deployment or live Auth pilot.

## TDD and verification
- RED `f0f6cc4edac78c609e267364e8135940513de5a0`: absent `portal/src/api.mjs` in [PR CI 39391177830](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39391177830).
- GREEN API `998fda50ea53320f8543f39db474b814cbf68c03`: Node simulated GoTrue / current student membership / RLS endpoint protocol; exact-sha panel GREEN, no real owner Auth.
- RED `cebfe7c7d925e5e2eb712dc6eeb852255a36ff63`: absent `portal/src/view.mjs` in [PR CI 39391668181](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39391668181).
- GREEN view and page `ac12b675381997d5829c5f8513370a9a06a892d6`: [Native PR CI 39392065456](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39392065456) panel **9/9 Node portal tests PASS**, native Windows/Ubuntu SUCCESS, and [isolated Supabase PR 39392065647](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39392065647) SUCCESS.
- `49637dfccee98d9f4d03892e6e7387afb0bc9cca` adds `node --check` for browser entrypoint/config/source. Native PR [39392331783](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39392331783) headless panel SUCCESS, but full exact-SHA CI remains separately tracked.

## Required follow-through
Actual browser authentication and hosted deployment, production verified student email/first owner-selected institution, enrolled sections, lesson/rubric/assignment/instructor/teacher/admin workflows, Auth refresh/logout semantics, accessible UI interaction, scientific attestation and academic evidence promotion remain OPEN. No live Supabase changes here. Original 66 canonical tasks unchanged: 15 VerifiedLocal / 51 Open, next P06.02. U05, G-U1, G-R1 OPEN; Unity Editor/Player/XR NOT_RUN, English-only, PR #1 unmerged.
