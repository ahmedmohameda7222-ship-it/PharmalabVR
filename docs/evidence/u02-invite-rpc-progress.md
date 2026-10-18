# U02 scoped SQL membership lifecycle, 18 October 2026

**Source only; database unapplied.** The owner has not provisioned the PharmaLabVR Supabase project.

- Test-first RED `39b03436675d5cdf11f5223e3333b01fc0473ff2`: Ubuntu push run `39084184284` rejected `test_u02_membership_rpc_is_server_checked_and_audited` because the expected RPC migration did not exist.
- GREEN migration SHA `73eb30ddf8ea17b9e3be3d0800695fe9a7a2c03c`: Windows+Ubuntu native/tooling and headless jobs succeeded on push run `39084372919` and PR run `39084377527`.
- `supabase/migrations/20261018000000_university_membership_rpc.sql`: three SECURITY DEFINER functions with empty `search_path` and explicit authenticated-only EXECUTE grants. `university_invite_member` may invite **existing, email-verified Auth accounts** to instructor/TA/student roles in the admin's own tenant; admin-role grants are intentionally forbidden. `university_accept_invite` accepts only the authenticated account's own pending invitation while the inviter remains active; `university_revoke_member` allows active tenant admin revocation but not of university admins. Each operation writes immutable audit data, guards replay identity and locks relevant membership rows.
- `supabase/security-tests/tenant_isolation.sql` includes synthetic A/B invite, self-accept, revoked authorization, exact replay, changed-role replay, unauthorised role, cross-tenant and append-only audit assertions. **PostgreSQL fixture NOT_RUN**. Static contract tests are not substitute evidence.
- **Open:** no Auth transport/invitation email delivery, owner/bootstrap provisioning, throttling, RLS/Storage runtime evidence, signed backend receipt, real user identities, GDPR/privacy/retention review or pilot. G-U1 OPEN. No live Unity/Player tests.
