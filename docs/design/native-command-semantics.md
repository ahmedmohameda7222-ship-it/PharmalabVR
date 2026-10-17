# Native command semantics

The version 1 JSON ABI keeps material mutation in the native `StateExecutor`. Every ordered command receives a terminal `CommandOutcome`; an accepted or rejected next sequence is consumed, an identical retry returns the recorded outcome, and a changed retry conflicts.

The first research profile makes these internal choices for command payloads that the frozen contract names but does not otherwise shape:

- `SetActuator`: `{toolId, actuator01, expectedActuatorRevision}`. A successful command increments `actuatorRevision` and clears the unconsumed live sample. It does not invalidate the tool's registered geometry revision.
- `DisposeContents`: `{sourceInventoryId, sinkInventoryId}` with the source in `expectedMaterialRevisions`. The complete homogeneous source parcel is transferred atomically to the registered sink; an empty source rejects.
- `RinseTool`: `{toolId, rinseSourceInventoryId, wasteSinkId, quantity:{basis,value}, expectedInventoryRevision}` with the rinse source revision. The declared rinse parcel passes through the tool to the explicit waste sink. The first profile retains no hidden rinse liquid; it increments the tool-inventory revision and clears the live sample without invalidating the registered geometry revision.
- `CreateCheckpoint` and `RestartCheckpoint`: `{checkpointId}`. A checkpoint captures native material/time state, registered tool state and current application mode. Export/import preserves checkpoints and ordered command receipts. Restart clears unconsumed live samples, preserves sample-sequence watermarks, and enters `CheckpointRestart` hold so material cannot resume before fresh neutral tracked baselines and explicit `Continue`.
- `BeginModeChange`: `{mode:"Desktop"|"VR"}`. It clears unconsumed samples and enters `ModeChange` hold. Unity changes the active rig only while native transport is held, then provides fresh neutral baselines before `Continue`.

Tools expose separate tip, residual and in-flight inventories in snapshots and exports even when empty. This makes the conservation boundary explicit and allows later calibrated parcel-retention profiles without changing schema version 1. The research burette currently has no retained parcel model, so accepted live deliveries debit the registered source and atomically allocate receiver captures plus all uncaptured/capacity overflow to its named sink.
