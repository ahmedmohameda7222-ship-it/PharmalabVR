# P06.02 — same-driver Native restoration must invalidate stale panels

**21 October 2026. Code-first PARTIAL; Unity Editor/Player/XR NOT_RUN. PR #1 open/unmerged.**

## Defect and dependency
`CoreDriver.ReplaceSession(CoreSession)` and `CoreDriver.RestartCheckpoint()` retain the Unity `CoreDriver` MonoBehaviour while changing the authoritative Native session or checkpoint state. The preexisting P06.02 `ScreenRouter.Configure` reset only when a *different driver object* was attached. Imported Native branch/revision could therefore leave old Settings/Inspect, selected vessel and `Saved` status visible before the new Native snapshot. Native emits `NativeSessionRestored` **before** `PublishSnapshot`.

## TDD
- RED `1a28c5ba103e5f035170675223b9916a5f5f920c`, [PR Native 39544692436](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39544692436): actual source-linked `ScreenRouter` in .NET synthetic-event fixture fails `NativeSessionRestored reused a stale Settings, Save, or selected vessel`, exit 134.
- GREEN `e765e3a2b7cf9042a40b71c714bcfd9bf2f85d34`: subscribe to `CoreDriver.NativeSessionRestored` with existing attach/detach lifetime, invalidate stale selected entity/save/history and conservatively show `Hold` pending authoritative snapshot. No chemistry/Continue/receipt changes.
- Verified functional PR [39544907113](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39544907113) C# 313 assertions and six mutation RED/restore GREEN, Windows and Ubuntu Native SUCCESS; disposable Supabase [39544907092](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39544907092) SUCCESS.
- Real `PanelStateTests.P06_02_NativeSessionImportClearsStaleSettingsAndSavedPanel` authored at `6d28a75e535479bb072b9ba90fd24f72980a2370`; uses actual pinned Native `CoreSession.ExportSession/ImportSession` and Lab scene `CoreDriver.ReplaceSession`, tests genuine SessionLoad Hold. **NOT_RUN**. Headless CoreDriver stub is not Native.

## Remaining
Review/Compare caches in `SessionController` still require an independently source-tested invalidation path for accepted checkpoint restarts. Full hardware/Unity testing deferred to owner Codex. Canonical P06.02 remains Open; 66 original statuses 15 VerifiedLocal/51 Open, G-R1 and G-U1 Open. Production Supabase untouched, English-only permanent, no Unity Cloud CI, no PR merge.

## Follow-up: event missed while panel disabled
- RED `09aaf888b28a52b757e21b8005be84694719f3dc`, [PR 39545814205](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39545814205): actual `ScreenRouter` OnDisable → simulated `NativeSessionRestored` → OnEnable retains old Settings/Saved/Inspect, proving bare event subscription is insufficient across Unity panel reactivation.
- GREEN `c3ef4c466a9ae07443096e3d1f6d1a666c5e37bf`: bounded process-local `NativeRestorationEpoch` in actual `CoreDriver` incremented only at accepted restore; `ScreenRouter.Refresh` reconciles missed epochs and conservatively re-enters Hold. [Native PR 39545974966](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39545974966) panel passed **317 C# source-linked assertions and six mutated RED/restore GREEN checks**. Latest exact-SHA matrix and disposable Supabase separately tracked; actual Unity PlayMode `P06_02_DisabledPanelReactivatesIntoNativeSessionLoadHold` authored `6a516dc`, NOT_RUN. This epoch is not a material revision or scientific attestation.
