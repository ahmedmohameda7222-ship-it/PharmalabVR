# U04 — Source-linked staging status recovery (20 October 2026)

**PR:** #1 on `feat/foundation-vr-first`, intentionally OPEN / unmerged. **Gate G-U1:** OPEN. Scientific Native authority and English-only design unchanged.

## RED → GREEN
- RED source `bf485a19f6d039e80aab0c80ae68dd5b897e6da2` introduced client reconciliation checks into `tools/outbox-contract/Program.cs` for authenticated status, exact artifact/digest/size matching, disallowed forged synced claim, denied role, timeout and server-secret key. Exact push `39203647403` and PR `39203652302` failed panel build with `CS2001`: missing `UniversityStagedEvidenceStatus.cs`. Native Windows and Ubuntu jobs were successful at this RED SHA; PostgreSQL tenant workflow successful.
- GREEN `1264c259e66ab11c9d7ada413a96902dd4cccdf6` supplies `Unity/Runtime/UniversityStagedEvidenceStatus.cs` (actual path `unity/Assets/PharmaLabVR/Runtime/University/UniversityStagedEvidenceStatus.cs`). Calls only `POST /rest/v1/rpc/university_staged_evidence_status` with student's current Auth bearer and public gateway apikey. Strict canonical tenant/artifact UUIDs, recognized HTTPS Supabase endpoint, bounded JSON response and real evidence bytes SHA-256/length check. Only `state=staged_unverified` can be returned by adapter, never a sync receipt. Fixed duplicate compilation inclusion in headless `OutboxContract.csproj`.
- Headless `.NET 8` source-linked push `39351521766` job `118396538761` succeeded with **75 assertions**; PR `39351525515` panel succeeded. Windows and Ubuntu Native jobs SUCCESS on functional SHA `1264c259`.
- `8d324078c7355f03a13c8eed8f1c774ea61b6841` adds stable Unity asset `.meta` and 3 actual NUnit EditMode regression tests for matching staged-only metadata, digest/size and forbidden/timeout failure, and disallowed secret key. These sources are **NOT executed in Unity**; latest full Native CI on this test-only SHA may still be running.

## Production unchanged
Supabase Free project `hhbnlhceehgbskzdtorn` has the stage-only Edge endpoint and student status RPC from prior increments. Latest live SQL: zero Auth users, staging rows, privately staged objects and attested academic artifacts. Security Advisor has **no WARN**, only two intentional INFO for deny-by-default audit and quarantine tables.

## Remaining
No enrolled real student/Auth HTTP transaction, Native proof-of-origin, promotion from quarantine to protected academic evidence, signed acknowledgment, unity integration, or Editor/Player/XR verification. Staged status can never clear `UniversitySyncOutbox.Pending()`. No release acceptance or G-U1/G-R1 promotion.
