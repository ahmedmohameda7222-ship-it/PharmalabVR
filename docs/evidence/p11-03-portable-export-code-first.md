# P11.03 — Portable export-only archive | 2026-10-16

**Formal task: Open/NotReviewed. Unity Editor/Player/XR: NOT_RUN.** English-only UI; PR #1 unmerged.

This bounded implementation writes a single `.plvpack` containing actual Native session JSON plus validated, current-branch, event-linked notebook notes. It has strict UTF-8; bounded Native (16 MiB), note (1 MiB), and archive (24 MiB) sizes; Native branch/event identity validation; a SHA-256 corruption checksum; and write-through staged atomic replacement. The checksum is **not** an authentication signature.

TDD: source RED `283c764` (absent archive class), real headless failure in [run 38835618652](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38835618652). Codec `eecfc7f`, regression correction `b4fdfca`; [push 38835970031](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38835970031) and [PR 38835975327](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38835975327) all three headless/Native jobs SUCCESS. Headless tests cover round-trip after sidecar deletion, corrupt checksum rejection, foreign-branch rejection, future event cutoff rejection and an empty notebook.

Shared-panel route RED `97777da` failed on absent `PanelAction.ExportPortable` ([run 38836029552](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38836029552)). Functional `2516188` adds explicit Notebook/Pause export, Desktop controls, XR Notebook export and verified-path reporting without marking ordinary Native Save. Author-only Unity PlayMode tests at `2516188` and `5dcca1e` cover Desktop and XR Review → Notebook → Export. Full Native CI results must be checked separately.

**Ruling:** The portable archive is **export-only**. No transactional import is implemented: never claim that `.plvpack` can restore an active Native session and sidecar, bypass database verification, or offer recomputed Review. CSV, two-run Compare, replay/fork and final integrated Unity execution remain outstanding. Continue code-first implementation while P11.03 and release gate G-R1 remain Open.
