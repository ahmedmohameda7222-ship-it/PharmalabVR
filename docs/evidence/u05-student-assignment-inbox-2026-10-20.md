# U05 partial — read-only student assignment inbox (20 October 2026)

**Implementation status: source-linked headless regression for a genuine REST read contract. Not a finished university portal, not live Auth and not Unity run.**

## Scope
- The Unity-compatible `UniversityStudentAssignmentInbox` reads the existing `public.assignments` table by GET over recognized HTTPS Supabase REST using *only* `sb_publishable_*` and an active user's bearer token.
- The request selects `id,institution_id,section_id,title,published_at,due_at`, filters institution to the user's current fresh selected student membership, requires `published_at` non-null, and caps results at 100. Any authorization is enforced by PostgreSQL RLS, never by the local role hint.
- The response is bounded to 128 KiB. All rows are rejected as a set if malformed, duplicate, cross-tenant, missing publication, or otherwise inconsistent. HTTP failures/timeouts do not fabricate assignments. Offline Native lab remains independent, no grade/submission mutations or science values.
- Current limited client returns an empty result both for no published assignments and unavailable/rejected data; **full U05 UX must distinguish these states** before release.

## TDD and CI
- RED `81808014ca8cbc719c34f273bb9bc9de89854a2c` (PR [39363530180](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39363530180)) failed expected missing source file in the university headless contract.
- Source `aa45ecc4eb75de3dc7d9d3f59566fe3e6f4da848`; C# fixture string escaping fixed `8d0f20d61506eed42169479d9e6ce78ff7e14977`.
- At `8d0f20d`, panel headless source-linked tests **46 assertions passed** (12 new U05), and U04 86 assertions; [PR Native 39363856825](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39363856825) panel success confirmed. Windows/Ubuntu/native and Supabase CI are separately tracked by exact SHA; no fake Auth response establishes RLS acceptance.
- Unity NUnit EditMode test source `cb3e4225ea3075ac33485cddc3652531cd3c73ef` **NOT_RUN**. Real learner/instructor/admin portal, UI, production Auth, two-university human pilot, grading and native trusted scientific artifact still OPEN.

## Safety
No change to the owner's live 18 Supabase migrations, Auth users, Edge functions, Storage, role grants or server deployment. Multi-institution RLS remains server-owned, PR #1 unmerged, English-only product, original 66-task ledger unchanged, G-U1/G-R1 Open, SessionLoad Hold preserved.
