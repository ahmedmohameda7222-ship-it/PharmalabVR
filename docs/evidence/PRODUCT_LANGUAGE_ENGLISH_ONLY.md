# PharmaLabVR — English-only product language decision

**Date:** 2026-10-16. **Authority:** direct user product requirement. **Applies to:** Boot, Setup, Desktop, Lab, XR world panels, tools, Review, status, captions, errors, and all shipping application resources.

The application must be **English-only**. Do not add Arabic user-interface strings, Arabic font assets, RTLTMPro, bidirectional/shaping UI components, extra language packs, localization switching controls, or locale settings. No language picker is required.

This directive **supersedes and replaces** the older Arabic/bilingual requirements in the frozen ZIP, original P10.03 master plan, J05 acceptance wording, and older phase documentation wherever they conflict. Preserve earlier non-language functionality, all Native material accounting, and the user's requirement to defer the integrated Unity Editor/Player/XR campaign until code-first development is finished.

**Committed product source:** `18c06baa08c599d054dbf0def9b012a359facd59`. Compared to the last green pre-localization baseline `7edf9b95d88453b58b0a7e40bef65245cc0a53d3`, the English-only branch keeps the English JSON catalog, product-copy parser, English Latin font, and all previous Native/Unity feature work. It removes the Arabic JSON, Arabic fonts, RTLTMPro plugin and asmdef reference, RTL-only label, and the in-progress multilingual language selector/persistence/Boot/Settings/XR changes.

**Tests:** The headless .NET product tests reject any non-English locale and invalid/duplicate/trailing-comma English catalog files, preserve prior valid English values on failure, and fail if forbidden second-language assets/plugin/runtime selectors reappear. GitHub Native/Windows/Ubuntu workflows are separate source-level evidence. The final pinned Unity 6000.3.25f1 campaign must compile the English-only assemblies, generate Boot/Lab scenes, inspect every visible English screen and status at multiple text scales, validate English scientific signs/units and no Arabic/RTL UI assets, and capture Desktop/XR artifacts. Do not label Unity verification as performed before then.

**Formal:** P10.03 remains Open/NotReviewed. The release gate G-R1 remains Open. The entire current 66-row Unity verification matrix remains NOT_RUN. PR #1 stays open. No Planner decision is required to execute this direct user language decision.

**Additional protection at `24ac6ca8b835b41ff52dfa4ac7673511f931f555`:** the English-only text parser rejects Arabic-script Unicode ranges in the catalog keys and values without replacing the last valid English labels, and the headless production tests exercise escaped Unicode bypass attempts. Native/UI object state is not modified by a rejected catalog. The final functional-source CI run must be checked separately from the doc checkpoint SHA.

**2026-10-16 source-tree audit:** the Git tree at `d0c3ebe1abc2026f0a07437be1638063cd15843f` contains only `Content/Localization/en.json` and its `.meta`, with no Arabic/RTL/LocalizedLabel/LanguageSelection asset paths under active `unity/` or `tools/`. English-only contradictions in the active product/UI/phase-map/first-release specs are superseded and corrected in this update; historical ledger translations and read-only reference assets are not shipping UI.
