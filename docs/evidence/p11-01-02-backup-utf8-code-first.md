# P11.01 / P11.02 — Recorded fallback and strict Native-save UTF-8 (2026-10-16)

**Code-first scope only:** P11.01/P11.02 remain Open; no Unity Editor/Player/XR run on these changes. PR #1 open/unmerged; English-only product.

## Confirmed backup-only recorded Review (P11.02)

- `RecordedSessionReview.OpenRecoverable` uses the existing bounded, strict recorded-snapshot validation on the primary `.plv.json`, then the confirmed `.bak` on recoverable read failure. The same safety rule is now shared by `RecordedRunComparison.CompareSaved`.
- A Native scientific-package mismatch continues to block **live** import. `SessionController.ContinueFrom` can instead use the validated recorded-only backup, showing historical observations without calling the science provider or replaying a command.
- RED `d9c71f0fbf415c99debee45b6e3fa0713fc55d6b` lacked a real disk-backed comparison fallback; GREEN `8e7941405abcaf2e87140bc7649cc8247127f93f` linked source to the .NET contract. Push [38864655626](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38864655626) SUCCESS, with PR result checked separately.
- RED `c6b67eee0c9d2ffa9cdff33ee42605ebe57a747c` headless compile failed without `OpenRecoverable`; GREEN `a2389f667749edd6b3d5c46852547a80e8399501`: [push 38864964234](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38864964234) and [PR 38864969515](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38864969515) SUCCESS for Native Windows/Ubuntu and headless. Unity EditMode backup-only/missing-model test authored at `4356a723bdbd74f371ee1d35d872527eb96fe98b`, NOT_RUN.

## Strict UTF-8 load boundary (P11.01/P11.02)

- Previously `SaveService.LoadOne` and `RecordedSessionReview.Open` used permissive UTF-8 decoding. Corrupt bytes could become replacement characters in a Native identity or scientific field instead of a rejected record.
- RED `41dd54d81af57f37cc9d4b21384f338c5c14d6d6`: real SaveService headless test fails with **"Invalid UTF-8 Native save was accepted rather than recovered from backup"** ([push 38865238229](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38865238229)). A recorded-only malformed-UTF8 negative assertion also added.
- GREEN `ebfef7fdbe9b7a1fa78d51444da1317530ecf341`: both production read paths use strict UTF-8. Invalid Native bytes trigger the existing recoverable read error classification; recorded Review reports invalid data. The first green candidate uncovered a **test fixture ordering error** (the backup-only fixture was intentionally corrupted too early); the fixture was corrected in `c2151f27cee01ffec0341da124be50cc3ab58ddb`. CI conclusions for final source must be confirmed; do not claim Unity verification from headless tests.

## Acceptance boundaries

This work is not a durable native on-disk journal/crash-recovery implementation, not an authenticated archive format and not portable transaction import. Real offline Native interruptions, disk-full failure, scientific-package changes, Unity load/review screens and XR/device action-driven acceptance remain Open. No release gate advanced.
