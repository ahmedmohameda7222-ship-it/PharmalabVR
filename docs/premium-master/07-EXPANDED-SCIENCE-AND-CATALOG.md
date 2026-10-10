# Compound library and expanded scientific model packs

## Product breadth
The platform serves pharmaceutical science broadly. A searchable compound catalog can be extensive; a reliable predictive simulation must declare a narrower model domain. The initial six-material profile is the first supported pack, not the final product vision. Never interpret a catalog entry or a rendered instrument as scientific simulation support.

## Three distinct registries
1. CompoundRecord: stable internal ID, English/Arabic names, formula, exact form/salt/hydrate/stereochemistry, verified CID/InChIKey where available, molecular structure when applicable, family tags, source/date/license. Each measured or predicted property carries units, conditions, uncertainty and its own source. Unknown fields stay null.
2. MaterialPreparation: components and quantities, solvent, reference volume, temperature, phase, purity, provenance and referenced compound IDs. A solution, formulation, extract or mixture is not a single molecule. Nominal preparations and measured composition are distinguished.
3. CapabilityPackage: version/hash, provider type, allowed materials and combinations, conditions, observables, equipment, assumptions, calibration, validation, uncertainty, maturity and device evidence. A missing provider returns ReferenceOnly or Unsupported. No fallback pH or reaction product is guessed from a formula or name.

## Interface and data contracts
`CompoundCatalog.Search(CatalogQuery query) -> IReadOnlyList<CompoundRecord>` accepts locale/text/family/current-pack filters. Records are immutable metadata, not inventories.
`CapabilityRegistry.Check(PreparationSpec prep, ExperimentConditions conditions, ObservableId quantity) -> CoverageDecision` returns allowed/reason/package identity/maturity/required evidence. Check mixtures both before and after material changes; checking each separate stock is insufficient.
`IModelProvider` consumes immutable authoritative material revisions, conditions and package identity. It returns observations with dependency identity, validity, units and uncertainty. Equilibrium, kinetic, multiphase, instrument forward models and recorded datasets are distinct provider kinds. Before any time-coupled model commits, document and verify its coupling contract; equilibrium coalescing cannot silently become a kinetic integrator.
Machine-readable schemas are in schemas/. Provider-specific parameter schemas must be supplied by each pack and tested against its source data. Schema validity alone is not numerical validation.

## Data sourcing
Start with the approved six materials, then curate commonly used pharmaceutical chemicals, solvents, APIs, excipients and reference standards by station. Use PubChem PUG REST/PUG View for verified identity/property/annotation ingestion, obey service limits and cache results. Record property-level sources, dates, licenses and conditions. The lab remains offline using versioned local packs; update packs outside active sessions, preserving identity.
Do not download the entire PubChem catalog as a prerequisite. Hazard/SDS and pharmacopoeial methods require actual references and applicable permissions; generated text and copied unlicensed full monographs are not authoritative data.

Candidate families: aqueous acids/bases/salts/buffers; organic solvents; functional-group examples; selected APIs; excipients; natural-product constituents; reference standards. Candidate names for source lookup include ethanol, methanol, acetone, ethyl acetate, citric acid, sodium bicarbonate, potassium chloride, glucose, sucrose, lactose, glycerol, propylene glycol, urea, caffeine, aspirin, paracetamol, ibuprofen, benzoic acid, salicylic acid, ascorbic acid, sodium benzoate and magnesium stearate. This is a lookup seed, not an assertion of properties or of six-material model support. Exact form, source and coverage must be verified.

## Station packs
Analytical: supported titration/buffer/dilution first; conductometry/UV-Visible/calibration and chromatography subpacks require instrument models or properly sourced measured datasets, standards/blanks and held-out checks.
Physical pharmacy: solubility, partition, dissolution and kinetics with explicit temperature/solvent/phase/particle conditions, conserved material and reference curves.
Organic/medicinal: real molecular structure and stereochemistry learning; bounded extraction/recrystallization/TLC packs. Reactions require documented stoichiometry, conditions and rate/yield data; arbitrary mixing is not a universal reaction predictor.
Pharmaceutics: solution/suspension/emulsion/semi-solid workflows and powder/tablet quality lessons. Phase-aware formulation and sourced performance are separate from any clinical efficacy claim.
Pharmacognosy/biochemical: botanical/extract mixtures, licensed microscopy specimens and controlled calibration/enzyme-assay datasets with declared conditions.
Microbiology/aseptic: procedural contamination training and documented reference images/datasets. Dynamic biological output needs a validated provider within declared organism/media/condition coverage.

## Per-pack execution gate
Manifest/schema → source/coverage inventory → immutable provider interface → independent reference fixtures and held-out checks → domain integration → equipment/preparation → VR/Desktop journey → bilingual explanation → Review/export → measured performance → acceptance.
No pack passes because its catalog card opens. Missing required data is an explicit named blocker; implement independent catalog/UI/workspace work meanwhile. Synthetic data is labelled synthetic and cannot be counted as independent experimental validation. A release may include working supported packs while blocked packs remain visible pending scope; the full backlog is not deleted.

An honest release publishes its coverage matrix rather than claiming every compound and every possible laboratory operation is predicted correctly. This supports useful breadth while protecting scientific understanding from invented results.
