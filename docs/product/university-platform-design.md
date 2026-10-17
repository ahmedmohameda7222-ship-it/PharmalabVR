# PharmaLabVR multi-university design — 17 October 2026

Owner-approved new scope: multiple universities share one platform, with authorization bound to active membership of each institution and enrolled or staffed course section, **not email domains or client-provided roles**. A person can be an instructor at one university and a student at another. The product is permanently English-only.

Core C++17/IPhreeqc remains offline-first and scientifically authoritative; Supabase is an identity, lessons, assignments, academic-record and evidence **metadata** service only. Native checkpoints are never replaced by cloud persistence.

Implementation is tracked separately as U01–U05, with G-U1 pilot security gate. U01 ships source-only SQL migrations and executable synthetic PostgreSQL RLS test specification; owner currently has no project account, so **neither schema nor SQL tests have been executed on a database**. All student records are synthetic in test source. Institution provisioning, private upload mediation, audited grade approval, membership mutation, offline sync and portal are subsequent tasks, not implied by read policies.

Security guarantees require actual PostgreSQL RLS and Auth/Storage integration tests with two tenants, role revocation, forged composite IDs, grade-mutation denies, private object access, idempotent retry and institutional privacy review. The existing native P11.01 SessionLoad Hold and original 66-task ledger stay independent; G-R1 is still open.
