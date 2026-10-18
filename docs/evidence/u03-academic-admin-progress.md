# U03 academic tenant administration — applied source increment

Owner project: `hhbnlhceehgbskzdtorn` (Supabase Free). 18 October 2026. Original Native authority and all release gates unchanged.

| Function | Private implementation | Remote migration version |
| --- | --- | --- |
| `university_enroll_student` | Verified active student + admin + same-tenant section | `20261018053627` |
| `university_create_course` | Active admin + validated code/name/term | `20261018054744` |
| `university_create_section` | Active admin + existing course in same institution | `20261018055029` |

All expose SECURITY INVOKER public delegates to `plv_private` SECURITY DEFINER functions. No direct client INSERT on academic tables. Idempotency uses the audited request UUID; conflicting exact replays fail. Live rollback smoke for anonymous and an unassigned synthetic JWT claim returned without SQL errors. Post-run database counts remain zero for Auth users, institutions, courses, sections, enrollments and audit rows; Security Advisor has no warning findings.

**RED/GREEN evidence:** enrollment RED `21aac6e`/GREEN source `c8da722b`, fixture syntax repair `1c24628`; course RED `98d7c89`/GREEN `148345c`; section RED `4d42bcd`/GREEN `8b44ebd`. Source/tests/SQL are committed; full positive seeded Auth/RLS checks were not run on production. GitHub isolated local Supabase PostgreSQL workflow introduced at `b073563` and remains unverified until its actual job result is inspected. Do not claim full U03 acceptance or university student pilot. Unity final campaign still NOT_RUN.
