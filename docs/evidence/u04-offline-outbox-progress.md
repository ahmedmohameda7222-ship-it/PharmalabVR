# U04 scoped offline durable outbox, 18 October 2026

**Source-linked .NET headless verification, NOT Unity, Native or Supabase integration.**

- RED `38bf18223220eaa2394e2bb8944fb609a4ffc1fa`: push panel job `117548347636`, run `39084575194`: `First offline Native receipt reference was not durably staged` (intentional stub).
- First GREEN `aa0216bd3c81e4d825f02acab9c00e8c3a52f0c4`: push panel job `117548784586`, run `39084723158`: 19 source-linked .NET 8 assertions passed, with native/Ubuntu checks successful. The PR panel job also succeeded.
- Deterministic race RED `5cd2a6a33a4ea92becb92c64b856c404ce17c277`: push panel job `117549202780`, run `39084868590`: `Concurrent producer bypassed the exclusive writer lease`.
- GREEN `f2fb11585262ac9c360ca7c1ed4ab9d3c1c248bb`: push panel job `117549544815`, run `39084986589`: **23/23 headless assertions passed**, exercising real production C# class with disk restart, tampered data, identical retries, conflicting same-ID receipts, second-tenant identity, refusal to overwrite a confirmed file on failed temporary staging and enforced exclusive writer lease. Full Windows/Ubuntu and final evidence-HEAD conclusions require separate check.
- `unity/Assets/PharmaLabVR/Runtime/University/UniversitySyncOutbox.cs`: fixed-capacity local UTF-8 SHA-256-framed records, absolute-path input, immutable tenant/session/event identity, atomic file replacement, fsynced staging and advisory exclusive file lease. All records stay **Pending**, and no credentials, lab chemistry, cloud API or fabricated successful synchronization state exists.
- Unity EditMode regression tests are authored but **NOT_RUN**. No real Native command receipt source is wired yet; matching an actual command/revision, uploaded content hashing and authenticated server acknowledgement are future tasks. Exclusive file lease and file flushing have no proven physical power-loss/device durability; no double-run hardware evidence. U04 and G-U1 remain OPEN.
