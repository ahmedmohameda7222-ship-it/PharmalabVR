# U04 Native receipt/source binding and producer — 20 October 2026

**Code-first PARTIAL. Do not call this Native attestation, academic sync or Unity verification.**

## Regression and implementation
- Incoming live HEAD: `0c7e3f912a6582a3f0099fddcf1adca06f023bd1`. Existing U04 randomized create-only outbox stage preserved.
- RED `1544d729189820004a1e0ffc3b2395ebfac5d4e8`: arbitrary non-Native artifact bytes passed all matching caller-provided digests. PR Native run 39361159592 panel failed exactly `Hash-only outbox admission accepted bytes without a Native export identity`.
- GREEN `6d40253e2904369367ec2c4591614f18b4972fc4`: `UniversityEvidenceIntegrity.EnqueueVerifiedEvidence` rejects forged/non-JSON receipts, mismatching export branch or sequence, scientific identity/digest drift and inconsistent receipt outcomes. It matches command and terminal receipt to the same Native export and only then stores Pending. All existing staging transport remains quarantined.
- First exact GREEN: push Native [39361705456](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39361705456), PR Native [39361711456](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39361711456), PR disposable Supabase [39361711509](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39361711509) SUCCESS. C# U04 80 headless assertions, Native Ubuntu/Windows and isolation CI passed at this SHA. No Unity Editor run.
- Producer RED `226b578e794a76c33f618bf232da22cf78dda7e4`: PR Native [39362369755](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39362369755) failed missing source file `UniversityNativeReceiptProducer.cs` after contract declared required behavior.
- Producer GREEN `4ec701b95c13b9e70a0d9fba68f2bdd2a9067cdc`: derives the tenant-scoped upload intent hashes from a completed outcome plus a **single actual** `CoreSession.ExportSession()` call (no Native commands are submitted), checks branch/sequence, and enqueues locally before any HTTP. Headless test invokes the real production producer with an explicitly synthetic `CoreSession` stub; **86 outbox assertions PASS** at [PR 39362572487](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39362572487) panel. [Disposable Supabase 39362572493](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39362572493) SUCCESS at inspection. Exact remaining jobs tracked in GitHub.
- Actual Native-backed Unity test source `75336bb8e9f71c94fcc7ee29ed61f0a0ba260e90`: `UniversityNativeReceiptProducerTests`. **NOT_RUN** in Unity, Windows Player or XR.

## Trust model and boundaries
- The program may compute SHA-256, verify branch/command sequence and package bytes, but a malicious client could supply forged data; these checks are local consistency only. No secret attestation key is stored in Unity. Server `staged_unverified` is not accepted academic evidence, signed confirmation or cleared Native outbox.
- No owner live Supabase insert, SQL migration, Edge deployment, Auth signup, production email or production pilot was performed. Existing 18 live migrations/v5 upload/v1 status are not altered.
- Original 66 tasks still formal 15 VerifiedLocal / 51 Open, next P06.02. U04 remains PARTIAL; G-U1, G-R1 OPEN. Unity Editor/PlayMode and Desktop/VR/Android actual testing remains NOT_RUN. PR #1 open/unmerged.
