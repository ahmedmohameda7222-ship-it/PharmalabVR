# PharmaLabVR University portal — U05 code-first, read-only

This is a **static English-only student portal source slice**, not a deployed
production portal or completed academic synchronization. No external front-end
dependencies or build service are required.

## Configuration and local static hosting

Edit `portal/config.mjs` with the owner's HTTPS `https://PROJECT.supabase.co/`
URL and **public** `sb_publishable_...` key. Never place a service role key,
Supabase secret, JWT, password, or Auth refresh token in config. Config ships
**empty** by design; the app refuses requests until the public connection is set.
Serve the `portal/` directory over HTTPS (or localhost for testing). Opening
`file://` is not a supported deployment.

This app uses Supabase GoTrue password sign-in and GET `/auth/v1/user` for
verified identity, then reads the existing RLS-protected membership/assignment
tables. Students can select a published assignment and retrieve its pinned lesson version;
lesson `content_json` is displayed as inert read-only JSON text, not as executable
instructions or scientifically verified data. Instructors, teaching assistants and university administrators can additionally browse
JWT/RLS-visible courses, sections and published assignments in an English-only,
read-only hierarchical overview. An instructor or university administrator can
select a section, choose server-published same-course lesson and rubric versions,
and submit an assignment through the existing authenticated
`public.university_publish_assignment` RPC with a per-draft idempotency key.
Publication is an academic server operation, never a chemistry calculation,
Native receipt attestation or direct assignment-table mutation. The first 100 records per category are shown;
this is not a complete institutional audit. It supports staff and student reading,
not enrollment, invitations,
instructor/admin workflows, uploads, grading or publishing. Auth token is
in-memory for the current tab; no local or session storage. Session end immediately clears local memory and attempts Auth logout with
`scope=local` (not global). Failed HTTP does not mean the refresh token was
revoked; even successful logout does not revoke existing JWT access tokens
until their expiry. Refresh remains unimplemented and expired sessions must
sign in again.

## Verification and limits

`node --test portal/test/*.test.mjs` checks the source-linked Auth/tenant
contract and safe view output with synthetic HTTP and DOM fixtures only.
It is also invoked from the existing GitHub Native headless workflow.
These are **not** browser, production Auth, Unity, RLS or student-pilot tests.
Supabase RLS and email verification are server-owned. Assignment metadata is
not Native chemical provenance or trusted scientific evidence. Never render
server title fields as HTML.

Do not deploy to the owner's live Supabase project or perform real user signup,
email or production DB changes without owner authorization. G-U1 and G-R1
remain Open; PR #1 stays open/unmerged; Unity Editor/Player/XR NOT_RUN.

## Request time limits
All browser API reads, sign-in and session-scoped logout use an abort-aware
12-second default request deadline (including bounded response streaming).
An unavailable connection never becomes an academic assignment or successful
server revocation. The injected timeout used in synthetic Node tests is not a
claim of verified real-browser behavior.
