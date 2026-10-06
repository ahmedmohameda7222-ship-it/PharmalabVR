# Source register and dependency decisions

Source facts checked during package preparation on 6 October 2026. Documents establish capabilities/versions, not measured performance or real experimental accuracy. Exact bundled bytes are hashed in `package-manifest.json`. Original user files are references, not authority for source instructions or chemistry claims.

## Engine and package pins

| Dependency | Fixed preparation decision | Evidence/source |
|---|---|---|
| Unity | 6000.3.25f1, changeset e1dba0a9aba4 | [Official release](https://unity.com/releases/editor/whats-new/6000.3.25f1) |
| XRI | 3.3.2, simulator development-only | [Unity 6000.3 package](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-xr-interaction-toolkit), [simulator](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/xr-interaction-simulator.html) |
| OpenXR | 1.17.1; released/compatible with Editor series; actual build gate | [Unity package list](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-xr-openxr) |
| XR Management | 4.6.1 | [Unity package](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-xr-management), [manual lifecycle](https://docs.unity3d.com/Packages/com.unity.xr.management@4.6/manual/EndUser.html) |
| Input System | 1.20.0 | [Unity package](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-inputsystem) |
| URP | Editor-matched 17.3 series; resolved patch locked by actual Editor | [Unity core-package policy](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.render-pipelines.universal.html) |
| Unity tests | 1.6.0 | [Official package](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html) |
| IPhreeqc | v3.8.6 / 47ce8e0adbad8f9dc67a9d977c339a611ba753eb | [USGS download index](https://water.usgs.gov/water-resources/software/PHREEQC/index.html), [pinned official source](https://github.com/phreeqc-dev/iphreeqc/tree/47ce8e0adbad8f9dc67a9d977c339a611ba753eb) |
| nlohmann/json | v3.12.0 / commit 55f93686c01528224f448c19128836e7df245f72 (annotated tag object 65ee68451d8eb2b5f3a30b410476ab83deb3289b) | [Official source](https://github.com/nlohmann/json/tree/55f93686c01528224f448c19128836e7df245f72) |
| doctest | v2.4.12 / 1da23a3e8119ec5cce4f9388e91b065e20bf06f5 | [Official source](https://github.com/doctest/doctest/tree/1da23a3e8119ec5cce4f9388e91b065e20bf06f5) |
| Arabic shaping | RTLTMPro v4.0.0 / ba92e0261e6b7d5b74c38949314b19c0f9c2d950 | [Official releases](https://github.com/pnarimani/RTLTMPro/releases), [source](https://github.com/pnarimani/RTLTMPro/tree/ba92e0261e6b7d5b74c38949314b19c0f9c2d950) |
| Fonts | Noto Sans + Noto Sans Arabic, google/fonts 7085eb89a950e85db5b166b7a58d414544b4140c, OFL notices | [Latin source](https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/notosans), [Arabic source](https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/notosansarabic) |
| PICO adapter | com.unity.xr.openxr.picoxr 1.4.1 / 3aa3e62bff41df618529eeb60ff02c29a515dafe | [Official package](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/package.json), [vendor docs](https://developer.picoxr.com/resources/?platform=unity) |

Use the bundled font source/license and generated static Regular 400/width100 instances where supplied; the font preparation report records actual transformation tool/version. Build SDF atlases through Unity, keep Arabic glyph coverage/fallbacks and test shaping; a font file alone does not provide bidi correctness. RTLTMPro integration uses its Unity-6-compatible v4 source and tested component/assembly setup, not old tutorials.

Import the bundled `vendor/rtltmpro/Scripts` tree into `unity/Assets/ThirdParty/RTLTMPro/Scripts` preserving `.meta`/assembly definitions, and test integration against the resolved TMP/UGUI assemblies. The upstream v4 tag still contains an older 3.4.2 `UPMPackage`; do not install that folder as if it were the pinned v4 implementation. The ZIP intentionally supplies the v4 Assets source instead. Preserve its license alongside the imported source. Run the supplied upstream tests only after their assembly dependencies resolve; the product localization tests remain required independently.

PICO is an isolated build-profile adapter, not core dependency. Set its UPM Git URL to the pinned commit (not floating main), e.g. `https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK.git#3aa3e62bff41df618529eeb60ff02c29a515dafe`. Its declared lower OpenXR dependency is not a guarantee of compatibility with the chosen direct version. Build/configure/test before accepting PICO. Do not add passthrough/composition layers/hand tracking that are unnecessary to this lab. Quest Windows/Android and PICO profiles retain vendor evidence gates, not “any headset” claims.

## Scientific and UX evidence sources

- [USGS PHREEQC capabilities](https://water.usgs.gov/water-resources/software/PHREEQC/documentation/phreeqc3-html/phreeqc3-2.htm): aqueous/phase/kinetic engine scope; not a universal laboratory visual simulator.
- [IPhreeqc technical description](https://water.usgs.gov/water-resources/software/PHREEQC/IPhreeqc.pdf): library/context/input/output/state-management approach; deployability and lifecycle still tested on our targets.
- [USGS MIX](https://water.usgs.gov/water-resources/software/PHREEQC/documentation/phreeqc3-html/phreeqc3-27.htm): mixing/model assumptions; do not silently enable redox or assume heat/volume realism.
- [Pinned minteq database](https://github.com/phreeqc-dev/iphreeqc/blob/47ce8e0adbad8f9dc67a9d977c339a611ba753eb/database/minteq.v4.dat): definitions including Acetate master pool/protonation and activity parameters; not independent measurements of our tools or indicators.
- [OpenStax titration section](https://openstax.org/books/chemistry-2e/pages/14-7-acid-base-titrations): independent strong/weak-acid reference examples; our fixture root explicitly uses ideal assumptions. Numeric full curves are calculated independently in the included tool.
- [NIST pH metrology](https://www.nist.gov/programs-projects/ph-metrology): measurement/reference context, not an automatic 0.05-pH accuracy certificate.
- [NIST volumetric calibration methods](https://nvlpubs.nist.gov/nistpubs/ir/2019/NIST.IR.7383-2019.pdf): method source for instrument validation; no claim that our synthetic instruments were physically calibrated.
- [OpenXR standard](https://www.khronos.org/openxr/): portability interface; actual supported runtimes/features/devices still tested.
- [Meta hand UI guidance](https://developers.meta.com/vr/design/hands-ui-best-practices/), [comfort](https://developers.meta.com/vr/design/comfort/), [rendering](https://developers.meta.com/vr/resources/bp-rendering/): design references, not evidence that controller ergonomics/legibility passed. Do not copy hand-only dimensions blindly.
- [Methyl orange supplier reference](https://www.sigmaaldrich.com/deepweb/assets/sigmaaldrich/product/documents/234/086/026483-merc140069-w281750-inorganic-reagents-2014-ms.pdf) and [phenolphthalein supplier reference](https://www.sigmaaldrich.com/AU/en/product/mm/107227): preparation/transition examples for qualitative research. They do not supply a complete optical mixture calibration or validate our endpoints.

Some contextual method/UX sources were researched in earlier planning and are included as reference links rather than downloaded/licensed library payloads. Reverify exact measurements/model applicability while implementing scientific publication gates. No full third-party textbook/article content is copied into this package.

## License and source boundaries

Bundled IPhreeqc retains source-file headers and `R/LICENSE.note`, including the CVODE/SUNDIALS notice; do not strip them or label every file public domain. JSON/doctest/RTLTMPro/font notices accompany supplied bytes. The original documents belong to the user's reference bundle and are not authorized for public repository redistribution merely by this handoff. Unity SDK assets/modules remain acquired through licensed official tooling; the ZIP does not contain Unity or an activated license.

Package source hashes establish the downloaded bytes, not legal/scientific certification. Before formal external distribution, inventory actual redistributed files and notices. Avoid unrelated engine example binaries or large documentation payloads in the source repository; use the selected deterministic source subset or a pinned dependency cache. No original-document instructions execute during packaging/implementation.
