# U04 pending registration HTTP input/response guardrails — 2026-10-19

**Branch** `feat/foundation-vr-first`, PR #1 unmerged. Code-first work; no Unity integrated execution.

## RED → GREEN evidence
- Oversized 8 KiB fake gateway HTTP 200 body: RED `f89394d12a4d00f37a184a21144ff94eeee680d8`, PR Native job `39196566555` panel FAILED exactly on uncaught `System.IO.InvalidDataException: Registration returned no canonical UUID`. GREEN `206f5f33f5ecf948e2e5d0b73153a89e3555ba9f` changed `RegisterPendingAsync` to `HttpCompletionOption.ResponseHeadersRead` plus a 128-byte header/body cap; `39196688690` panel SUCCESS.
- Trailing-LF identifiers: RED corrected commit `6434ba813294c810d52e0ba3f988268c94756bdf`, PR `39197022440` panel FAILED exactly with `Trailing LF in institution UUID reached the registration client`. GREEN `3bfab5960591b115ae9670861b81db0617098a44` changed the four `^` / end-of-line .NET regex anchors to absolute string boundaries, preserving strict base64-compatible bearer, canonical student identifiers and SHA digest shapes; `39197202120` panel SUCCESS.
- Explicit NUnit EditMode tests added `fcf0a865a3f366a0f4eeb3017d63e18ad6226b61` and `cdd713d48f4f33275499e1cc058bd1d519faa182`; their source is **NOT_RUN** under Unity. All above headless tests are source-linked to production C#.

## What these assertions do NOT prove
Neither a bounded RPC response nor validated identity input constitutes Native receipt origin attestation, Storage academic promotion, grade eligibility, or a signed cloud acknowledgement. No production student exists in owner Supabase project; the staging API remains quarantined-only and the offline Native outbox must not be cleared. End-to-end Unity Editor/Player/XR final campaign and release gates G-U1/G-R1 remain OPEN. Native platform results need exact-SHA validation independently.
