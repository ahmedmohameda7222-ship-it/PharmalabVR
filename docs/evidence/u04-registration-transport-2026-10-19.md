# U04 student session registration + staging transports

**Date:** 19 October 2026. **Scope:** untrusted U04 cloud quarantine only, not Native attestation.

### Test-first and actual verification
- SQL test RED `74c6ca8ca99f7c4a898cbe08c2dd0fb35907aeb1` reproduced absent `university_register_staging_session` on local PostgreSQL `39177988359`; GREEN functional source `53d2ed57611e8dd35ef54194380bfd901f35252f`. Push `39178191954` and PR `39178194667` both succeeded: **17 migrations, 17 RLS public tables, 60 DO assertions, final ROLLBACK**.
- Owner Free migration `staging_session_registration_20261018` version `20261018235349` (SQL blob SHA `d641393f73ce16a2398b9d5a5deee36fa043d800`) was already installed when checked; never replayed. Live anonymous/unassigned smoke passed; still **0 Auth users, 0 lab sessions, 0 staging objects/metadata, 0 audit events**. No Security Advisor WARN.
- C# stages: RED transport `25fdb5b` missing source, GREEN `970323e`; RED registration `2627232` missing RPC client, GREEN `29a1696`. Real source-linked .NET 8 fake HTTP assertions passed on push `39179130630` and PR `39179134811`; **all Windows/Ubuntu Native and panel jobs SUCCESS** on that functional SHA.
- Unity `.meta` files added, with no integrated Unity Editor, Player or device execution.

### Trust boundaries still outstanding
Database session registration is a student self-report and revision zero; it does not validate a Native session or scientific package. HTTP 201 from the staging Edge is only `StagedUnverified`. Neither C# transport removes a pending outbox item nor uses a service role key. Native receipt provenance signing, server-side verification, trusted academic evidence promotion, signed sync acknowledgment, real student Auth/HTTP journey and Unity final campaign are not implemented/verified. Maintain PR #1 unmerged, English-only, G-U1/G-R1/P11.01 OPEN.
