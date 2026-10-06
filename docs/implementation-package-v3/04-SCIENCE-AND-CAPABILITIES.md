# Scientific implementation and capability boundaries

## Frozen first profile

Profile ID `aqueous-six-research-v1`, maturity Research. Water, hydrochloric acid, sodium hydroxide, sodium chloride, acetic acid and sodium acetate. Temperature 298.15 K, pressure 101325 Pa; prepared stock test points 0.01/0.05/0.1 mol/L, explicitly a research matrix, not an approved continuous range. No active heating, gas exchange, precipitation, redox or kinetic coupling. An open-looking bench does not imply a chemically open CO2 boundary. Capabilities panel states isothermal/closed-atmosphere and homogeneous approximation.

Stock configuration is data: ID, preparation, concentration convention, prepared reference volume, analytical pool amounts, nominal solvent convention, source provenance and allowed range. HCl contributes chloride, NaOH sodium, NaCl equal sodium/chloride, acetic acid acetate, sodium acetate equal sodium/acetate. The derived charge-balance solution determines hydrogen/hydroxide and protonated/deprotonated acetate; preparation cannot hardcode final pH. Include the first profile's solvent/volume approximation in every evidence report.

## Pinned IPhreeqc integration

Vendor reference is `vendor/iphreeqc-source`, upstream `phreeqc-dev/iphreeqc`, tag v3.8.6, commit `47ce8e0adbad8f9dc67a9d977c339a611ba753eb`. Use its C++ library in-process, not Windows COM, spawned shell commands or file-per-solve. Build static position-independent IPhreeqc and link it into `pharmalab_core`; no second DLL distribution dependency if avoidable. Core uses C++17; upstream C++14 requirements are compatible only after actual compilation. Preserve upstream licensing/notices; no assumption that every file is public domain.

All instance registry/context operations execute on SolverWorker. Initial one context per active vessel, no concurrent access to a context. The supplied Git source has placeholder version macros; record the pinned upstream commit/tag, actual loaded database hash, adapter build identity and observed runtime version string separately. Do not infer content provenance from that placeholder string or silently edit it to pretend an official binary build.

Use the supplied `database/minteq.v4.dat` as pinned data for the research adapter. Keep it intact initially to avoid deleting dependent activity/master-species definitions. Restrict student inputs and enabled model operations to the allowed pools; absent elements stay absent. No PHASES/EQUILIBRIUM_PHASES/GAS_PHASE/SURFACE/EXCHANGE/KINETICS or arbitrary student-generated PHREEQC programs. A later trimmed allowlist database requires equivalence tests against the intact restricted-input baseline. This supersedes the earlier assumption of trimming before first integration.

Construct a complete new `SOLUTION` request from immutable pools and solvent every solve, with `units mol/kgw`, `temp 25`, actual `-water` mass and Na/Cl/Acetate totals divided by kg water. Use charge balance on hydrogen (`pH 7 charge`) as the initial guess/constraint for this restricted six-material aqueous profile; never charge-balance sodium/chloride to manufacture neutrality or discard their totals. Omit redox/phase changes and reject any unexpected pool. The adapter must prove pool balance and solution-charge convergence over acid, base, acetate and mixed fixtures before accepting this mapping. PHREEQC totals/solvent semantics are not identical to simply entering mol/L.

Use `DELETE`/complete reconstruction or reinitialize/import so prior solution definitions/results cannot survive incorrectly. Do not reuse a prior cell's reacted content as the new authoritative input. `SELECTED_OUTPUT` returns calculated pH, Na, Cl, Acetate, ionic strength, charge error and available species diagnostics; convert returned units explicitly. Use named columns resolved from header, not magic row/column indices. Disable output/log/dump files; retain bounded error text. Output references a single solve request, never a growing table's previous successful row after a failure.

Read solver warning/error codes, validate finite results, check represented-pool balances and charge residual before publishing. For initial research reject nonconvergence; keep prior observations stale and enter the correct failure state. Engine iteration/failure limits and clean lifecycle are acceptance gates. Complete same-input clean-context equivalence tests following diverse prior compositions. If the adapter cannot satisfy deployment/science/performance requirements, produce the specific failed cases and compare alternatives; do not silently downgrade the software to scripts or claim a solved problem.

## Independent ideal fixtures

`fixtures/ideal-aqueous.json` and `tools/verify_ideal_fixtures.py` are independently generated reference evidence, not the product solver. They solve the single charge-balance root:

`H + Na − Cl − AcTotal·Ka/(Ka+H) − Kw/H = 0`.

Use Ka=1.8e−5, Kw=1e−14, ideal activities, additive volumes and no atmosphere/indicator. Solve in log10(H) space with bracketing/bisection, not Henderson–Hasselbalch across every endpoint or a piecewise experiment branch. Full root handles acid, base, water and mixed acetate fixtures. Reference tests use these assumptions; realistic engine activity conventions need matching-assumption comparisons, not forced agreement.

Reference pH target ≤0.001 only when assumptions match. Freshly calculated fixture residuals/expected values are in the preparation report. Conservation project tolerance for represented pools is 1e−12 mol +1e−9 relative. Real experimental pH accuracy goal 0.05 is provisional, requiring independent measurements/uncertainties. Ideal numeric agreement cannot establish it.

## Indicators and local mixing

Do not invent authoritative indicator preparations or extinction spectra. Implement generic model/config schemas and observation plumbing, then include explicit Approximate research visualizations only when the model has identifiable supplied constants and preparation. A transparent qualitative pH-to-transition curve may show theoretical acid/base indicator fractions with a separately labeled approximate visual mapping; it is not a measured endpoint color or certified delivered-indicator interaction. Keep added indicator/solvent quantity in the inventory, and never select a mixture's color by the last-added indicator.

Use independently sourced preparation/pKa/spectral calibration to promote an indicator capability. If unavailable, retain a colorless Unsupported optical result plus a capability explanation; do not decorate the final screenshot with a fake validated endpoint. The API/UX paths must be implemented even if the validated optical data are absent. Separate fitting versus held-out data, quantity/solvent effects and optical display/lighting profile. Do not make successful lesson completion depend on unsupported color.

The first homogeneous profile does not pretend to simulate swirling/local endpoint persistence. Implement a research mixing provider interface and compare homogeneous, three-region and more detailed approaches using tracer/convergence evidence; unvalidated coefficients remain research/approximate. A swirl animation does not prove chemical mixing accuracy. Phase/kinetic time advances require a distinct coupled design; do not retrofit asynchronous historical-observation commits into them.

## Research lesson data

Include two configurable example instruction sets, disabled for certified grading: strong acid/base titration and HCl/acetic-acid mixture. They subscribe to current model/state/events, never set chemical results. Each declares capability, mode/assistance, preparation and observation requirements. Reading a burette remains a reading task rather than entering a prefilled value.

The original mixed-acid protocol's “first endpoint exactly HCl, second exactly acetate” is a hypothesis requiring analysis, not truth. Keep cumulative versus refill-stage volume separate; no reset of material history when resetting the displayed burette reading. Overshoot, contamination, rinse and spill remain consequential. If original indicator preparation/endpoint persistence cannot be validated, expose the lesson as incomplete Research guidance and explain the missing capability; do not force success at a hardcoded volume.

## Publication matrix

For every observable/profile: composition ratios; stock and mixture concentration; ionic strength/model applicability; T/P/atmosphere; volume convention; uncertainty; interpolation/extrapolation; calibration/reference sources; held-out verification; supported devices; assistance and task objectives. Hash engine, database, profile and adapter identity into package provenance. Library source provenance is not independent experimental validation.

Research is the initial shipped local mode. Only independently evidenced capabilities may be Published. Unsupported combinations may remain explorable through supported material transport without fabricated chemical effects. Generic instructions cannot enable a missing scientific domain. This foundation does not contain arbitrary iodine/chloride predictions.
