# U02 — membership selection UI-only code increment (18 October 2026)

## Authority and scope
- RED source+test commit: `6fde5ba5ac595b9011940bec73d2247cd659e510`.
- Verified RED: push Native Actions run [39071932833](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39071932833), Ubuntu `panel-routing` job `117511160592`, U02 step failed with `Fresh two-tenant membership snapshot was refused.` (`Program.cs:33`); earlier panel/Save/notebook steps passed. The intentional stub never claimed completeness.
- GREEN source commit: `e871ada682734bfe2e703b74381aa8aed99a97c6`.
- Verified GREEN: push run [39072049058](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39072049058), source-linked `.NET 8` test in `panel-routing` completed `U02 headless presentation-state assertions: 34 passed`. PR run [39072054575](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39072054575) also reports panel routing success.
- Real Unity Editor/EditMode/Player/XR: **NOT_RUN**. `UniversityMembershipSessionTests.cs` is authored for local Codex final campaign. Full Native Windows/Ubuntu conclusions on the GREEN SHA require independent final status inspection.
- SQL/Postgres/Auth/Storage/RLS: **NOT_RUN**. No PharmaLabVR Supabase project has been provisioned; no schema was applied. U02 and G-U1 remain OPEN.

## Changed contract
`unity/Assets/PharmaLabVR/Runtime/University/UniversityMembershipSession.cs` implements a standalone, source-linked presentation-only university selector. It never stores bearer tokens or communicates with Supabase. Input snapshots are supplied by a future authenticated gateway; every rejected refresh clears the previously cached membership. Explicit canonical nonzero Auth/tenant UUIDs, recognized institutional roles/statuses, bounded membership counts, duplicate rejection and a five-minute freshness / Auth expiry window fail closed. Tenant selection reads only active rows; distinct roles remain tenant-scoped, invitation/revocation never grants selection, and logout discards the selector.

**Security boundary:** This selector does not validate the trustworthiness of supplied snapshots or authorize sync, enrollment, grades, evidence reads or storage writes. U02 remains incomplete until a real verified Auth+membership adapter and server-enforced RPC are implemented and tested. A cloud outage must not block independent Native practice. No service-role key is suitable for Unity or browser.

## Follow-up
1. Auth gateway, invite acceptance, role revocation and audit RPCs with genuine two-tenant Postgres/RLS tests when a disposable local database is available.
2. U04 append-only local event/receipt outbox, explicit server acknowledgements, duplicate retry/fork handling.
3. Keep P00–P24 66-task ledger unmodified and preserve P11.01 imported `SessionLoad` Hold; next formal original task remains P06.02.
