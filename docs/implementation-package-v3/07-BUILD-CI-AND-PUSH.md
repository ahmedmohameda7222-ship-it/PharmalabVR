# Build, CI, evidence and Git delivery

## Pins and tool discovery

Unity Editor baseline `6000.3.25f1` (official release changeset `e1dba0a9aba4`). Packages: XRI 3.3.2, OpenXR 1.17.1, XR Management 4.6.1, Input System 1.20.0, URP Editor-matched 17.3 series, Unity Test Framework 1.6.0. Use the Editor's actual bundled URP patch and record/lock it rather than fabricating a patch. Source pages list these released/compatible series; the combination is not claimed pretested. Import/build is the gate. If an available later 6000.3 LTS patch resolves a reproduced blocker, record the exact old/new identity and regression evidence; no arbitrary migration to another major engine.

Native baseline: C++17, MSVC 2022 v143 x64 on Windows, CMake ≥3.24, Python 3.11+, Unity-provided Android SDK/NDK/JDK for this Editor, Android ARM64 IL2CPP. Record actual CMake/compiler/NDK versions in the reproducible manifest. Doctest 2.4.12 and nlohmann/json 3.12.0 are chosen lightweight native test/JSON dependencies; pin actual source hash/archive digest and notices before use. Do not fetch an unpinned main at build time. The bundle supplies pinned IPhreeqc source; preserve it as a deterministic vendor directory.

Windows Desktop native DLL importer x86_64/Editor/Windows; Android `.so` Android ARM64 only; do not accidentally include the Windows DLL in Android or load an ARM plugin in Editor. Set Android native target position-independent, static dependency linked into shared wrapper, verify exported C symbols and IL2CPP runtime linking. No Fortran/COM requirement in the product path. Desktop startup does not initialize OpenXR until selected.

Android initial app identifier `com.pharmalabvr.lab`, version `0.1.0-research`, ARM64-only, IL2CPP, SDK levels chosen from installed compatible Unity/vendor requirements and recorded before build. Android minimum target is a build/runtime compatibility gate; do not use a guessed API value to claim support. Generic Windows OpenXR profiles cover available controllers; Quest/PICO build profiles isolate required vendor features instead of putting both vendor loaders into one Android build blindly. No store distribution is included.

## Exact callable build functions

Implement static methods under `PharmaLabVR.Editor.BuildPipelineEntry`:

- `AllAssets()` — deterministic create/update research profiles, meshes, materials, prefabs, scenes, input/XR project settings; repeat run does not duplicate objects/GUIDs.
- `WindowsDesktop()` — Windows x64 `artifacts/WindowsDesktop/PharmaLabVR.exe`, desktop-compatible boot and optional XR mode disabled unless selected.
- `WindowsVR()` — Windows x64 `artifacts/WindowsVR/PharmaLabVR.exe`, VR-primary selection/rig with Desktop fallback.
- `AndroidVR()` — alias for initial Quest OpenXR profile, output `artifacts/AndroidVR/PharmaLabVR-research.apk`.
- `AndroidVRPico()` — isolated PICO OpenXR build profile, output `artifacts/AndroidVRPico/PharmaLabVR-pico-research.apk`; dependency source/compatibility proof needed before claiming this built/supported.

Build functions set explicit scene order Boot/Lab/Review and check native plugin/config assets before invoking BuildPipeline.BuildPlayer. Throw/fail exit on BuildResult failure; never just print an error with exit 0. Scene/content generation is Editor code, not an after-the-fact manual step hidden from reproducibility. Preserve `.meta`, input actions and serialized settings.

## Commands the implementer must implement/support

Native from repository root:

```text
cmake -S . -B build/native -DBUILD_TESTING=ON
cmake --build build/native --config Release
ctest --test-dir build/native -C Release --output-on-failure
python science/reference/verify_ideal_fixtures.py
```

Use shell-native variables for discovered Editor path; example PowerShell invocations below use `$taskUnityEditor` and resolved absolute project/evidence paths. Create evidence directories first; inspect process exit code and test XML, not log phrases alone.

```powershell
& $taskUnityEditor -batchmode -quit -projectPath $taskUnityProject -executeMethod PharmaLabVR.Editor.BuildPipelineEntry.AllAssets -logFile $taskAssetLog
& $taskUnityEditor -batchmode -projectPath $taskUnityProject -runTests -testPlatform EditMode -testResults $taskEditXml -logFile $taskEditLog
& $taskUnityEditor -batchmode -projectPath $taskUnityProject -runTests -testPlatform PlayMode -testResults $taskPlayXml -logFile $taskPlayLog
& $taskUnityEditor -batchmode -quit -projectPath $taskUnityProject -executeMethod PharmaLabVR.Editor.BuildPipelineEntry.WindowsDesktop -logFile $taskDesktopBuildLog
& $taskUnityEditor -batchmode -quit -projectPath $taskUnityProject -executeMethod PharmaLabVR.Editor.BuildPipelineEntry.WindowsVR -logFile $taskVrBuildLog
& $taskUnityEditor -batchmode -quit -projectPath $taskUnityProject -executeMethod PharmaLabVR.Editor.BuildPipelineEntry.AndroidVR -logFile $taskAndroidBuildLog
```

Do not add `-quit` to the test commands before Unity completes its test runner. Use a display-capable environment where PlayMode/render tests need it; `-nographics` cannot prove visual rendering or headset output. Add a Player smoke/journey harness writing structured assertions and exit result; process launch alone is not a test. Run actual screenshots from rendered application and label simulator versus physical.

Native plugin staging is in build scripts, with hash/architecture verification. Android CMake uses the Editor's NDK toolchain and `ANDROID_ABI=arm64-v8a`; do not copy the x64 library into `.so` destination. Tests record different activity/CPU floating-point tolerance, not bitwise output equality.

## CI structure

`.github/workflows/native.yml`: run on pushes/PRs; Windows x64 + Ubuntu 24.04 headless native CMake/CTest/reference fixtures; checkout pinned vendor, build/test, retain JUnit/log artifacts on failure. Linux testing verifies portability, not a marketed Linux Unity client. Use minimal read-only workflow permissions unless the action needs more; no repository-wide write token for tests.

`.github/workflows/unity.yml`: support the existing authorized licensed Unity runner, using the same scripts/entry points. A self-hosted Windows runner labeled `unity` is the baseline when available. Gate scheduling on a repository variable reflecting real runner availability; retain a separate visible availability report when unconfigured. Hosted licensed GameCI is optional only after its action/source/secret setup is verified; do not invent secrets or sign into paid services. Unity workflow can be PendingEvidence/BuildNotVerified; never “passes” because every real step was skipped. Native CI must not depend on Unity licensing.

Artifacts: exact commit SHA, engine/package/adapter/database/native hashes, test XML/logs, build manifests, real screenshots and benchmark JSON/CSV. Build outputs go in CI artifacts or user-approved attached files, not source commits. `.gitignore` excludes Unity Library/Temp/Logs/UserSettings, native build folders, `.env`, signing keys, journals/saves, original source docs and player binaries. Include generated source assets/`.meta` and correct license notices. Use text serialization for scenes/prefabs and `.gitattributes` appropriate for Windows. Do not commit private activation logs.

## Git workflow for this repository

User authorizes pushes here. No force push, reset of unrelated work, automatic merge or publication to stores.

1. Clone/fetch `https://github.com/ahmedmohameda7222-ship-it/PharmalabVR.git`; inspect actual refs/default branch and working-tree changes. Recheck since the observed empty repository may have changed.
2. If still empty: create local `main`, add only bootstrap README/.gitignore/.gitattributes and minimal package authority/provenance, commit `chore: initialize PharmaLabVR foundation`, push `main` to origin. This establishes a PR base, not a completed product claim.
3. Create `feat/foundation-vr-first` from actual main. If that exact branch already belongs to this work, continue it after inspecting its head; do not replace somebody else's branch.
4. Implement/test/commit P tasks on that branch. Do not commit `source-documents/` originals merely because they are in the ZIP. Vendor source retains its own notices; generated concept labeled as such. No secrets or unapproved purchased assets.
5. Before push: `git status`, `git diff --check`, exact diff scope, test/build report and committed HEAD. Push `git push -u origin feat/foundation-vr-first`.
6. Create PR to main using available GitHub tooling. Title `Build VR-first PharmaLabVR foundation with desktop support`; describe actual final implemented scope, real validation and missing physical/experimental evidence. For multiline gh bodies use a saved file and `--body-file`; no escaped shell reconstruction.
7. If Codex attach_artifact exists, attach the created PR URL. Fetch/check statuses for the exact branch head; fix in-scope code/CI failures and push new commits. Do not merge or enable auto-merge.
8. Final handoff reports remote URL, branch, full SHA, PR URL, CI status at that SHA and artifact links/paths. Network/auth rejection is explicit; preserve local commits and exact safe retry command, not a fabricated successful push.

No user messaging to other chats is included; return the review handoff to this implementation chat. The user will send the final work to the planner.
