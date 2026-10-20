# PharmaLabVR University portal — U05 code-first, read-only

This is a **static English-only student portal source slice**, not a deployed
production portal or completed academic synchronization. No external front-end
dependencies or build service are required.

## Configuration and local static hosting

Edit \`portal/config.mjs\` with the owner's HTTPS \`https://PROJECT.supabase.co/\`
URL and **public** \`sb_publishable_...\` key. Never place a service role key,
Supabase secret, JWT, password, or Auth refresh token in config. Config ships
**empty** by design; the app refuses requests until the public connection is set.
Serve the \`portal/\` directory over HTTPS (or localhost for testing). Opening
\`file://\` is not a supported deployment.

This app uses Supabase GoTrue password sign-in and GET \`/auth/v1/user\` for
verified identity, then reads the existing RLS-protected membership/assignment
tables. It supports student viewing only, not enrollment, invitations,
instructor/admin workflows, uploads, grading or publishing. Auth token is
in-memory for the current tab; no local or session storage. Session end clears
local memory; this source slice does **not** yet implement remote token
revocation/refresh. Expired sessions require signing in again.

## Verification and limits

\`node --test portal/test/*.test.mjs\` checks the source-linked Auth/tenant
contract and safe view output with synthetic HTTP and DOM fixtures only.
It is also invoked from the existing GitHub Native headless workflow.
These are **not** browser, production Auth, Unity, RLS or student-pilot tests.
Supabase RLS and email verification are server-owned. Assignment metadata is
not Native chemical provenance or trusted scientific evidence. Never render
server title fields as HTML.

Do not deploy to the owner's live Supabase project or perform real user signup,
email or production DB changes without owner authorization. G-U1 and G-R1
remain Open; PR #1 stays open/unmerged; Unity Editor/Player/XR NOT_RUN.
