# Full phase map and execution route

All P00–P16 retain original detailed tasks. P17–P24 extend the user’s pharmaceutical platform scope. Status means local implementation evidence, not scientific/device/planner acceptance. Each task file includes decisions, paths, acceptance and retained original phase.

| Phase | Name | العربية | Tasks |
|---|---|---|---|
| P00 | Baseline and durable execution control | تثبيت الأساس ومكان الاستكمال | 2 |
| P01 | Native ABI and runtime correctness | صحة المحرك والواجهة الأصلية | 3 |
| P02 | Materials, preparations and revisions | المواد والتحضير وتوازن الكميات | 3 |
| P03 | Tools, parcels and capture geometry | الأدوات والقطرات والالتقاط والتلوث | 3 |
| P04 | Scientific models and provenance | الحسابات العلمية ومصدر النتائج | 3 |
| P05 | Scheduling, budgets and safe holds | الجدولة والميزانيات والتعافي | 3 |
| P06 | Unity authority integration | ربط Unity بالمحرك | 2 |
| P07 | Operational VR and two-hand interaction | تشغيل VR كامل وتفاعل اليدين | 3 |
| P08 | Premium laboratory and spatial UI | المعمل Premium والواجهات المكانية | 4 |
| P09 | Indicators, mixing and support states | المؤشرات والخلط وحدود النموذج | 2 |
| P10 | Desktop, Arabic and learning journeys | Desktop والعربية وتجربة التعلم | 3 |
| P11 | Durable sessions, notebook and Review | الجلسات الدائمة والملاحظات والمراجعة | 3 |
| P12 | Guided research lessons and deep understanding | التجارب التعليمية والفهم العميق | 3 |
| P13 | Reproducible builds and CI | البناء المتكرر والتحقق الآلي | 2 |
| P14 | Performance and device evidence | الأداء وإثبات التشغيل على الأجهزة | 2 |
| P15 | Whole-app quality and final planner review | جودة التطبيق والمراجعة النهائية | 2 |
| P16 | Push, release handoff and continuation | رفع العمل وتسليمه والاستكمال | 2 |
| P17 | Compound library and model capability registry | مكتبة المركبات وسجل قدرات النماذج | 3 |
| P18 | Analytical and physical pharmacy | الكيمياء التحليلية والصيدلة الفيزيائية | 3 |
| P19 | Organic and medicinal chemistry | الكيمياء العضوية والدوائية | 2 |
| P20 | Pharmaceutics and formulation | الصيدلانيات والتركيبات | 3 |
| P21 | Pharmacognosy and biochemical methods | العقاقير والطرق الحيوكيميائية | 2 |
| P22 | Microbiology and aseptic technique | الميكروبيولوجي وتقنيات العمل المعقم | 2 |
| P23 | Student, instructor, expert and research workspaces | مساحات الطالب والمدرس والخبير والباحث | 4 |
| P24 | Expanded platform release and coverage audit | إصدار المنصة الموسعة ومراجعة التغطية | 2 |

## Actual resume priority

P00.01 → P00.02 → P01.01 → P01.02 → P01.03 → P02.01 → P02.02 → P02.03 → P04.01 → P04.02 → P04.03 → P05.01 → P05.02 → P05.03 → P06.01 → P06.02 → P08.01 → P08.02 → P07.01 → P07.02 → P07.03 → P03.01 → P03.02 → P03.03 → P08.03 → P08.04 → P09.01 → P09.02 → P10.01 → P10.02 → P10.03 → P11.01 → P11.02 → P11.03 → P12.01 → P12.02 → P12.03 → P13.01 → P13.02 → P14.01 → P14.02 → P15.01 → P15.02 → P16.01 → P16.02 → P17.01 → P17.02 → P17.03 → P23.01 → P23.02 → P23.03 → P23.04 → P18.01 → P18.02 → P18.03 → P19.01 → P19.02 → P20.01 → P20.02 → P20.03 → P21.01 → P21.02 → P22.01 → P22.02 → P24.01 → P24.02

The task dependency graph in tracker and each phase file is authoritative. This order is priority, not a single global dependency chain. P17 catalog and P23 workspaces can proceed independently of missing new science-pack sources once their actual core dependencies are verified. P18–P22 are separate packs. P24 depends on the implemented claimed packs; a blocked pack must remain pending and outside advertised coverage, not falsely counted complete.

P08 shell/assets run early to expose a visible laboratory; P08 full acceptance waits for tools/VR/journey. All 95 original criteria are mapped in tracker/acceptance-95-map.json. New tasks add scope without deleting original cases. Phases retain separate local software, scientific/device/human and planner acceptance states.


## v2 priority decision
Read12-PRODUCT-DECISION-AR.md and13-FIRST-RELEASE-GATE.md. They control release order: complete an actual usable learner/instructor/expert laboratory first. Broad scientific-pack implementation begins after G-R1 local evidence; original25-phase roadmap is retained. No new human approval step is required merely to resume coding. Task dependencies and release-gate dependencies both apply.
