# Final evidence review

The detailed delivery review is maintained in [`docs/review/final-review.md`](../review/final-review.md). This evidence-side entry exists because P15 requires the final review under `docs/evidence/` as well as the broader review record.

Verified locally on functional revision `83d862780e7fe043a3026c1c3930999fcc09c9ee`:

- native Release: 52/52 cases, 565/565 assertions, CTest 1/1;
- Python tooling: 6/6;
- independently labelled ideal numeric reference: 40 cases;
- Unity 6000.3.25f1: EditMode 18/18 and PlayMode 3/3;
- WindowsDesktop: build, actual start and Lab entry passed;
- WindowsVR: build and no-runtime start passed, with expected `XR_ERROR_RUNTIME_UNAVAILABLE` diagnostics;
- AndroidVR: ARM64 IL2CPP APK build and package-content inspection passed.

The per-case acceptance record is [`acceptance-95.json`](acceptance-95.json), summarized as 42 Passed, 49 Partial and 4 EvidenceMissing. Artifact paths, sizes and SHA-256 values are in [`artifact-manifest.json`](artifact-manifest.json). Physical VR, Android device execution, long-run target performance, participant studies, calibration, optical/tracer/mixing validation, and experimental scientific validation are not claimed. PR #1 remains open and unmerged.
