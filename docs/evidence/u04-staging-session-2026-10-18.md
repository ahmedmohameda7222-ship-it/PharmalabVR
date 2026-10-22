# U04 self-reported staging session — 18 October 2026

Owner Free project `hhbnlhceehgbskzdtorn`. **PARTIAL**. This is not Native source attestation or cloud synchronization.

- Source SQL `supabase/migrations/20261018140000_staging_session_registration.sql` commit `53d2ed57611e8dd35ef54194380bfd901f35252f`, blob SHA `d641393f73ce16a2398b9d5a5deee36fa043d800`.
- RED `74c6ca8ca99f7c4a898cbe08c2dd0fb35907aeb1`: PostgreSQL isolated Actions `39177988359` failed because `public.university_register_staging_session` was missing. The native tooling guard also failed for the same missing API.
- GREEN: real disposable Supabase PostgreSQL Actions `39178191954` push and `39178194667` PR, **17/17** migrations installed, **17** public tables RLS-enabled, **60** SQL DO blocks passed, final ROLLBACK, isolated containers destroyed.
- Applied live under owner authorization: `staging_session_registration_20261018` version `20261018235349`. Public SECURITY INVOKER wrapper/private SECURITY DEFINER function, authenticated-only EXECUTE and current Auth/membership checks. Live deny-role smoke passed and zero production Auth users, sessions, staging metadata or audit records exist. Direct client INSERT into `lab_sessions` remains denied.
- Role semantics: only active verified student in the requested university may create a new **pending revision-zero** lab_sessions row. The local session/native branch/science digest are self-reported *claims*. Exact request-ID replay yields the same row; changed replay rejects. An immutable audit records the claim explicitly with `attested:false`.

**Still missing:** real Native command receipt bytes/verifiable origin, trusted cloud attestation, session revision promotion, private academic evidence object, signed sync acknowledgment, test Auth users and HTTP upload pilot, Unity Editor/Player/XR. U04, P11.01, G-U1 and G-R1 remain OPEN. Application English-only and PR #1 unmerged.
