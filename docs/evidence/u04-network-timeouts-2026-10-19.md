# U04 offline HTTP timeout fail-closed — 19 October 2026

This increment targets **network cancellation/timeout handling only**. No Supabase schema or Edge deployment was changed.

**Upload:**
- RED `170da96385304b9c7c58c5303817d5b469e46fc9`, push `39193786074` panel `117872034478`: uncaught `TaskCanceledException` from fake HTTP handler.
- GREEN source `cb604b67451bc7b15ca78da55eb4020ad04a7160`: `UniversityStagingTransport.UploadAsync` returns `Rejected` for a timeout where the provided cancellation token was not canceled; outbox remains Pending, not synced. Explicit caller cancellation still propagates. Exact push `39193961521` panel `117872560711` SUCCESS (59 source-linked C# assertions).

**Pending session registration:**
- RED `8365bae6ba8dac5826f44cab5c9d4c1ffd52de50`, push `39194108062` panel `117873000737`: uncaught `TaskCanceledException`.
- GREEN `2cb33b3dd4f99625c091614381282eca46ea5302`: `UniversityStagingSessionRegistration.RegisterPendingAsync` returns `null` (no cloud identity) on HttpRequestException and non-caller-cancelled OperationCanceledException. Exact push `39194247743` panel `117873426273` SUCCESS, **61 source-linked C# outbox assertions**, U04 Node suites **7+10+5+4 cases all passing**. Unity EditMode timeout test source exists, but is **NOT_RUN**.

Neither response is Native receipt attestation, durable sync success, or academic evidence. A caller should retry safely using the stable request UUID/Artifact UUID after restoring Auth, and must never clear the offline Native queue based on a timed-out HTTP request. No claim of power-loss atomicity, complete retry orchestration, real production student upload, or final Unity/XR verification. G-U1/G-R1/P11.01 and PR #1 remain open.
