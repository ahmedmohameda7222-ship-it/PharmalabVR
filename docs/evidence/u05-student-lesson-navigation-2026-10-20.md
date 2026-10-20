# U05 published assignment → pinned lesson source increment

**Code-first PARTIAL on 20 October 2026. U05 Open; G-U1/G-R1 Open. No production deploy, browser pilot or Unity run.**

## Existing source contract
`supabase/migrations/20261017000000_university_core.sql` defines each published assignment with institution, section, course and `lesson_version_id` FK. The existing RLS policy allows an enrolled student to read only the published version assigned to a section they may view; instructor/admin rules are separate. This increment does not alter SQL or remote Supabase.

## TDD evidence
- RED `8a34ae693312c1fbd42109fa63bc3f1485136270` failed 4 new Node regressions at missing `assignmentLesson`, [Native PR 39395524920](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39395524920).
- GREEN API `f457b8a19a9006ea4a18f9bb6986a66dcbf68ec6`, [Native PR 39395714320](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39395714320) SUCCESS; [disposable PostgreSQL PR 39395714096](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39395714096) SUCCESS. The real production JS `assignmentLesson` reads the RLS-filtered published student assignment list, refetches exact assignment+course+version, and reads one matching published lesson. It checks tenant, section, course, immutable pin, published status, digest syntax, content object and bounded HTTP reply. No arbitrary foreign row accepted.
- RED `99a95de3e4ed13b02b596f39dc1aa888c0551b4a` failed missing `renderLesson` named export, [Native PR 39395786906](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39395786906).
- GREEN render `66067f01396fae2327a583ff54d4f5a364118726` and browser route `b8b4972973776e691f50472f35edfb0b0e0653b0`: explicit assignment selection, read-only lesson panel, inert JSON `textContent`, loader/unavailable/no-selection state. Stale response cannot repopulate after sign-out or institution change.
- Latest [Native PR 39396357041](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39396357041) panel **18/18 Node tests passed** and `node --check` source checks successful. Windows/Ubuntu + [disposable Supabase PR 39396357029](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39396357029) are tracked independently at exact SHA.

## Trust limits
- The published `content_sha256` is based on PostgreSQL `jsonb::text` canonicalization, NOT recomputed in JS. This is metadata, not scientifically trustworthy Native/IPhreeqc provenance or academic evidence approval.
- No executed lesson workflow, interactive teacher-authored instructions UI, submission/grade portal, real browser, real student Auth, live storage or Unity/Player/XR testing in this increment.
- Original 66-task roadmap unchanged (15 VerifiedLocal, 51 Open, P06.02 formal next), English-only forever, offline Native authority and SessionLoad Hold, PR #1 open/unmerged. Production Supabase untouched.

## Subsequent transport reliability increment
- RED `37c9c53203e99b520c6ea624cb9bde9d9e54bf13` ([Native PR 39397215380](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39397215380)): two hanging Auth and scoped-logout requests failed independent watchdog because deadline absent. First RED `8aa0e2d042e17ed2066984f7674d20a7abba99bd` was replaced by a finite RED watchdog to avoid a permanently running CI test.
- GREEN `e2fae3f2eb1a48ad0d296d546f83f7a3b7d5667a`: `AbortController` deadline encloses HTTP request and bounded response parsing; same for `scope=local` sign-out, no false server logout. [Native PR 39397395483](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39397395483) panel passed **20/20** source-linked portal Node assertions. Latest exact SHA Native Windows/Ubuntu and disposable PostgreSQL results to be inspected separately. No real browser/local hardware verification.
