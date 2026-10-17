# P02.02 — revision-bound command receipt evidence

Functional code SHA: `a62c3fc5d242e23ed94bbf357f4ab4d5799e2dea` (one Unity folder `.meta` whitespace cleanup follows in the evidence commit). Windows Unity Editor `6000.3.25f1`, Research aqueous-six profile.

`LabCommandService` captures only the touched vessel revisions from a native snapshot at intent time and submits them through `CoreSession.SubmitOrdered`. It returns the native terminal receipt; it does not calculate or copy material inventory. Stock preparation in the actual `CoreDriver` now uses this facade instead of a hard-coded revision zero.

`P02_02_TouchedRevisionFacadeRejectsStaleAndIgnoresUnrelatedVessel` failed to compile before the facade existed. In Unity PlayMode with the real Native/IPhreeqc context it then accepted a source preparation after a different vessel advanced, replayed an identical command identity as `Accepted`, rejected the same identity with altered payload as `CommandIdentityConflict`, and rejected a new command against the captured old source revision as `StaleRevision`. The earlier C01/A02/R05 native tests independently cover the same authority semantics and import receipts. PlayMode at this code: 16/16, XML SHA-256 `B00F1E6A20DBA30EDEFF47C05DDFE0B01D268AC81EE841DCE6ACE7DB40B4D2B5`.

WindowsDesktop build: `Build Finished, Result: Success.` in `artifacts/logs/unity-WindowsDesktop.log`. Runtime assembly SHA-256 `EFAB81D19B9EDFD4912E7D6C9F1BDF9DA7049E652FDBF092E3824ED162D4E44B`. The EXE launcher hash is unchanged and is not used as evidence of the new C# code. This task verifies command facade semantics in PlayMode and a compilable Player; it does not verify a complete GUI, VR interaction, or the P02.03 material ledger.
