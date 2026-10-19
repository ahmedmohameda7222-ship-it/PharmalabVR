# U04 stage retry admission — exact RED/GREEN and Free deployment (19 October 2026)

## Scope
This increment only recovers a **previously quarantined, unverified** upload after a lost HTTP response. It does not attest Native origin, sign synchronization acknowledgment, create `session_artifacts` or approve grades. Do not lift G-U1/G-R1 or merge PR #1.

## Tests and commits
- Node RED `ef4ec95545ca27cd87936fcb73c63e93f53eacc8` added four test cases for exact metadata+byte replay, tenant/student/session/artifact isolation, mismatched private object and Storage duplicate error classification. Push `39180682381`, `panel-routing`, failed in U04 replay regression while `reconcile.mjs` was absent. 
- Node GREEN source `6aefc31a49c6c4b935d136482c2a213de574dffa` introduced `reconcile.mjs`, and Edge `index.ts` now responds 200 only after `evidence_upload_staging` and private `Storage.download` both match the same request. Missing metadata, missing bytes and collisions return conflict; neither upload nor retry uses upsert.
- C# staging client RED `de53d62cb78576ff5952e35d99609ad1f57f76c4`, push `39180925392`, headless outbox test failed exactly `Verified exact replay was rejected as if first upload failed`. Test JSON escape and expected request count fixes: `19a87cd5770b7246ae009c52b7b878064912f6d1` and `88e70c86e9771057c8ead5161d2f3408650659f2`. Functional GREEN `d1e098fa4f3e85393dfc64ebe64dc421836fc583` permits HTTP 200 only with staged/unverified and explicit `alreadyStaged:true`, never Native sync. GitHub push `39181138169` panel job `117833934706` completed **SUCCESS**, including all Node U04 tests and C# outbox contract.
- Unity EditMode tests `unity/Assets/PharmaLabVR/Tests/EditMode/UniversityStagingTransportTests.cs` authored at `77c48709b76b1facd579257825cd74732a023ff0`: tests exact retry and collision while holding outbox. **Unity Editor/Player/XR NOT_RUN**.

## Live deployment
- User-authorized Supabase Free project `hhbnlhceehgbskzdtorn`: `student-evidence-upload` Edge **ACTIVE v4**, `verify_jwt=true`, `ezbr_sha256=8fd3a6335dc721a78ac8be6210457cbc2800609e5be94662ba67f950faf90c5b`. Deployed from functional source SHA `d1e098fa4f3e85393dfc64ebe64dc421836fc583`. Supabase deployment required explicit `import_map_path=deno.json` after the first v4 request inherited a stale v3 absolute import-map path.
- Fresh SQL verified **0** Auth users, registered lab sessions, staged metadata/objects, academic artifact rows and audit events. Staging bucket private, authenticated INSERT denied, Security Advisor 0 WARN (two expected INFO for deny-all private audit/staging RLS tables).
- A real authenticated HTTP upload, a retry through hosted Storage, genuine Native receipt attestation, signed authoritative server ACK and end-to-end Unity/VR tests remain **NOT_RUN / NOT_IMPLEMENTED**. The scientific solver remains local; product is English-only.
